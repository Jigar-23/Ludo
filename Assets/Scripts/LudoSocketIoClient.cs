using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PremiumLudo
{
    public sealed class LudoSocketIoClient : IDisposable
    {
        [Serializable]
        private sealed class EngineHandshake
        {
            public string sid;
            public int pingInterval;
            public int pingTimeout;
        }

        private const char EngineMessageSeparator = '\u001e';
        private const float InitialReconnectDelay = 1f;
        private const float MaxReconnectDelay = 8f;

        private readonly Queue<Action> _dispatchQueue = new Queue<Action>(32);
        private readonly object _dispatchLock = new object();
        private readonly byte[] _receiveBuffer = new byte[8192];
#if UNITY_WEBGL && !UNITY_EDITOR
        private const string WebGlBridgeObjectName = "__LudoSocketIoBridge";

        [DllImport("__Internal")]
        private static extern void LudoSocketIoBridge_Connect(string objectName, string serverBaseUrl, int allowReconnect);

        [DllImport("__Internal")]
        private static extern void LudoSocketIoBridge_Disconnect();

        [DllImport("__Internal")]
        private static extern void LudoSocketIoBridge_EmitJson(string eventName, string payloadJson);

        private bool _webGlConnecting;
#endif

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _lifecycleTokenSource;
        private Task _connectTask;
        private Task _receiveTask;
        private string _serverBaseUrl = string.Empty;
        private bool _disposed;
        private bool _intentionalDisconnect;
        private bool _shouldReconnect;
        private bool _socketConnected;
        private float _nextReconnectAt;
        private int _reconnectAttempts;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<string, string> EventReceived;
        public event Action<string> ErrorReceived;

        public bool IsConnected
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return _socketConnected;
#else
                return _socketConnected
                    && _webSocket != null
                    && _webSocket.State == WebSocketState.Open;
#endif
            }
        }

        public bool IsConnecting
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return _webGlConnecting;
#else
                return _connectTask != null && !_connectTask.IsCompleted;
#endif
            }
        }

        public bool WantsReconnect
        {
            get { return _shouldReconnect; }
        }

        public void Connect(string serverBaseUrl, bool allowReconnect)
        {
            if (_disposed)
            {
                return;
            }

            _serverBaseUrl = NormalizeServerUrl(serverBaseUrl);
            _intentionalDisconnect = false;
            _shouldReconnect = allowReconnect;
            if (IsConnected || IsConnecting)
            {
                return;
            }

            BeginConnect();
        }

        public void Disconnect()
        {
            if (_disposed)
            {
                return;
            }

            _intentionalDisconnect = true;
            _shouldReconnect = false;
            _socketConnected = false;
            _ = DisposeSocketAsync();
        }

        public void Update()
        {
            PumpDispatchQueue();

            if (_disposed || !_shouldReconnect || IsConnected || IsConnecting || string.IsNullOrEmpty(_serverBaseUrl))
            {
                return;
            }

            if (Time.unscaledTime >= _nextReconnectAt)
            {
                BeginConnect();
            }
        }

        public void EmitJson(string eventName, string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(eventName) || !IsConnected)
            {
                return;
            }

            string json = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
#if UNITY_WEBGL && !UNITY_EDITOR
            LudoSocketIoBridge_EmitJson(eventName, json);
#else
            string packet = "42[\"" + EscapeJsonString(eventName) + "\"," + json + "]";
            _ = SendPacketAsync(packet);
