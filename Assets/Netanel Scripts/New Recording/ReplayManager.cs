using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WaterVision;
using WaterVision.Recording;
using XRMultiplayer;

/// <summary>
/// Loads a recorded session and drives one Ghost per recorded participant. Interpolates
/// head + both hands between consecutive frames. Honors per-participant joinedAtTimestamp
/// so mid-recording joiners only appear at their actual entry time.
/// </summary>
public class ReplayManager : MonoBehaviour
{
    public static ReplayManager Instance;

    [Header("Setup")]
    public GameObject ghostPrefab;
    public PlaybackControlUI playbackUI;

    [Tooltip("Translucent material applied to every ghost renderer at spawn so the ghost reads " +
             "as a recording (see-through) rather than a live user. Tinted per-participant, alpha ~0.8.")]
    public Material ghostMaterial;

    [Header("Adjustments")]
    [Tooltip("If true, live player avatars are hidden during replay so only ghosts are visible. " +
             "Default false: translucent ghosts are meant to coexist with the real (opaque) users.")]
    public bool hideLivePlayersDuringReplay = false;

    [Header("Playback State")]
    public bool isPlaying = false;
    public bool IsActive { get; private set; } = false;

    [SerializeField] private float playbackTime = 0f;
    [SerializeField] private float totalDuration = 0f;

    private RecordingSession currentSession;
    private readonly Dictionary<string, GhostController> activeGhosts = new Dictionary<string, GhostController>();
    private int currentFrameIndex = 0;

