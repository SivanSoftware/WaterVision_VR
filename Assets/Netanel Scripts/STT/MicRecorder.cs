using UnityEngine;
using System;

public class MicRecorder : MonoBehaviour
{
    public static MicRecorder Instance { get; private set; }

    [Header("Voice Detection Settings")]
    public float sensitivity = 0.02f;
    public float silenceDuration = 2.0f;
    public int bufferLengthSec = 300;
    public int sampleRate = 16000;

    [Header("Events")]
    public Action OnSpeechStart;
    public Action<float> OnSpeechEnd; // duration seconds
    public Action<AudioClip> OnClipReady;

    /// <summary>
    /// When true, the speech detection pipeline is paused.
    /// The microphone keeps running (so unmute is instant), but no clips
    /// are sent to Whisper. Vivox is independent and NOT affected by this flag.
    /// </summary>
    public bool IsMuted { get; private set; } = false;

    private string selectedDevice;
    private AudioClip recordingBuffer;
    private int lastReadPos = 0;
    private bool isSpeechActive = false;
    private float silenceTimer = 0f;
    private int speechStartSample = 0;

    void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[MicRecorder] ⚠️ Multiple instances detected! Destroying duplicate on '{gameObject.name}'. Active instance is on '{Instance.gameObject.name}'");
            Destroy(this);
            return;
        }

        Instance = this;
        Debug.Log($"[MicRecorder] ✅ Singleton instance set on '{gameObject.name}', InstanceID={GetInstanceID()}");
    }

    void Start()
    {
        Debug.Log("[DEBUG] MicRecorder: Start() called.");
        InitializeMicrophone();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Debug.Log("[MicRecorder] Singleton instance cleared.");
        }
    }

    /// <summary>
    /// Mute / unmute the STT pipeline. Call this from the Mute button alongside Vivox mute.
    /// While muted, no speech is detected and no clips are sent to Whisper.
    /// </summary>
    public void SetMuted(bool muted)
    {
        if (IsMuted == muted) return;
        IsMuted = muted;
        Debug.Log($"[MicRecorder] 🔇 STT pipeline muted = {muted}");

        // If muting mid-speech, abort the current detection silently (no event, no clip).
        if (muted && isSpeechActive)
        {
            isSpeechActive = false;
            silenceTimer = 0f;
            Debug.Log("[MicRecorder] Aborted active speech detection due to mute.");
        }
    }

    void InitializeMicrophone()
    {
        if (Microphone.devices.Length > 0)
        {
            selectedDevice = Microphone.devices[0];
            Debug.Log($"[DEBUG] MicRecorder: Found {Microphone.devices.Length} devices.");
            Debug.Log($"[DEBUG] MicRecorder: Selected Device: '{selectedDevice}'");

            try
            {
                recordingBuffer = Microphone.Start(selectedDevice, true, bufferLengthSec, sampleRate);
                Debug.Log($"[DEBUG] MicRecorder: Microphone.Start() success! IsRecording? {Microphone.IsRecording(selectedDevice)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DEBUG] MicRecorder: FAILED to start mic. Error: {e.Message}");
            }
        }
        else
        {
            Debug.LogError("[DEBUG] MicRecorder: FATAL - No Microphone devices found!");
        }
    }

    void Update()
    {
        if (recordingBuffer == null) return;
        if (string.IsNullOrEmpty(selectedDevice)) return;

        if (!Microphone.IsRecording(selectedDevice))
        {
            Debug.LogWarning("[DEBUG] MicRecorder: Mic stopped unexpectedly! Attempting restart...");
            InitializeMicrophone();
            return;
        }

        // Skip voice detection while muted, but keep the mic running so unmute is instant.
        if (IsMuted) return;

        int currentPos = Microphone.GetPosition(selectedDevice);
        if (currentPos < lastReadPos) lastReadPos = 0;

        float currentVolume = GetLevelMax();

        if (currentVolume > 0.02f)
        {
            if (Time.frameCount % 60 == 0)
                Debug.Log($"[DEBUG] MicRecorder: Hearing Audio... Vol: {currentVolume:F4}");
        }

        if (currentVolume > sensitivity)
        {
            silenceTimer = 0f;
            if (!isSpeechActive)
            {
                isSpeechActive = true;
                speechStartSample = Mathf.Max(0, currentPos - (sampleRate / 2));

                int subscribers = OnSpeechStart?.GetInvocationList().Length ?? 0;
                Debug.Log($"[DEBUG] MicRecorder: 🗣️ START TALKING (sample {speechStartSample}). OnSpeechStart subscribers: {subscribers}");
                OnSpeechStart?.Invoke();
            }
        }
        else if (isSpeechActive)
        {
            silenceTimer += Time.deltaTime;
            if (silenceTimer > silenceDuration)
            {
                isSpeechActive = false;

                float durationSec = Mathf.Max(0f, (currentPos - speechStartSample) / (float)sampleRate);

                int subscribers = OnSpeechEnd?.GetInvocationList().Length ?? 0;
                Debug.Log($"[DEBUG] MicRecorder: 🤫 END TALKING. End sample: {currentPos}, Duration: {durationSec:F2}s. OnSpeechEnd subscribers: {subscribers}");

                if (durationSec >= 0.15f)
                    OnSpeechEnd?.Invoke(durationSec);

                ProcessClip(speechStartSample, currentPos);
            }
        }
    }

    private void ProcessClip(int startSample, int endSample)
    {
        int length = endSample - startSample;
        if (length <= 0)
        {
            Debug.LogWarning("[DEBUG] MicRecorder: Recorded clip length was 0 or negative. Ignoring.");
            return;
        }

        float[] clipData = new float[length];
        recordingBuffer.GetData(clipData, startSample);

        AudioClip phraseClip = AudioClip.Create("Phrase", length, 1, sampleRate, false);
        phraseClip.SetData(clipData, 0);

        if (OnClipReady != null)
        {
            int subscribers = OnClipReady.GetInvocationList().Length;
            Debug.Log($"[DEBUG] MicRecorder: Invoking OnClipReady event. Subscribers: {subscribers}");
            OnClipReady.Invoke(phraseClip);
        }
        else
        {
            Debug.LogWarning("[DEBUG] MicRecorder: Clip ready, but nobody is listening.");
        }
    }

    float GetLevelMax()
    {
        int dec = 128;
        float[] waveData = new float[dec];
        int micPos = Microphone.GetPosition(selectedDevice) - (dec + 1);
        if (micPos < 0) return 0;

        recordingBuffer.GetData(waveData, micPos);

        float max = 0;
        for (int i = 0; i < dec; i++)
        {
            float wavePeak = waveData[i] * waveData[i];
            if (max < wavePeak) max = wavePeak;
        }
        return Mathf.Sqrt(max);
    }
}