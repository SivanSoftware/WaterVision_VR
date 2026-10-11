using UnityEngine;
using Unity.Netcode;
using TMPro; 
using UnityEngine.UI; 

namespace XRMultiplayer
{
    public class MessageManager : NetworkBehaviour
    {
        [Header("UI References")]
        [SerializeField] TMP_Text outputText; 
        [SerializeField] ScrollRect scrollRect; 

        private NetworkVariable<NetworkString> fullChatHistory = new NetworkVariable<NetworkString>(
            new NetworkString { },
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// Fired on EVERY client (incl. host) once per transcribed message, carrying (text, senderName,
        /// nameColor). Used by the Sign-Language bubble; independent of the chat-history UI above.
        /// </summary>
        public static event System.Action<string, string, Color> OnMessageReceived;

        public override void OnNetworkSpawn()
        {
            UpdateChatUI(fullChatHistory.Value);
            fullChatHistory.OnValueChanged += OnHistoryChanged;
        }

        public override void OnNetworkDespawn()
        {
            fullChatHistory.OnValueChanged -= OnHistoryChanged;
        }

        public void BroadcastMessage(string text, string playerName, Color nameColor)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            SubmitMessageServerRpc(text, playerName, nameColor); 
        }

        [Rpc(SendTo.Server)]
        private void SubmitMessageServerRpc(string text, string playerName, Color color)
        {
            // --- תיקון השגיאה כאן ---
            if (ServerLogManager.Instance != null)
            {
                // שולחים רק 3 פרמטרים (שם, טקסט, צבע) כי זה מה שה-ServerLogManager שלך יודע לקבל
                ServerLogManager.Instance.LogChat(playerName, text, color);
            }
            
            // עדכון הצ'אט הוויזואלי במשחק
            string colorHex = ColorUtility.ToHtmlStringRGB(color);
            string formattedLine = $"<b><color=#{colorHex}>{playerName}:</color></b> {text}\n";

            string currentText = fullChatHistory.Value.ToString();
            string updatedText = currentText + formattedLine;

            if (updatedText.Length > 2000)
            {
                updatedText = updatedText.Substring(updatedText.Length - 2000);
            }

            fullChatHistory.Value = updatedText;

            // Fan out the single latest message to every client for the sign-language bubble.
            ReceiveMessageClientRpc(text, playerName, color);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ReceiveMessageClientRpc(string text, string playerName, Color color)
        {
            OnMessageReceived?.Invoke(text, playerName, color);
        }

        private void OnHistoryChanged(NetworkString oldVal, NetworkString newVal)
        {
            UpdateChatUI(newVal);
        }

        private void UpdateChatUI(string fullText)
        {
            if (outputText != null)
            {
                outputText.text = fullText;
                Canvas.ForceUpdateCanvases();
                if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }

    public struct NetworkString : INetworkSerializable, System.IEquatable<NetworkString>
    {
        private string _info;
        public static implicit operator string(NetworkString s) => s._info;
        public static implicit operator NetworkString(string s) => new NetworkString { _info = s };
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsReader) { var reader = serializer.GetFastBufferReader(); reader.ReadValueSafe(out _info); }
            else { var writer = serializer.GetFastBufferWriter(); writer.WriteValueSafe(_info ?? string.Empty); }
        }
        public bool Equals(NetworkString other) => string.Equals(_info, other._info);
        public override string ToString() => _info ?? string.Empty;
    }
}