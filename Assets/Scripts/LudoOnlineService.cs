using System;
using System.Collections.Generic;
using UnityEngine;

namespace PremiumLudo
{
    public sealed class LudoOnlineService : MonoBehaviour
    {
        private const string DefaultServerBaseUrl = "https://ludo-server-vg5b.onrender.com";
        private const float PendingCommandTimeoutSeconds = 20f;
        private const float SlowConnectNoticeSeconds = 6f;

        [Serializable]
        private sealed class CreateRoomRequest
        {
            public string PlayerName;
            public int PlayerCount;
            public string LocalColor;
            public string[] ActiveColors;
        }

        [Serializable]
        private sealed class JoinRoomRequest
        {
            public string RoomCode;
            public string PlayerName;
            public string PreferredColor;
        }

        [Serializable]
        private sealed class RecoverSessionRequest
        {
            public string RoomCode;
            public string PlayerId;
        }

        [Serializable]
        private sealed class RoomPlayerRequest
        {
            public string RoomCode;
            public string PlayerId;
        }

        [Serializable]
        private sealed class ChatRequest
        {
            public string RoomCode;
            public string PlayerId;
            public string Sender;
            public string Message;
            public string Color;
        }

        [Serializable]
        private sealed class TurnRequest
        {
            public string RoomCode;
            public string PlayerId;
            public string Color;
            public int Roll;
            public int TokenIndex;
            public bool NoMove;
        }

        private enum PendingCommand
        {
            None = 0,
            CreateRoom = 1,
            JoinRoom = 2,
            StartMatch = 3,
            RecoverSession = 4,
        }

        public event Action<LudoRoomSnapshot> RoomSnapshotReceived;
        public event Action<LudoRoomSnapshot> MatchStartedReceived;
        public event Action<LudoChatMessage> ChatMessageReceived;
        public event Action<LudoTurnActionMessage> TurnActionReceived;
        public event Action<string> StatusChanged;
        public event Action<string> ErrorReceived;

        private string _serverBaseUrl = DefaultServerBaseUrl;
        private string _roomCode = string.Empty;
        private string _roomId = string.Empty;
        private string _localPlayerName = "Player";
        private string _localColor = string.Empty;
        private string _playerId = string.Empty;
        private bool _isHost;
        private bool _connected;
        private long _lastRoomSequence;
        private long _lastChatSequence;
        private long _lastTurnSequence;
        private long _lastStateVersion;
        private PendingCommand _pendingCommand;
        private CreateRoomRequest _pendingCreateRoomRequest;
        private JoinRoomRequest _pendingJoinRoomRequest;
        private RoomPlayerRequest _pendingStartRequest;
        private RecoverSessionRequest _pendingRecoverRequest;
        private LudoSocketIoClient _socketClient;
        private float _pendingCommandDeadlineAt;
        private float _slowConnectNoticeAt;
        private bool _slowConnectNoticeSent;

        public string RoomCode
        {
            get { return _roomCode; }
        }

        public bool IsConnected
        {
            get { return _connected && _socketClient != null && _socketClient.IsConnected; }
        }

        public bool IsHost
        {
            get { return _isHost; }
        }

        public string LocalColor
        {
            get { return _localColor; }
        }

        public string LocalPlayerName
        {
            get { return _localPlayerName; }
        }

        public string LocalPlayerId
        {
            get { return _playerId; }
        }

        public string ServerBaseUrl
        {
            get { return _serverBaseUrl; }
        }

        public bool HasPendingCommand
        {
            get { return _pendingCommand != PendingCommand.None; }
        }

        private void Awake()
        {
            EnsureSocketClient();
        }

        private void OnDestroy()
        {
            TearDownSocketClient();
        }

        private void Update()
        {
            if (_socketClient != null)
            {
                _socketClient.Update();
            }

            UpdatePendingCommandState();
        }

        public void ConfigureServer(string serverBaseUrl)
        {
            if (!string.IsNullOrWhiteSpace(serverBaseUrl))
            {
                _serverBaseUrl = serverBaseUrl.Trim().TrimEnd('/');
            }
        }