    // Index into currentSession.worldEvents — events strictly before this index have already
    // fired in this playthrough. Reset when LoadAndPlay or JumpToTime is called.
    private int currentEventIndex = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (playbackUI != null) playbackUI.OnHide();
    }

    private void Update()
    {
        if (!IsActive || !isPlaying) return;

        playbackTime += Time.deltaTime;

        if (playbackTime >= totalDuration)
        {
            StopReplay();
            return;
        }

        ProcessFrame();
    }

    public void LoadAndPlay(string filePath)
    {
        StopReplay();

        if (!File.Exists(filePath))
        {
            Debug.LogError("[ReplayManager] File not found: " + filePath);
            return;
        }

        string json = File.ReadAllText(filePath);
        currentSession = JsonUtility.FromJson<RecordingSession>(json);
        if (currentSession == null)
        {
            Debug.LogError("[ReplayManager] Failed to parse recording JSON.");
            return;
        }

        totalDuration = currentSession.totalDuration;
        playbackTime = 0f;
        currentFrameIndex = 0;
        currentEventIndex = 0;

        SpawnGhosts();

        if (hideLivePlayersDuringReplay) AvatarLayerController.SetGhostMode(true);

        // Flip the global "replay is active" flag BEFORE we re-fire any events — so
        // gameplay systems wired to listen suppress their own input/networking
        // before world-event Apply() calls land on them.
        WaterVision.Recording.WorldEventReplayer.SetReplayActive(true);

        IsActive = true;
        isPlaying = false;

        // Snap ghosts to their initial pose at t=0.
        ProcessFrame();

        // Auto-start playback so selecting a recording plays immediately. Previously this stayed
        // paused with no visible control to un-pause it — the core "could record but couldn't
        // play it back" symptom. The playback panel (if wired) still lets the user pause/scrub/stop.
        isPlaying = true;

        if (playbackUI != null) playbackUI.OnShow();
    }

    private void SpawnGhosts()
    {
        if (currentSession.participants == null) return;

        // Match the ghost avatar to the LOCAL player's current in-session avatar (per the
        // user's request — "show whichever character the player is using right now").
        // Falls back to the classic Ghost.prefab if the catalog or the index is unavailable.
        GameObject source = ResolveGhostSourcePrefab();

        foreach (var participant in currentSession.participants)
        {
            if (string.IsNullOrEmpty(participant.networkId)) continue;
            if (activeGhosts.ContainsKey(participant.networkId)) continue;

            GameObject ghost = InstantiateGhost(source);
            ghost.name = "Ghost_" + participant.displayName;
            ghost.tag = "Untagged";

            var controller = ghost.GetComponent<GhostController>();
            if (controller == null) controller = ghost.AddComponent<GhostController>();
            controller.Initialize();

            // Hide the ghost until its participant's joinedAtTimestamp arrives.
            controller.SetVisible(participant.joinedAtTimestamp <= 0f);

            // Make the ghost translucent (~0.8 alpha) so it reads as a recording, tinted by
            // the participant's color. Falls back to a plain tint if no ghost material is set.
            if (ghostMaterial != null)
                controller.ApplyGhostMaterial(ghostMaterial, participant.colorRGB);
            else if (participant.colorRGB.a > 0f)
                controller.SetTint(participant.colorRGB);

            activeGhosts[participant.networkId] = controller;
        }
    }

    public void TogglePause()
    {
        if (!IsActive) return;
        isPlaying = !isPlaying;
    }

    public void StopReplay()
    {
        foreach (var ghost in activeGhosts.Values)
        {
            if (ghost != null) Destroy(ghost.gameObject);
        }
        activeGhosts.Clear();

        if (hideLivePlayersDuringReplay) AvatarLayerController.SetGhostMode(false);

        // Release the global "replay is active" flag so live gameplay systems
        // resume processing input/network state normally.
        WaterVision.Recording.WorldEventReplayer.SetReplayActive(false);

        isPlaying = false;
        IsActive = false;
        playbackTime = 0;
        currentFrameIndex = 0;
        currentEventIndex = 0;

        if (playbackUI != null) playbackUI.OnHide();
    }

    // ── Ghost source selection + safe spawn ─────────────────────────────────────

    private static AvatarCatalog s_catalog;
    private static AvatarCatalog Catalog
    {
        get
        {
            if (s_catalog == null) s_catalog = Resources.Load<AvatarCatalog>("AvatarCatalog");
            return s_catalog;
        }
    }

    /// <summary>
    /// Which prefab to Instantiate for each ghost. Preference: the pre-baked
    /// Ghost_&lt;idx&gt;.prefab under Resources/Ghosts that mirrors the local player's currently
    /// selected avatar from <see cref="AvatarCatalog"/> — so replay shows the character the
    /// user actually plays as. Falls back to <see cref="ghostPrefab"/> (built-in RPM Ghost)
    /// if the pre-baked variant is missing (i.e. the user hasn't run
    /// WaterVision ▸ UI ▸ Rebuild Ghost Prefab Variants yet).
    /// </summary>
    private GameObject ResolveGhostSourcePrefab()
    {
        int idx = XRINetworkGameManager.LocalPlayerAvatarIndex != null
            ? XRINetworkGameManager.LocalPlayerAvatarIndex.Value
            : 0;

        var baked = Resources.Load<GameObject>($"Ghosts/Ghost_{idx}");
        if (baked != null) return baked;

        // Fallback #1: index 0 (the default avatar's ghost) — the user may have picked a
        // variant we haven't baked yet.
        var defaultBaked = Resources.Load<GameObject>("Ghosts/Ghost_0");
        if (defaultBaked != null) return defaultBaked;

        // Fallback #2: the classic RPM Ghost.prefab wired in the Inspector.
        return ghostPrefab;
    }

    /// <summary>
    /// Instantiate an avatar prefab as a ghost. Networked player prefabs come with a
    /// NetworkObject + a stack of NetworkBehaviours (XRAvatarIK, XRHandPoseReplicator,
    /// NetworkPlayerPositionSync, etc.) that would either (a) log errors because
    /// NetworkManager.Singleton isn't bound to this offline replay object, or (b) fight
    /// GhostController by writing their own head/hand transforms on top of ours. Spawn
    /// under an inactive holder so no Awake fires, strip the risky components, then
    /// reparent to scene root and activate.
    /// </summary>
    private GameObject InstantiateGhost(GameObject source)
    {
        var holder = new GameObject("_ghost_spawn_holder");
        holder.SetActive(false);

        GameObject ghost = Instantiate(source, holder.transform);
        StripNonGhostComponents(ghost);

        ghost.transform.SetParent(null, worldPositionStays: true);
        ghost.SetActive(true);
        Destroy(holder);
        return ghost;
    }

    // ORDER MATTERS: destroy leaf/dependent components BEFORE their dependencies.
    // The player-avatar prefabs use [RequireComponent(typeof(XRINetworkPlayer))] on
    // XRAvatarVisuals / IK / hand replicators / name tag, so removing XRINetworkPlayer
    // first errors out ("Can't remove XRINetworkPlayer because XRAvatarVisuals depends
    // on it"). Walk this list in order so dependents die first, then the base script.
    private static readonly string[] s_stripByTypeName = new[]
    {
        // Dependents (things that RequireComponent(XRINetworkPlayer) or read from it)
        "PlayerNameTag",
        "XRAvatarVisuals", "XRAvatarVisuals_RPM",
        "XRAvatarIK", "XRAvatarIK_RPM",
        "XRHandPoseReplicator", "FingerPoseReplicator",
        "NetworkPlayerPositionSync",
        "AvatarHeadFollower", "AvatarRoomscaleAndYawFollower",
        // Base networked-player script comes AFTER its dependents.
        "XRINetworkPlayer", "XRINetworkPlayer_RPM",
    };

    private static void StripNonGhostComponents(GameObject root)
    {
        // First: strip specific named types in dependency order. Do this BEFORE the
        // blanket NetworkBehaviour sweep so we honor RequireComponent chains — if the
        // sweep removes the base class first, Unity refuses and logs
        // "Can't remove XRINetworkPlayer because XRAvatarVisuals depends on it".
        foreach (var typeName in s_stripByTypeName)
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (mb.GetType().Name == typeName) Destroy(mb);
            }
        }

        // Then any remaining NetworkBehaviours (subclasses we didn't list by name) +
        // the NetworkObject itself. Offline replay has no NetworkManager.Singleton to
        // bind to, so keeping these alive would spam NREs.
        foreach (var nb in root.GetComponentsInChildren<Unity.Netcode.NetworkBehaviour>(true))
            if (nb != null) Destroy(nb);
        foreach (var no in root.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true))
            if (no != null) Destroy(no);
    }

    private void ProcessFrame()
    {
        if (currentSession == null || currentSession.frames == null || currentSession.frames.Count == 0) return;

        // Advance to the frame whose timestamp <= playbackTime (and whose successor is > playbackTime).
        while (currentFrameIndex < currentSession.frames.Count - 1 &&
               currentSession.frames[currentFrameIndex + 1].timestamp < playbackTime)
        {
            currentFrameIndex++;
        }

        RecordingFrame currentFrame = currentSession.frames[currentFrameIndex];
        RecordingFrame nextFrame = currentSession.frames[
            Mathf.Min(currentFrameIndex + 1, currentSession.frames.Count - 1)];

        float t = 0f;
        float dt = nextFrame.timestamp - currentFrame.timestamp;
        if (dt > 0f) t = Mathf.Clamp01((playbackTime - currentFrame.timestamp) / dt);

        // Reveal ghosts whose joinedAtTimestamp has arrived; hide ghosts past their last seen frame
        // (left disconnected — they simply stop appearing in frames).
        if (currentSession.participants != null)
        {
            foreach (var p in currentSession.participants)
            {
                if (string.IsNullOrEmpty(p.networkId)) continue;
                if (!activeGhosts.TryGetValue(p.networkId, out var ghost) || ghost == null) continue;
                bool shouldBeVisible = playbackTime >= p.joinedAtTimestamp;
                ghost.SetVisible(shouldBeVisible);
            }
        }

        foreach (var pose in currentFrame.avatarPoses)
        {
            if (!activeGhosts.TryGetValue(pose.networkId, out var ghost) || ghost == null) continue;

            AvatarPose nextPose = FindPose(nextFrame, pose.networkId) ?? pose;

            Vector3 headPos = Vector3.Lerp(pose.headPos, nextPose.headPos, t);
            Quaternion headRot = Quaternion.Slerp(pose.headRot, nextPose.headRot, t);
            Vector3 lPos = Vector3.Lerp(pose.lHandPos, nextPose.lHandPos, t);
            Quaternion lRot = Quaternion.Slerp(pose.lHandRot, nextPose.lHandRot, t);
            Vector3 rPos = Vector3.Lerp(pose.rHandPos, nextPose.rHandPos, t);
            Quaternion rRot = Quaternion.Slerp(pose.rHandRot, nextPose.rHandRot, t);

            ghost.ApplyPose(headPos, headRot, lPos, lRot, rPos, rRot);
        }

        // Fire any world events whose timestamp has now passed. Events are stored in
        // chronological order so a single forward cursor suffices in normal playback.
        // JumpToTime / scrubbing reset the cursor to 0 (see JumpToTime).
        if (currentSession.worldEvents != null)
        {
            while (currentEventIndex < currentSession.worldEvents.Count &&
                   currentSession.worldEvents[currentEventIndex].timestamp <= playbackTime)
            {
                var stored = currentSession.worldEvents[currentEventIndex];
                var evt = WaterVision.Recording.WorldEventReplayer.Deserialize(
                    stored.typeKey, stored.payload, stored.timestamp);
                evt?.Apply();
                currentEventIndex++;
            }
        }
    }

    private static AvatarPose FindPose(RecordingFrame frame, string networkId)
    {
        if (frame.avatarPoses == null) return null;
        for (int i = 0; i < frame.avatarPoses.Count; i++)
            if (frame.avatarPoses[i].networkId == networkId) return frame.avatarPoses[i];
        return null;
    }

    public void JumpToTime(float time)
    {
        if (!IsActive) return;
        playbackTime = Mathf.Clamp(time, 0, totalDuration);
        currentFrameIndex = 0;
        // Reset the event cursor to 0 so we re-fire everything up to the new time.
        // Note: events are NOT inherently idempotent — re-firing a door toggle on a
        // scrub-back is the right thing, but for state-changing events the gameplay
        // system needs to handle "set state to X" semantics, not "toggle from prev".
        currentEventIndex = 0;
        ProcessFrame();
    }

    public float GetCurrentTime() => playbackTime;
    public float GetTotalDuration() => totalDuration;
}
