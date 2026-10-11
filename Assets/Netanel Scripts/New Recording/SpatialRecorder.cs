using System;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using WaterVision.Recording;
using XRMultiplayer;

/// <summary>
/// Captures a multi-user VR session: head + both hands for every connected NGO player,
/// plus discrete world events. Sampled at a fixed rate; output is a v2 RecordingSession
/// JSON in Application.persistentDataPath.
///
/// Replaces the old tag-scan recorder. Discovery is driven by XRINetworkGameManager so
/// mid-session join/leave is handled automatically (joinedAtTimestamp is stored per
/// participant for accurate replay timing).
///
/// Phase 2: now a NetworkBehaviour. The <see cref="recorderState"/> NetworkVariable
/// replicates "who is currently recording" to every client so the dashboard can show a
/// "Recording by &lt;user&gt;" banner and hide the Stop button for non-recorders. Recording
/// remains a per-client write — only the recorder serializes frames to disk locally.
/// </summary>
public class SpatialRecorder : NetworkBehaviour
{
    public static SpatialRecorder Instance;

    [Header("Recording Settings")]
    public float recordRate = 0.033f; // 30Hz
    public bool isRecording = false;

    [Header("Debug")]
    [SerializeField] private float currentDuration;
    [SerializeField] private int framesRecorded;
    [SerializeField] private int eventsRecorded;
    [SerializeField] private int participantCount;

    /// <summary>
    /// Replicated "is anyone in the session recording" state. Owner-write under
    /// Distributed Authority: the recording client takes ownership of this
    /// NetworkObject before flipping the flag.
    /// </summary>
    public readonly NetworkVariable<RecorderStateData> recorderState =
        new NetworkVariable<RecorderStateData>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

    /// <summary>Fires whenever <see cref="recorderState"/> changes. UI subscribes here.</summary>
    public event System.Action<RecorderStateData, RecorderStateData> OnRecorderStateChanged;

    /// <summary>True if any client in the session is currently recording.</summary>
    public bool IsAnyoneRecording => recorderState.Value.isRecording;

    /// <summary>Client ID of the active recorder, or 0 if none.</summary>
    public ulong CurrentRecorderClientId => recorderState.Value.recorderClientId;

    /// <summary>True iff the local client is the one writing frames right now.</summary>
    public bool LocalIsRecorder =>
        recorderState.Value.isRecording &&
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsConnectedClient &&
        recorderState.Value.recorderClientId == NetworkManager.Singleton.LocalClientId;

    private RecordingSession currentSession;
    private float startTime;
    private float nextSampleTime;

    // Per-participant tracking. Parallel arrays keyed by `participantSlots` index for cache locality.
    private readonly List<TrackedParticipant> tracked = new List<TrackedParticipant>();
    private readonly Dictionary<ulong, int> clientIdToSlot = new Dictionary<ulong, int>();

    private bool subscribedToPlayerEvents = false;

    private struct TrackedParticipant
    {
        public ulong clientId;
        public NetworkObject networkObject;
        public string networkIdString; // cached: clientId.ToString()
        public bool transformsBound;   // false until PlayerTransformProvider returns true at least once
        public Transform head;
        public Transform leftHand;
        public Transform rightHand;
    }