        public void CreateRoomAndJoin(string playerName, int playerCount, IReadOnlyList<LudoTokenColor> activeColors, LudoTokenColor localColor)
        {
            if (!CanUseOnline())
            {
                return;
            }

            ResetSessionState();

            _localPlayerName = SanitizePlayerName(playerName);
            _localColor = localColor.ToString();
            _isHost = true;
            _pendingCreateRoomRequest = new CreateRoomRequest
            {
                PlayerName = _localPlayerName,
                PlayerCount = Mathf.Clamp(playerCount, 2, 4),
                LocalColor = _localColor,
                ActiveColors = BuildColorArray(activeColors),
            };
            _pendingCommand = PendingCommand.CreateRoom;
            BeginPendingCommandWindow();

            EmitStatus("Connecting to lobby...");
            ConnectOrDispatchPending();
        }

        public void JoinRoom(string roomCode, string playerName, LudoTokenColor preferredColor)
        {
            if (!CanUseOnline())
            {
                return;
            }

            string normalizedRoomCode = string.IsNullOrWhiteSpace(roomCode) ? string.Empty : roomCode.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(normalizedRoomCode))
            {
                EmitError("Enter a room code to join.");
                return;
            }

            ResetSessionState();

            _localPlayerName = SanitizePlayerName(playerName);
            _localColor = preferredColor.ToString();
            _isHost = false;
            _pendingJoinRoomRequest = new JoinRoomRequest
            {
                RoomCode = normalizedRoomCode,
                PlayerName = _localPlayerName,
                PreferredColor = _localColor,
            };
            _pendingCommand = PendingCommand.JoinRoom;
            BeginPendingCommandWindow();

            EmitStatus("Connecting to room " + normalizedRoomCode + "...");
            ConnectOrDispatchPending();
        }

        public void StartMatch()
        {
            if (!CanUseOnline() || string.IsNullOrEmpty(_roomCode) || string.IsNullOrEmpty(_playerId))
            {
                return;
            }

            _pendingStartRequest = new RoomPlayerRequest
            {
                RoomCode = _roomCode,
                PlayerId = _playerId,
            };
            _pendingCommand = PendingCommand.StartMatch;
            BeginPendingCommandWindow();
            EmitStatus("Starting match...");
            ConnectOrDispatchPending();
        }

        public void LeaveRoom()
        {
            if (_socketClient != null && _socketClient.IsConnected && !string.IsNullOrEmpty(_roomCode) && !string.IsNullOrEmpty(_playerId))
            {
                RoomPlayerRequest request = new RoomPlayerRequest
                {
                    RoomCode = _roomCode,
                    PlayerId = _playerId,
                };
                _socketClient.EmitJson("leaveRoom", JsonUtility.ToJson(request));
            }

            ResetSessionState();
            if (_socketClient != null)
            {
                _socketClient.Disconnect();
            }
            EmitStatus("Offline");
        }

