using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace PremiumLudo
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [Preserve]
    public sealed class LudoSocketIoBridgeHost : MonoBehaviour
    {
        [Serializable]
        private sealed class SocketEventEnvelope
        {
            public string EventName;
            public string PayloadJson;
        }

        private static LudoSocketIoBridgeHost s_Instance;
        private LudoSocketIoClient _client;

        public static void EnsureInitialized(LudoSocketIoClient client, string objectName)
        {
            if (s_Instance == null)
            {
                GameObject host = new GameObject(string.IsNullOrWhiteSpace(objectName) ? "__LudoSocketIoBridge" : objectName);
                DontDestroyOnLoad(host);
                s_Instance = host.AddComponent<LudoSocketIoBridgeHost>();
            }
            else if (!string.IsNullOrWhiteSpace(objectName) && !string.Equals(s_Instance.gameObject.name, objectName, StringComparison.Ordinal))
            {
                s_Instance.gameObject.name = objectName;
            }

            s_Instance._client = client;
        }

        [Preserve]
        public void OnSocketConnected(string _)
        {
            if (_client != null)
            {
                _client.HandleWebGlConnected();
            }
        }

        [Preserve]
        public void OnSocketDisconnected(string reason)
        {
            if (_client != null)
            {
                _client.HandleWebGlDisconnected(reason);
            }
        }

        [Preserve]
        public void OnSocketError(string message)
        {
            if (_client != null)
            {
                _client.HandleWebGlError(message);
            }
        }

        [Preserve]
        public void OnSocketEvent(string payload)
        {
            if (_client == null || string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            SocketEventEnvelope envelope = null;
            try
            {
                envelope = JsonUtility.FromJson<SocketEventEnvelope>(payload);
            }
            catch
            {
            }

            if (envelope == null || string.IsNullOrWhiteSpace(envelope.EventName))
            {
                return;
            }

            _client.HandleWebGlEvent(envelope.EventName, envelope.PayloadJson);
        }
    }
#else
    public static class LudoSocketIoBridgeHost
    {
        public static void EnsureInitialized(LudoSocketIoClient client, string objectName)
        {
        }
    }
#endif
}