    private void Awake()
    {
        // Singleton without DDOL: this object lives as part of the scene (it's a
        // NetworkObject, which precludes DontDestroyOnLoad). If the scene reloads, a
        // fresh recorder takes over.
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    public override void OnNetworkSpawn()
    {
        recorderState.OnValueChanged += HandleRecorderStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        recorderState.OnValueChanged -= HandleRecorderStateChanged;
    }

    private void OnDestroy()
    {
        UnsubscribeFromPlayerEvents();
        if (Instance == this) Instance = null;
    }

    private void HandleRecorderStateChanged(RecorderStateData previous, RecorderStateData current)
    {
        OnRecorderStateChanged?.Invoke(previous, current);
    }

    private void Update()
    {
        if (!isRecording) return;

        // Late-bind any participants whose head/hands weren't ready at registration time.
        for (int i = 0; i < tracked.Count; i++)
        {
            if (!tracked[i].transformsBound && tracked[i].networkObject != null)
                TryBindTransforms(i);
        }

        float elapsed = Time.unscaledTime - startTime;
        currentDuration = elapsed;

        if (elapsed >= nextSampleTime)
        {
            CaptureFrame(elapsed);
            nextSampleTime += recordRate;
            // After a frame hitch, snap forward instead of bursting many catch-up samples.
            if (nextSampleTime < elapsed) nextSampleTime = elapsed + recordRate;
        }
    }

    // ── Control ──────────────────────────────────────────────────────────

    public void StartRecording()
    {
        try
        {
            if (isRecording)
            {
                Debug.LogWarning("[SpatialRecorder] Already recording locally.");
                return;
            }

            // Refuse to start if someone else is already recording cluster-wide.
            if (IsSpawned && recorderState.Value.isRecording &&
                recorderState.Value.recorderClientId != GetLocalClientIdOrZero())
            {
                Debug.LogWarning($"[SpatialRecorder] Cannot start: client {recorderState.Value.recorderClientId} is already recording.");
                return;
            }

            ulong localId = GetLocalClientIdOrZero();

            currentSession = new RecordingSession
            {
                version = 2,
                sessionID = "Session_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss"),
                date = DateTime.Now.ToString(),
                recorderClientId = localId
            };

            tracked.Clear();
            clientIdToSlot.Clear();

            SnapshotInitialParticipants();
            SubscribeToPlayerEvents();

            startTime = Time.unscaledTime;
            nextSampleTime = 0f;
            framesRecorded = 0;
            eventsRecorded = 0;
            participantCount = tracked.Count;
            isRecording = true;

            PublishRecorderState(true, localId);

            Debug.Log($"[SpatialRecorder] Recording started: {currentSession.sessionID} with {tracked.Count} initial participant(s).");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SpatialRecorder] StartRecording failed: {e.Message}\n{e.StackTrace}");
            isRecording = false;
        }
    }

    public void StopRecording()
    {
        if (!isRecording) return;

        isRecording = false;
        UnsubscribeFromPlayerEvents();

        PublishRecorderState(false, 0UL);

        currentSession.totalDuration = currentDuration;
        SaveToFile();
    }

    private static ulong GetLocalClientIdOrZero()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient
            ? NetworkManager.Singleton.LocalClientId
            : 0UL;
    }

    /// <summary>
    /// Take ownership (Distributed Authority — owner-write NetworkVariable) and push
    /// the new recorder state. No-op when the recorder isn't network-spawned (offline
    /// editor recording still works locally).
    /// </summary>
    private void PublishRecorderState(bool nowRecording, ulong recorderId)
    {
        if (!IsSpawned) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient) return;

        if (NetworkObject != null && !IsOwner)
        {
            try { NetworkObject.ChangeOwnership(NetworkManager.Singleton.LocalClientId); }
            catch (Exception e) { Debug.LogWarning($"[SpatialRecorder] ChangeOwnership failed: {e.Message}"); }
        }

        if (!IsOwner) return; // Couldn't take ownership — bail rather than throw.

        recorderState.Value = new RecorderStateData
        {
            isRecording = nowRecording,
            recorderClientId = recorderId,
            sessionStartServerTime = nowRecording && NetworkManager.Singleton.NetworkTimeSystem != null
                ? NetworkManager.Singleton.ServerTime.Time
                : 0.0
        };
    }

    public float GetStartTime() => startTime;

    // ── Participant tracking ─────────────────────────────────────────────

    private void SnapshotInitialParticipants()
    {
        if (XRINetworkGameManager.Instance == null)
        {
            Debug.LogWarning("[SpatialRecorder] XRINetworkGameManager.Instance is null. Recording will start empty and capture participants as they connect.");
            return;
        }

        foreach (var kvp in XRINetworkGameManager.Instance.ConnectedPlayers)
        {
            RegisterParticipant(kvp.Key, kvp.Value, isInitial: true);
        }
    }

    private void SubscribeToPlayerEvents()
    {
        if (subscribedToPlayerEvents) return;
        if (XRINetworkGameManager.Instance == null) return;

        XRINetworkGameManager.Instance.OnPlayerStateChanged += OnPlayerStateChanged;
        subscribedToPlayerEvents = true;
    }

    private void UnsubscribeFromPlayerEvents()
    {
        if (!subscribedToPlayerEvents) return;
        if (XRINetworkGameManager.Instance != null)
            XRINetworkGameManager.Instance.OnPlayerStateChanged -= OnPlayerStateChanged;
        subscribedToPlayerEvents = false;
    }

    private void OnPlayerStateChanged(ulong clientId, bool isConnected)
    {
        if (!isRecording) return;

        if (isConnected)
        {
            if (XRINetworkGameManager.Instance != null &&
                XRINetworkGameManager.Instance.TryGetPlayerByID(clientId, out NetworkObject playerObj))
            {
                RegisterParticipant(clientId, playerObj, isInitial: false);
            }
        }
        else
        {
            // Mark as disconnected — keep the slot so existing recorded frames stay valid;
            // future frames just won't include this participant.
            if (clientIdToSlot.TryGetValue(clientId, out int slot))
            {
                var t = tracked[slot];
                t.networkObject = null;
                t.head = null;
                t.leftHand = null;
                t.rightHand = null;
                tracked[slot] = t;
            }
        }
    }

    private void RegisterParticipant(ulong clientId, NetworkObject playerObj, bool isInitial)
    {
        if (clientIdToSlot.ContainsKey(clientId)) return; // already tracked

        var info = new ParticipantInfo
        {
            networkId = clientId.ToString(),
            displayName = playerObj.name,
            joinedAtTimestamp = isInitial ? 0f : currentDuration,
            colorRGB = Color.white,
            avatarColorId = 0
        };

        var trackedEntry = new TrackedParticipant
        {
            clientId = clientId,
            networkObject = playerObj,
            networkIdString = info.networkId,
            transformsBound = false
        };

        int slot = tracked.Count;
        tracked.Add(trackedEntry);
        clientIdToSlot[clientId] = slot;
        currentSession.participants.Add(info);
        participantCount = tracked.Count;

        // Best-effort: try to bind transforms + identity now; if not ready, Update() retries.
        TryBindTransforms(slot);
    }

    private void TryBindTransforms(int slot)
    {
        var t = tracked[slot];
        if (t.networkObject == null) return;

        if (!PlayerTransformProvider.TryGetHandles(t.networkObject, out var handles)) return;

        t.head = handles.head;
        t.leftHand = handles.leftHand;
        t.rightHand = handles.rightHand;
        t.transformsBound = true;
        tracked[slot] = t;

        // Update identity (the player's name/color may not have been set until after spawn).
        var info = currentSession.participants[slot];
        info.displayName = handles.displayName;
        info.colorRGB = handles.color;
        currentSession.participants[slot] = info;
    }

    // ── Frame capture ────────────────────────────────────────────────────

    private void CaptureFrame(float timestamp)
    {
        var frame = new RecordingFrame { timestamp = timestamp };
        if (frame.avatarPoses == null) frame.avatarPoses = new List<AvatarPose>(tracked.Count);

        for (int i = 0; i < tracked.Count; i++)
        {
            var t = tracked[i];
            if (!t.transformsBound) continue;       // not bound yet (just-joined player)
            if (t.head == null) continue;            // disconnected; skip

            frame.avatarPoses.Add(new AvatarPose
            {
                networkId = t.networkIdString,
                headPos = t.head.position,
                headRot = t.head.rotation,
                lHandPos = t.leftHand != null ? t.leftHand.position : t.head.position,
                lHandRot = t.leftHand != null ? t.leftHand.rotation : t.head.rotation,
                rHandPos = t.rightHand != null ? t.rightHand.position : t.head.position,
                rHandRot = t.rightHand != null ? t.rightHand.rotation : t.head.rotation,
                isSpeaking = false
            });
        }

        currentSession.frames.Add(frame);
        framesRecorded++;
    }

    // ── World events ─────────────────────────────────────────────────────

    /// <summary>
    /// Phase 2 event API. Gameplay systems call <c>WorldEventRecorder.Record(evt)</c>
    /// which forwards here. We stamp the timestamp from the current recording clock
    /// and persist the typeKey + serialized payload — the replayer resolves them
    /// back into an <see cref="WaterVision.Recording.Events.IRecordableEvent"/> via
    /// <c>WorldEventReplayer.Deserialize</c>.
    /// </summary>
    public void RecordEvent(WaterVision.Recording.Events.IRecordableEvent evt)
    {
        if (!isRecording || evt == null) return;

        currentSession.worldEvents.Add(new RecordedWorldEvent
        {
            typeKey = evt.TypeKey,
            timestamp = currentDuration,
            payload = evt.SerializePayload()
        });
        eventsRecorded++;
    }

    // ── I/O ──────────────────────────────────────────────────────────────

    private void SaveToFile()
    {
        try
        {
            string json = JsonUtility.ToJson(currentSession, true);
            string filename = currentSession.sessionID + ".json";
            string path = Path.Combine(Application.persistentDataPath, filename);
            File.WriteAllText(path, json);
            Debug.Log($"[SpatialRecorder] Saved {currentSession.frames.Count} frame(s), {currentSession.participants.Count} participant(s) to {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SpatialRecorder] Failed to save recording: {e.Message}");
        }
    }
}