        public void SendChat(string sender, string message, LudoTokenColor color)
        {
            if (_socketClient == null || !_socketClient.IsConnected || string.IsNullOrEmpty(_roomCode) || string.IsNullOrEmpty(_playerId) || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            ChatRequest request = new ChatRequest
            {
                RoomCode = _roomCode,
                PlayerId = _playerId,
                Sender = string.IsNullOrWhiteSpace(sender) ? _localPlayerName : SanitizePlayerName(sender),
                Message = message.Trim(),
                Color = color.ToString(),
            };
            _socketClient.EmitJson("sendChat", JsonUtility.ToJson(request));
        }

        public void SendTurnAction(LudoTurnActionMessage action)
        {
            if (_socketClient == null || !_socketClient.IsConnected || string.IsNullOrEmpty(_roomCode) || string.IsNullOrEmpty(_playerId) || action == null)
            {
                return;
            }

            action.PlayerId = _playerId;

            TurnRequest request = new TurnRequest
            {
                RoomCode = _roomCode,
                PlayerId = _playerId,
                Color = action.Color,
                Roll = action.Roll,
                TokenIndex = action.TokenIndex,
                NoMove = action.NoMove,
            };
            _socketClient.EmitJson("playTurn", JsonUtility.ToJson(request));
        }

        private void EnsureSocketClient()
        {
            if (_socketClient != null)
            {
                return;
            }

            _socketClient = new LudoSocketIoClient();
            _socketClient.Connected += OnSocketConnected;
            _socketClient.Disconnected += OnSocketDisconnected;
            _socketClient.EventReceived += OnSocketEventReceived;
            _socketClient.ErrorReceived += OnSocketErrorReceived;
        }

        private void TearDownSocketClient()
        {
            if (_socketClient == null)
            {
                return;
            }

            _socketClient.Connected -= OnSocketConnected;
            _socketClient.Disconnected -= OnSocketDisconnected;
            _socketClient.EventReceived -= OnSocketEventReceived;
            _socketClient.ErrorReceived -= OnSocketErrorReceived;
            _socketClient.Dispose();
            _socketClient = null;
        }

        private void ConnectOrDispatchPending()
        {
            EnsureSocketClient();
            if (_socketClient == null)
            {
                return;
            }

            if (_socketClient.IsConnected)
            {
                DispatchPendingCommand();
                return;
            }

            _socketClient.Connect(_serverBaseUrl, true);
        }

        private void OnSocketConnected()
        {
            _connected = true;
            EmitStatus(string.IsNullOrEmpty(_roomCode) ? "Connected" : "Connected to " + _roomCode);
            DispatchPendingCommand();
        }

        private void OnSocketDisconnected()
        {
            _connected = false;
            if (_socketClient != null && _socketClient.WantsReconnect && (!string.IsNullOrEmpty(_roomCode) || _pendingCommand != PendingCommand.None))
            {
                EmitStatus("Reconnecting...");
                if (!string.IsNullOrEmpty(_roomCode) && !string.IsNullOrEmpty(_playerId))
                {
                    _pendingRecoverRequest = new RecoverSessionRequest
                    {
                        RoomCode = _roomCode,
                        PlayerId = _playerId,
                    };
                    _pendingCommand = PendingCommand.RecoverSession;
                    BeginPendingCommandWindow();
                }
            }
            else
            {
                EmitStatus("Offline");
            }
        }

        private void OnSocketErrorReceived(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                EmitError(message);
            }
        }

        private void OnSocketEventReceived(string eventName, string payloadJson)
        {
            switch (eventName)
            {
                case "roomCreated":
                    HandleRoomCreated(Deserialize<LudoSocketRoomAck>(payloadJson));
                    break;
                case "playerJoined":
                    HandlePlayerJoined(Deserialize<LudoSocketSeatEvent>(payloadJson));
                    break;
                case "playerLeft":
                    HandlePlayerLeft(Deserialize<LudoSocketSeatEvent>(payloadJson));
                    break;
                case "gameStarted":
                    HandleGameStarted(Deserialize<LudoSocketRoomAck>(payloadJson));
                    break;
                case "turnPlayed":
                    HandleTurnPlayed(Deserialize<LudoSocketTurnEvent>(payloadJson));
                    break;
                case "chatMessage":
                    HandleChatMessage(Deserialize<LudoSocketChatEvent>(payloadJson));
                    break;
                case "gameStateUpdate":
                    HandleGameStateUpdate(Deserialize<LudoSocketGameStateEvent>(payloadJson));
                    break;
                case "errorMessage":
                    HandleSocketErrorPayload(Deserialize<LudoSocketErrorEvent>(payloadJson));
                    break;
            }
        }

