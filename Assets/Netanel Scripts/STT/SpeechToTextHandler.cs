using UnityEngine;
using XRMultiplayer; // For XRINetworkGameManager + MessageManager

public class SpeechToTextHandler : MonoBehaviour
{
    [Header("Component References")]
    public MicRecorder micRecorder;
    public CloudWhisperManager cloudWhisper;

    [Header("Network Integration")]
    public MessageManager messageManager;

    [Header("Fallback Settings")]
    public Color nameColor = Color.cyan;

    void Start()
    {
        if (micRecorder != null) micRecorder.OnClipReady += HandleAutoClip;
    }

    private void HandleAutoClip(AudioClip clip)
    {
        if (clip != null && cloudWhisper != null)
        {
            cloudWhisper.OnTranscriptionComplete += AddToChatLog;
            cloudWhisper.SendAudioToCloud(clip);
        }
    }

    public void AddToChatLog(string transcribedText)
    {
        if (cloudWhisper != null) cloudWhisper.OnTranscriptionComplete -= AddToChatLog;

        if (string.IsNullOrWhiteSpace(transcribedText)) return;
        if (messageManager == null) return;

        // Read the local player's name from XRINetworkGameManager.LocalPlayerName.
        // This is the source of truth used by the entire XR Multiplayer Sample
        // (set by the offline profile setup screen — "WATERVISION Set up your profile").
        string realName = XRINetworkGameManager.LocalPlayerName.Value;
        if (string.IsNullOrEmpty(realName))
            realName = "Player";

        messageManager.BroadcastMessage(transcribedText, realName, nameColor);
    }

    private void OnDestroy()
    {
        if (micRecorder != null) micRecorder.OnClipReady -= HandleAutoClip;
        if (cloudWhisper != null) cloudWhisper.OnTranscriptionComplete -= AddToChatLog;
    }
}