#endif
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _intentionalDisconnect = true;
            _shouldReconnect = false;
            _socketConnected = false;
            _ = DisposeSocketAsync();
        }

        private void BeginConnect()
        {
            if (_disposed || string.IsNullOrEmpty(_serverBaseUrl) || IsConnecting)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            _webGlConnecting = true;
            _socketConnected = false;
            LudoSocketIoBridgeHost.EnsureInitialized(this, WebGlBridgeObjectName);
            LudoSocketIoBridge_Connect(WebGlBridgeObjectName, _serverBaseUrl, _shouldReconnect ? 1 : 0);
#else
            _connectTask = ConnectAsync();
#endif
        }

        private async Task ConnectAsync()
        {
            try
            {
                await DisposeSocketAsync().ConfigureAwait(false);

                _lifecycleTokenSource = new CancellationTokenSource();
                _webSocket = new ClientWebSocket();
                _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20d);

                Uri socketUri = BuildSocketUri(_serverBaseUrl);
                await _webSocket.ConnectAsync(socketUri, _lifecycleTokenSource.Token).ConfigureAwait(false);
                _receiveTask = ReceiveLoopAsync(_lifecycleTokenSource.Token);
            }
            catch (Exception exception)
            {
                Enqueue(() =>
                {
                    Action<string> handler = ErrorReceived;
                    if (handler != null)
                    {
                        handler("Socket connect failed: " + exception.Message);
                    }
                });
                ScheduleReconnect();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            StringBuilder builder = new StringBuilder(1024);

            try
            {
                while (!cancellationToken.IsCancellationRequested && _webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(_receiveBuffer), cancellationToken).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await HandleSocketClosedAsync().ConfigureAwait(false);
                            return;
                        }

                        if (result.Count > 0)
                        {
                            builder.Append(Encoding.UTF8.GetString(_receiveBuffer, 0, result.Count));
                        }
                    }
                    while (!result.EndOfMessage);

                    if (builder.Length == 0)
                    {
                        continue;
                    }

                    string rawPacket = builder.ToString();
                    builder.Length = 0;
                    ProcessIncomingFrame(rawPacket);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Enqueue(() =>
                {
                    Action<string> handler = ErrorReceived;
                    if (handler != null)
                    {
                        handler("Socket receive failed: " + exception.Message);
                    }
                });
            }

            await HandleSocketClosedAsync().ConfigureAwait(false);
        }

        private void ProcessIncomingFrame(string rawFrame)
        {
            if (string.IsNullOrEmpty(rawFrame))
            {
                return;
            }

            string[] packets = rawFrame.Split(EngineMessageSeparator);
            for (int i = 0; i < packets.Length; i++)
            {
                HandlePacket(packets[i]);
            }
        }

        private void HandlePacket(string packet)
        {
            if (string.IsNullOrEmpty(packet))
            {
                return;
            }

            if (packet[0] == '0')
            {
                HandleHandshake(packet);
                return;
            }

            if (packet[0] == '2')
            {
                _ = SendPacketAsync("3");
                return;
            }

            if (packet[0] != '4')
            {
                return;
            }

            if (packet.StartsWith("40", StringComparison.Ordinal))
            {
                _socketConnected = true;
                _reconnectAttempts = 0;
                Enqueue(() =>
                {
                    Action handler = Connected;
                    if (handler != null)
                    {
                        handler();
                    }
                });
                return;
            }

            if (packet.StartsWith("41", StringComparison.Ordinal))
            {
                _socketConnected = false;
                ScheduleReconnect();
                return;
            }

            if (packet.StartsWith("42", StringComparison.Ordinal))
            {
                string eventName;
                string payloadJson;
                if (TryParseEventPacket(packet, out eventName, out payloadJson))
                {
                    Enqueue(() =>
                    {
                        Action<string, string> handler = EventReceived;
                        if (handler != null)
                        {
                            handler(eventName, payloadJson);
                        }
                    });
                }

                return;
            }

            if (packet.StartsWith("44", StringComparison.Ordinal))
            {
                string payload = packet.Length > 2 ? packet.Substring(2) : string.Empty;
                Enqueue(() =>
                {
                    Action<string> handler = ErrorReceived;
                    if (handler != null)
                    {
                        handler("Socket error: " + payload);
                    }
                });
            }
        }

        private void HandleHandshake(string packet)
        {
            if (string.IsNullOrEmpty(packet) || packet.Length <= 1)
            {
                return;
            }

            try
            {
                JsonUtility.FromJson<EngineHandshake>(packet.Substring(1));
            }
            catch
            {
            }

            _ = SendPacketAsync("40");
        }

        private async Task HandleSocketClosedAsync()
        {
            if (_disposed)
            {
                return;
            }

            _socketConnected = false;
            await DisposeSocketAsync().ConfigureAwait(false);
            Enqueue(() =>
            {
                Action handler = Disconnected;
                if (handler != null)
                {
                    handler();
                }
            });
            ScheduleReconnect();
        }

        private void ScheduleReconnect()
        {
            if (_disposed || _intentionalDisconnect || !_shouldReconnect)
            {
                return;
            }

            float delay = Mathf.Min(MaxReconnectDelay, InitialReconnectDelay * Mathf.Pow(1.65f, _reconnectAttempts));
            _reconnectAttempts += 1;
            _nextReconnectAt = Time.unscaledTime + delay;
        }

        private async Task SendPacketAsync(string packet)
        {
            if (string.IsNullOrEmpty(packet) || _webSocket == null || _webSocket.State != WebSocketState.Open || _lifecycleTokenSource == null)
            {
                return;
            }

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(packet);
                await _webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, _lifecycleTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Enqueue(() =>
                {
                    Action<string> handler = ErrorReceived;
                    if (handler != null)
                    {
                        handler("Socket send failed: " + exception.Message);
                    }
                });
                ScheduleReconnect();
            }
        }

        private async Task DisposeSocketAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _webGlConnecting = false;
            _socketConnected = false;
            try
            {
                LudoSocketIoBridge_Disconnect();
            }
            catch
            {
            }

            await Task.CompletedTask;
            return;
