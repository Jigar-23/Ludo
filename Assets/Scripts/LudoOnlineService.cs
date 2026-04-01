using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PremiumLudo
{
    public sealed class LudoOnlineService : MonoBehaviour
    {
        private const float PollIntervalSeconds = 0.45f;
        private const string DefaultServerBaseUrl = "https://ludo-server-vg5b.onrender.com/api/ludo";

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
        private sealed class StartRoomRequest
        {
            public string RoomCode;
        }

        [Serializable]
        private sealed class ChatRequest
        {
            public string Sender;
            public string Message;
            public string Color;
        }

        [Serializable]
        private sealed class LeaveRoomRequest
        {
            public string PlayerName;
            public string Color;
        }

        [Serializable]
        private sealed class TurnRequest
        {
            public string Color;
            public int Roll;
            public int TokenIndex;
            public bool NoMove;
        }

        public event Action<LudoRoomSnapshot> RoomSnapshotReceived;
        public event Action<LudoChatMessage> ChatMessageReceived;
        public event Action<LudoTurnActionMessage> TurnActionReceived;
        public event Action<string> StatusChanged;
        public event Action<string> ErrorReceived;

        private string _serverBaseUrl = DefaultServerBaseUrl;
        private string _roomCode = string.Empty;
        private string _localPlayerName = "Player";
        private string _localColor = string.Empty;
        private bool _isHost;
        private bool _connected;
        private bool _pollInFlight;
        private float _nextPollAt;
        private long _lastRoomSequence;
        private long _lastChatSequence;
        private long _lastTurnSequence;

        public string RoomCode
        {
            get { return _roomCode; }
        }

        public bool IsConnected
        {
            get { return _connected; }
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

        public string ServerBaseUrl
        {
            get { return _serverBaseUrl; }
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

            ResetState();

            CreateRoomRequest request = new CreateRoomRequest
            {
                PlayerName = SanitizePlayerName(playerName),
                PlayerCount = Mathf.Clamp(playerCount, 2, 4),
                LocalColor = localColor.ToString(),
                ActiveColors = BuildColorArray(activeColors),
            };

            _localPlayerName = request.PlayerName;
            _localColor = request.LocalColor;
            _isHost = true;
            EmitStatus("Creating room...");
            StartCoroutine(SendJsonRequest<LudoRoomOperationResponse>(
                _serverBaseUrl + "/rooms/create",
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(request),
                HandleRoomOperationSuccess,
                HandleRequestError));
        }

        public void JoinRoom(string roomCode, string playerName, LudoTokenColor preferredColor)
        {
            if (!CanUseOnline())
            {
                return;
            }

            ResetState();

            JoinRoomRequest request = new JoinRoomRequest
            {
                RoomCode = string.IsNullOrWhiteSpace(roomCode) ? string.Empty : roomCode.Trim().ToUpperInvariant(),
                PlayerName = SanitizePlayerName(playerName),
                PreferredColor = preferredColor.ToString(),
            };

            if (string.IsNullOrEmpty(request.RoomCode))
            {
                EmitError("Enter a room code to join.");
                return;
            }

            _localPlayerName = request.PlayerName;
            _localColor = request.PreferredColor;
            _isHost = false;
            EmitStatus("Joining room " + request.RoomCode + "...");
            StartCoroutine(SendJsonRequest<LudoRoomOperationResponse>(
                _serverBaseUrl + "/rooms/join",
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(request),
                HandleRoomOperationSuccess,
                HandleRequestError));
        }

        public void StartMatch()
        {
            if (!CanUseOnline() || string.IsNullOrEmpty(_roomCode))
            {
                return;
            }

            EmitStatus("Starting match...");
            StartRoomRequest request = new StartRoomRequest
            {
                RoomCode = _roomCode,
            };

            StartCoroutine(SendJsonRequest<LudoRoomOperationResponse>(
                _serverBaseUrl + "/rooms/" + Escape(_roomCode) + "/start",
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(request),
                HandleRoomOperationSuccess,
                HandleRequestError));
        }

        public void LeaveRoom()
        {
            if (string.IsNullOrEmpty(_roomCode))
            {
                ResetState();
                return;
            }

            if (!CanUseOnline())
            {
                ResetState();
                return;
            }

            LeaveRoomRequest request = new LeaveRoomRequest
            {
                PlayerName = _localPlayerName,
                Color = _localColor,
            };

            StartCoroutine(SendJsonRequest<LudoRoomOperationResponse>(
                _serverBaseUrl + "/rooms/" + Escape(_roomCode) + "/leave",
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(request),
                HandleLeaveSuccess,
                _ => ResetState()));
        }

        public void SendChat(string sender, string message, LudoTokenColor color)
        {
            if (!CanUseOnline() || string.IsNullOrEmpty(_roomCode) || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            ChatRequest request = new ChatRequest
            {
                Sender = string.IsNullOrWhiteSpace(sender) ? _localPlayerName : sender.Trim(),
                Message = message.Trim(),
                Color = color.ToString(),
            };

            StartCoroutine(SendJsonRequest<LudoRoomOperationResponse>(
                _serverBaseUrl + "/rooms/" + Escape(_roomCode) + "/chat",
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(request),
                HandleRoomOperationSuccessSilently,
                HandleRequestError));
        }

        public void SendTurnAction(LudoTurnActionMessage action)
        {
            if (!CanUseOnline() || string.IsNullOrEmpty(_roomCode) || action == null)
            {
                return;
            }

            TurnRequest request = new TurnRequest
            {
                Color = action.Color,
                Roll = action.Roll,
                TokenIndex = action.TokenIndex,
                NoMove = action.NoMove,
            };

            StartCoroutine(SendJsonRequest<LudoRoomOperationResponse>(
                _serverBaseUrl + "/rooms/" + Escape(_roomCode) + "/turn",
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(request),
                HandleRoomOperationSuccessSilently,
                HandleRequestError));
        }

        private void Update()
        {
            if (!_connected || _pollInFlight || string.IsNullOrEmpty(_roomCode) || Time.unscaledTime < _nextPollAt)
            {
                return;
            }

            StartCoroutine(PollRoomState());
        }

        private IEnumerator PollRoomState()
        {
            _pollInFlight = true;
            string url = string.Format(
                "{0}/rooms/{1}/poll?roomSequence={2}&chatSequence={3}&turnSequence={4}",
                _serverBaseUrl,
                Escape(_roomCode),
                _lastRoomSequence,
                _lastChatSequence,
                _lastTurnSequence);

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();
                _pollInFlight = false;
                _nextPollAt = Time.unscaledTime + PollIntervalSeconds;

                if (!RequestSucceeded(request))
                {
                    EmitError(GetRequestError(request));
                    yield break;
                }

                LudoRoomPollResponse response = Deserialize<LudoRoomPollResponse>(request.downloadHandler.text);
                if (response == null)
                {
                    EmitError("The room poll response could not be read.");
                    yield break;
                }

                if (!response.Success)
                {
                    EmitError(string.IsNullOrEmpty(response.Error) ? "Polling the room failed." : response.Error);
                    yield break;
                }

                ApplySnapshot(response.Snapshot);
                DispatchChatMessages(response.ChatMessages);
                DispatchTurnActions(response.TurnActions);
            }
        }

        private void HandleRoomOperationSuccess(LudoRoomOperationResponse response)
        {
            if (response == null)
            {
                EmitError("The server returned an empty response.");
                return;
            }

            if (!response.Success)
            {
                EmitError(string.IsNullOrEmpty(response.Error) ? "The server rejected the request." : response.Error);
                return;
            }

            if (!string.IsNullOrWhiteSpace(response.AssignedColor))
            {
                _localColor = response.AssignedColor;
            }

            ApplySnapshot(response.Snapshot);
        }

        private void HandleRoomOperationSuccessSilently(LudoRoomOperationResponse response)
        {
            if (response != null && response.Success)
            {
                if (!string.IsNullOrWhiteSpace(response.AssignedColor))
                {
                    _localColor = response.AssignedColor;
                }

                ApplySnapshot(response.Snapshot);
            }
        }

        private void HandleLeaveSuccess(LudoRoomOperationResponse response)
        {
            ResetState();
        }

        private void ApplySnapshot(LudoRoomSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _roomCode = string.IsNullOrWhiteSpace(snapshot.RoomCode) ? _roomCode : snapshot.RoomCode.Trim().ToUpperInvariant();
            _connected = true;
            _nextPollAt = Time.unscaledTime + PollIntervalSeconds;

            if (snapshot.RoomSequence > _lastRoomSequence)
            {
                _lastRoomSequence = snapshot.RoomSequence;
            }

            Action<LudoRoomSnapshot> handler = RoomSnapshotReceived;
            if (handler != null)
            {
                handler(snapshot);
            }

            EmitStatus(snapshot.Started ? "Match live" : "Room ready");
        }

        private void DispatchChatMessages(LudoChatMessage[] messages)
        {
            if (messages == null)
            {
                return;
            }

            Action<LudoChatMessage> handler = ChatMessageReceived;
            for (int i = 0; i < messages.Length; i++)
            {
                if (messages[i] == null)
                {
                    continue;
                }

                _lastChatSequence = Math.Max(_lastChatSequence, messages[i].Sequence);
                if (handler != null)
                {
                    handler(messages[i]);
                }
            }
        }

        private void DispatchTurnActions(LudoTurnActionMessage[] actions)
        {
            if (actions == null)
            {
                return;
            }

            Action<LudoTurnActionMessage> handler = TurnActionReceived;
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] == null)
                {
                    continue;
                }

                _lastTurnSequence = Math.Max(_lastTurnSequence, actions[i].Sequence);
                if (handler != null)
                {
                    handler(actions[i]);
                }
            }
        }

        private IEnumerator SendJsonRequest<TResponse>(string url, string method, string jsonBody, Action<TResponse> onSuccess, Action<string> onError) where TResponse : class
        {
            byte[] body = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(jsonBody) ? "{}" : jsonBody);
            using (UnityWebRequest request = new UnityWebRequest(url, method))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 15;

                yield return request.SendWebRequest();

                if (!RequestSucceeded(request))
                {
                    if (onError != null)
                    {
                        onError(GetRequestError(request));
                    }

                    yield break;
                }

                TResponse response = Deserialize<TResponse>(request.downloadHandler.text);
                if (response == null)
                {
                    if (onError != null)
                    {
                        onError("The server response could not be read.");
                    }

                    yield break;
                }

                if (onSuccess != null)
                {
                    onSuccess(response);
                }
            }
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

        private void HandleRequestError(string message)
        {
            EmitError(message);
        }

        private void ResetState()
        {
            _roomCode = string.Empty;
            _localPlayerName = "Player";
            _localColor = string.Empty;
            _isHost = false;
            _connected = false;
            _pollInFlight = false;
            _lastRoomSequence = 0L;
            _lastChatSequence = 0L;
            _lastTurnSequence = 0L;
            _nextPollAt = 0f;
            EmitStatus("Offline");
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
                return new[] { LudoTokenColor.Blue.ToString(), LudoTokenColor.Red.ToString() };
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

        private static bool RequestSucceeded(UnityWebRequest request)
        {
            return request != null && request.result != UnityWebRequest.Result.ConnectionError && request.result != UnityWebRequest.Result.ProtocolError && request.result != UnityWebRequest.Result.DataProcessingError;
        }

        private static string GetRequestError(UnityWebRequest request)
        {
            if (request == null)
            {
                return "The online request failed.";
            }

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            if (!string.IsNullOrWhiteSpace(responseText))
            {
                return responseText;
            }

            return string.IsNullOrWhiteSpace(request.error) ? "The online request failed." : request.error;
        }

        private static string Escape(string value)
        {
            return UnityWebRequest.EscapeURL(value ?? string.Empty);
        }

        private void EmitStatus(string status)
        {
            Action<string> handler = StatusChanged;
            if (handler != null)
            {
                handler(status);
            }
        }

        private void EmitError(string message)
        {
            Action<string> handler = ErrorReceived;
            if (handler != null)
            {
                handler(message);
            }

            EmitStatus(message);
        }
    }
}