        private void DispatchPendingCommand()
        {
            if (_socketClient == null || !_socketClient.IsConnected)
            {
                return;
            }

            switch (_pendingCommand)
            {
                case PendingCommand.CreateRoom:
                    if (_pendingCreateRoomRequest != null)
                    {
                        _socketClient.EmitJson("createRoom", JsonUtility.ToJson(_pendingCreateRoomRequest));
                    }
                    break;
                case PendingCommand.JoinRoom:
                    if (_pendingJoinRoomRequest != null)
                    {
                        _socketClient.EmitJson("joinRoom", JsonUtility.ToJson(_pendingJoinRoomRequest));
                    }
                    break;
                case PendingCommand.StartMatch:
                    if (_pendingStartRequest != null)
                    {
                        _socketClient.EmitJson("startGame", JsonUtility.ToJson(_pendingStartRequest));
                    }
                    break;
                case PendingCommand.RecoverSession:
                    if (_pendingRecoverRequest != null)
                    {
                        _socketClient.EmitJson("recoverSession", JsonUtility.ToJson(_pendingRecoverRequest));
                    }
                    break;
            }
        }

        private void HandleRoomCreated(LudoSocketRoomAck response)
        {
            if (!HandleAckErrors(response))
            {
                return;
            }

            _isHost = true;
            CompleteRoomAcknowledgement(response);
            EmitStatus("Room ready");
        }

        private void HandlePlayerJoined(LudoSocketSeatEvent response)
        {
            if (response == null)
            {
                return;
            }

            if (!response.Success && !string.IsNullOrWhiteSpace(response.Error))
            {
                EmitError(response.Error);
                return;
            }

            if (string.IsNullOrEmpty(_playerId) && !string.IsNullOrWhiteSpace(response.PlayerId))
            {
                _playerId = response.PlayerId;
            }

            if ((_pendingCommand == PendingCommand.JoinRoom || _pendingCommand == PendingCommand.RecoverSession || string.IsNullOrEmpty(_localColor)) && !string.IsNullOrWhiteSpace(response.AssignedColor))
            {
                _localColor = response.AssignedColor;
            }

            if (response.Snapshot != null)
            {
                ApplySnapshot(response.Snapshot);
            }

            _pendingJoinRoomRequest = null;
            if (_pendingCommand == PendingCommand.JoinRoom)
            {
                _pendingCommand = PendingCommand.None;
                ResetPendingCommandWindow();
            }
        }

        private void HandlePlayerLeft(LudoSocketSeatEvent response)
        {
            if (response != null && response.Snapshot != null)
            {
                ApplySnapshot(response.Snapshot);
            }
        }

        private void HandleGameStarted(LudoSocketRoomAck response)
        {
            if (!HandleAckErrors(response))
            {
                return;
            }

            _pendingStartRequest = null;
            if (_pendingCommand == PendingCommand.StartMatch)
            {
                _pendingCommand = PendingCommand.None;
                ResetPendingCommandWindow();
            }

            if (response.Snapshot != null)
            {
                ApplySnapshot(response.Snapshot);
                EmitMatchStarted(response.Snapshot);
            }

            EmitStatus("Match live");
        }