#else
            CancellationTokenSource tokenSource = _lifecycleTokenSource;
            _lifecycleTokenSource = null;

            if (tokenSource != null)
            {
                try
                {
                    tokenSource.Cancel();
                }
                catch
                {
                }
            }

            ClientWebSocket socket = _webSocket;
            _webSocket = null;

            if (socket != null)
            {
                try
                {
                    if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch
                {
                }
                finally
                {
                    socket.Dispose();
                }
            }

            if (tokenSource != null)
            {
                tokenSource.Dispose();
            }
#endif
        }

        private void PumpDispatchQueue()
        {
            while (true)
            {
                Action action = null;
                lock (_dispatchLock)
                {
                    if (_dispatchQueue.Count > 0)
                    {
                        action = _dispatchQueue.Dequeue();
                    }
                }

                if (action == null)
                {
                    break;
                }

                action();
            }
        }

        private void Enqueue(Action action)
        {
            if (action == null)
            {
                return;
            }

            lock (_dispatchLock)
            {
                _dispatchQueue.Enqueue(action);
            }
        }

        private static string NormalizeServerUrl(string serverBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(serverBaseUrl))
            {
                return string.Empty;
            }

            string trimmed = serverBaseUrl.Trim().TrimEnd('/');
            Uri uri;
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out uri))
            {
                return uri.GetLeftPart(UriPartial.Authority);
            }

            return trimmed;
        }

        private static Uri BuildSocketUri(string serverBaseUrl)
        {
            Uri httpUri = new Uri(serverBaseUrl);
            string scheme = string.Equals(httpUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
            string uri = scheme + "://" + httpUri.Authority + "/socket.io/?EIO=4&transport=websocket";
            return new Uri(uri);
        }

        private static bool TryParseEventPacket(string packet, out string eventName, out string payloadJson)
        {
            eventName = string.Empty;
            payloadJson = "{}";
            if (string.IsNullOrEmpty(packet) || packet.Length < 6)
            {
                return false;
            }

            int startQuote = packet.IndexOf('"', 2);
            if (startQuote < 0)
            {
                return false;
            }

            int endQuote = packet.IndexOf('"', startQuote + 1);
            if (endQuote <= startQuote)
            {
                return false;
            }

            eventName = packet.Substring(startQuote + 1, endQuote - startQuote - 1);

            int commaIndex = packet.IndexOf(',', endQuote + 1);
            if (commaIndex < 0)
            {
                payloadJson = "{}";
                return true;
            }

            int closingBracketIndex = packet.LastIndexOf(']');
            if (closingBracketIndex <= commaIndex)
            {
                return false;
            }

            payloadJson = packet.Substring(commaIndex + 1, closingBracketIndex - commaIndex - 1);
            return true;
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        internal void HandleWebGlConnected()
        {
            _webGlConnecting = false;
            _socketConnected = true;
            _reconnectAttempts = 0;
            Enqueue(() =>
            {
                Action handler = Connected;
                if (handler != null)
                {
                    handler();
                }
            });
        }

        internal void HandleWebGlDisconnected(string reason)
        {
            _webGlConnecting = false;
            _socketConnected = false;
            Enqueue(() =>
            {
                Action handler = Disconnected;
                if (handler != null)
                {
                    handler();
                }
            });
        }

        internal void HandleWebGlEvent(string eventName, string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return;
            }

            Enqueue(() =>
            {
                Action<string, string> handler = EventReceived;
                if (handler != null)
                {
                    handler(eventName, string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
                }
            });
        }

        internal void HandleWebGlError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Enqueue(() =>
            {
                Action<string> handler = ErrorReceived;
                if (handler != null)
                {
                    handler(message);
                }
            });
        }
#endif
    }
}