        private void HandleTurnPlayed(LudoSocketTurnEvent response)
        {
            if (response == null || response.Action == null)
            {
                return;
            }

            LudoTurnActionMessage action = response.Action;
            if (action.Sequence > 0 && action.Sequence <= _lastTurnSequence)
            {
                return;
            }

            _lastTurnSequence = Math.Max(_lastTurnSequence, action.Sequence);
            _lastStateVersion = Math.Max(_lastStateVersion, action.StateVersion);

            if (!string.IsNullOrEmpty(action.PlayerId) && string.Equals(action.PlayerId, _playerId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Action<LudoTurnActionMessage> handler = TurnActionReceived;
            if (handler != null)
            {
                handler(action);
            }
        }

        private void HandleChatMessage(LudoSocketChatEvent response)
        {
            if (response == null || response.Message == null)
            {
                return;
            }

            LudoChatMessage message = response.Message;
            if (message.Sequence > 0 && message.Sequence <= _lastChatSequence)
            {
                return;
            }

            _lastChatSequence = Math.Max(_lastChatSequence, message.Sequence);
            Action<LudoChatMessage> handler = ChatMessageReceived;
            if (handler != null)
            {
                handler(message);
            }
        }

        private void HandleGameStateUpdate(LudoSocketGameStateEvent response)
        {
            if (response != null && response.Snapshot != null)
            {
                if (_pendingCommand == PendingCommand.RecoverSession)
                {
                    _pendingRecoverRequest = null;
                    _pendingCommand = PendingCommand.None;
                    ResetPendingCommandWindow();
                    EmitStatus(response.Snapshot.Started ? "Recovered match" : "Recovered lobby");
                }

                ApplySnapshot(response.Snapshot);
                if (response.Snapshot.Started)
                {
                    EmitMatchStarted(response.Snapshot);
                }
            }
        }

        private void HandleSocketErrorPayload(LudoSocketErrorEvent response)
        {
            if (response != null && !string.IsNullOrWhiteSpace(response.Error))
            {
                if (_pendingCommand == PendingCommand.CreateRoom || _pendingCommand == PendingCommand.JoinRoom || _pendingCommand == PendingCommand.StartMatch)
                {
                    _pendingCommand = PendingCommand.None;
                    ResetPendingCommandWindow();
                }

                EmitError(response.Error);
            }
        }

        private bool HandleAckErrors(LudoSocketRoomAck response)
        {
            if (response == null)
            {
                _pendingCommand = PendingCommand.None;
                EmitError("The server returned an empty response.");
                return false;
            }

            if (!response.Success)
            {
                _pendingCommand = PendingCommand.None;
                ResetPendingCommandWindow();
                EmitError(string.IsNullOrWhiteSpace(response.Error) ? "The server rejected the request." : response.Error);
                return false;
            }

            return true;
        }

        private void CompleteRoomAcknowledgement(LudoSocketRoomAck response)
        {
            if (response == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(response.PlayerId))
            {
                _playerId = response.PlayerId;
            }

            if (!string.IsNullOrWhiteSpace(response.AssignedColor))
            {
                _localColor = response.AssignedColor;
            }

            _pendingCreateRoomRequest = null;
            _pendingJoinRoomRequest = null;
            _pendingRecoverRequest = null;
            if (_pendingCommand == PendingCommand.CreateRoom || _pendingCommand == PendingCommand.JoinRoom || _pendingCommand == PendingCommand.RecoverSession)
            {
                _pendingCommand = PendingCommand.None;
                ResetPendingCommandWindow();
            }

            if (response.Snapshot != null)
            {
                ApplySnapshot(response.Snapshot);
            }
        }

        private void ApplySnapshot(LudoRoomSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            string incomingRoomCode = string.IsNullOrWhiteSpace(snapshot.RoomCode) ? _roomCode : snapshot.RoomCode.Trim().ToUpperInvariant();
            string incomingRoomId = string.IsNullOrWhiteSpace(snapshot.RoomId) ? _roomId : snapshot.RoomId;
            bool exactDuplicate = !string.IsNullOrEmpty(incomingRoomCode)
                && string.Equals(incomingRoomCode, _roomCode, StringComparison.OrdinalIgnoreCase)
                && snapshot.RoomSequence == _lastRoomSequence
                && snapshot.StateVersion == _lastStateVersion
                && (string.IsNullOrEmpty(incomingRoomId) || string.IsNullOrEmpty(_roomId) || string.Equals(incomingRoomId, _roomId, StringComparison.Ordinal));
            if (exactDuplicate)
            {
                EmitStatus(snapshot.Started ? "Match live" : "Room ready");
                return;
            }

            bool isStaleRoom = snapshot.RoomSequence > 0 && snapshot.RoomSequence < _lastRoomSequence;
            bool isStaleState = snapshot.StateVersion > 0 && snapshot.StateVersion < _lastStateVersion;
            if (isStaleRoom && isStaleState)
            {
                return;
            }

            _roomCode = incomingRoomCode;
            _roomId = incomingRoomId;
            _connected = true;
            _lastRoomSequence = Math.Max(_lastRoomSequence, snapshot.RoomSequence);
            _lastStateVersion = Math.Max(_lastStateVersion, snapshot.StateVersion);
            ResetPendingCommandWindow();

            Action<LudoRoomSnapshot> handler = RoomSnapshotReceived;
            if (handler != null)
            {
                handler(snapshot);
            }

            EmitStatus(snapshot.Started ? "Match live" : "Room ready");
        }

        private bool CanUseOnline()
        {
            if (string.IsNullOrWhiteSpace(_serverBaseUrl) || _serverBaseUrl.IndexOf("your-render-service", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EmitError("Set your Render server URL in LudoOnlineService before using online mode.");
                return false;
            }

            return true;
        }

        private void ResetSessionState()
        {
            _roomCode = string.Empty;
            _roomId = string.Empty;
            _playerId = string.Empty;
            _connected = false;
            _isHost = false;
            _lastRoomSequence = 0L;
            _lastChatSequence = 0L;
            _lastTurnSequence = 0L;
            _lastStateVersion = 0L;
            _pendingCommand = PendingCommand.None;
            _pendingCreateRoomRequest = null;
            _pendingJoinRoomRequest = null;
            _pendingStartRequest = null;
            _pendingRecoverRequest = null;
            ResetPendingCommandWindow();
        }

        private void BeginPendingCommandWindow()
        {
            _pendingCommandDeadlineAt = Time.unscaledTime + PendingCommandTimeoutSeconds;
            _slowConnectNoticeAt = Time.unscaledTime + SlowConnectNoticeSeconds;
            _slowConnectNoticeSent = false;
        }

        private void ResetPendingCommandWindow()
        {
            _pendingCommandDeadlineAt = 0f;
            _slowConnectNoticeAt = 0f;
            _slowConnectNoticeSent = false;
        }

        private void UpdatePendingCommandState()
        {
            if (_pendingCommand == PendingCommand.None)
            {
                return;
            }

            if (!_slowConnectNoticeSent && _slowConnectNoticeAt > 0f && Time.unscaledTime >= _slowConnectNoticeAt)
            {
                _slowConnectNoticeSent = true;
                EmitStatus("Waking server...");
            }

            if (_pendingCommandDeadlineAt > 0f && Time.unscaledTime >= _pendingCommandDeadlineAt)
            {
                PendingCommand expiredCommand = _pendingCommand;
                _pendingCommand = PendingCommand.None;
                _pendingCreateRoomRequest = null;
                _pendingJoinRoomRequest = null;
                _pendingStartRequest = null;
                _pendingRecoverRequest = null;
                if (_socketClient != null && !_socketClient.IsConnected)
                {
                    _socketClient.Disconnect();
                }
                ResetPendingCommandWindow();
                EmitError(expiredCommand == PendingCommand.StartMatch
                    ? "Starting the online match took too long. The server may be asleep or unavailable."
                    : "Connecting to the online room took too long. If Render is waking up, wait a few seconds and try again.");
            }
        }

        private static string SanitizePlayerName(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return "Player";
            }

            string trimmed = playerName.Trim();
            return trimmed.Length > 18 ? trimmed.Substring(0, 18) : trimmed;
        }

        private static string[] BuildColorArray(IReadOnlyList<LudoTokenColor> colors)
        {
            if (colors == null || colors.Count == 0)
            {
                return new[] { LudoTokenColor.Red.ToString(), LudoTokenColor.Blue.ToString() };
            }

            string[] values = new string[colors.Count];
            for (int i = 0; i < colors.Count; i++)
            {
                values[i] = colors[i].ToString();
            }

            return values;
        }

        private static T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch
            {
                return null;
            }
        }

        private void EmitStatus(string status)
        {
            Action<string> handler = StatusChanged;
            if (handler != null)
            {
                handler(status ?? string.Empty);
            }
        }

        private void EmitError(string message)
        {
            Action<string> handler = ErrorReceived;
            if (handler != null)
            {
                handler(message ?? "Unknown online error.");
            }
        }

        private void EmitMatchStarted(LudoRoomSnapshot snapshot)
        {
            Action<LudoRoomSnapshot> handler = MatchStartedReceived;
            if (handler != null)
            {
                handler(snapshot);
            }
        }
    }
}
