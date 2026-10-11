// Hand teleporter — XR Hands microgesture detector (restored Pass 3, 2026-05-28).
//
// Restored at user request after the OVRMicrogestureEventSource (Pass 5) and proximity (Pass 4)
// experiments. The user noted earlier "not working" reports were due to incorrect testing, so
// this curl-based detector may have been fine all along. This is the permissive curl-edge version.
//
// State flow (matches Meta XR Interaction SDK locomotion sample):
//   IDLE   → ThumbTap          → AIMING (parabolic arc ray rendered)
//   AIMING → ThumbTap          → commit teleport → IDLE
//   IDLE   → SwipeLeft/Right   → snap-turn ±m_SnapTurnDegrees (does NOT enter aiming)
//   AIMING → SwipeLeft/Right   → IGNORED (so user can re-aim without spurious turns)
//   ANY    → hand untracked    → cancel any aiming → IDLE
//
// Detection (XR Hands API only — no OVRPlugin / Meta SDK dependency):
//   ThumbTap = thumb FullCurl rises above m_ThumbTapCurl (edge detect), then must fall below
//              m_ThumbTapResetCurl to re-arm. There is NO 'trigger pose' gate — fingers can be
//              in any position. A trigger-pose gate created brittle behaviour (lose pose
//              mid-tap → tap dropped), so it was removed.
//   Swipe   = thumb tip's hand-LOCAL X position changes by more than m_SwipeMinDistance over
//             the last m_SwipeWindowSeconds, with sign giving direction. Right-hand local +X
//             is the radial direction (toward the thumb side) per OpenXR Hand convention.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;   // extension method XRHand.CalculateFingerShape
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;   // XRInteractorLineVisual (reticle toggle)
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Unity.XR.CoreUtils;

[DefaultExecutionOrder(100)]
public class HandThumbTapTeleporter : MonoBehaviour
{
    public enum HandSide { Left, Right }
    public enum AimState { Idle, Aiming }

    [Header("References")]
    [SerializeField, Tooltip("Teleport-only XRRayInteractor (InteractionLayers = Teleport, LineType = ProjectileCurve).")]
    XRRayInteractor m_TeleportRay;

    // Cached line visual on the same GameObject. Toggled with the ray in Enter/ExitAiming so the
    // teleport reticle (landing circle) is cleared on cancel/commit instead of lingering on the floor.
    XRInteractorLineVisual m_LineVisual;

    [SerializeField, Tooltip("TeleportationProvider that performs the locomotion. Auto-resolved if null.")]
    TeleportationProvider m_TeleportationProvider;

    [SerializeField, Tooltip("XR Origin to rotate when snap-turning. Auto-resolved if null.")]
    XROrigin m_XROrigin;

    [Header("Hand selection")]
    [SerializeField, Tooltip("Which hand drives gestures.")]
    HandSide m_Hand = HandSide.Right;

    [Header("Ray origin (driven from the tracked hand)")]
    [SerializeField, Tooltip("Drive this GameObject's transform from the XR Hand wrist pose each frame so the teleport " +
                             "ray fires from the HAND, not from the controller-following parent. Turn off only to debug.")]
    bool m_DriveOriginFromHand = true;

    [SerializeField, Tooltip("Euler offset applied to the wrist pose rotation to set the aim direction. The wrist pose's " +
                             "+Z points toward the fingers; a positive X (pitch-down) makes a comfortably-held fist arc to " +
                             "the floor ahead instead of straight down. Tune in-headset.")]
    Vector3 m_AimEulerOffset = new Vector3(30f, 0f, 0f);

    [SerializeField, Tooltip("Metres to push the ray origin forward (along the aim direction) from the wrist — e.g. toward " +
                             "the knuckles, so the ray doesn't start inside the fist. 0 = exactly at the wrist joint.")]
    float m_OriginForwardOffset = 0.05f;

    [Header("ThumbTap detection (permissive curl-edge)")]
    [SerializeField, Range(0.2f, 0.9f), Tooltip("Thumb FullCurl must rise ABOVE this to fire a tap. " +
                                                 "Default 0.45 — relaxed enough to catch a natural thumb flex.")]
    float m_ThumbTapCurl = 0.45f;

    [SerializeField, Range(0.05f, 0.7f), Tooltip("Thumb FullCurl must fall BELOW this before another tap can fire (debounce).")]
    float m_ThumbTapResetCurl = 0.25f;

    [SerializeField, Range(0.05f, 1f), Tooltip("Minimum seconds between consecutive taps.")]
    float m_TapCooldownSeconds = 0.30f;

    [Header("Trigger pose (must hold a FIST for taps to count — prevents accidental teleports)")]
    [SerializeField, Tooltip("Require a fist (non-thumb fingers curled) before a thumb tap is accepted. " +
                             "Matches the 'fist + thumb up, then tap' gesture and stops a relaxed/open hand " +
                             "from triggering teleport accidentally.")]
    bool m_RequireFist = true;

    [SerializeField, Range(0.3f, 0.95f), Tooltip("Each of index/middle/ring/little FullCurl must be ABOVE this for " +
                                                  "the hand to count as a fist. Default 0.55 — a deliberate closed hand.")]
    float m_FistFingerCurl = 0.55f;

    [SerializeField, Range(2, 4), Tooltip("How many of the 4 non-thumb fingers must be curled past the fist threshold. " +
                                          "3 tolerates one mistracked finger.")]
    int m_FistFingersRequired = 3;

    [SerializeField, Range(0f, 1f), Tooltip("The fist must be HELD this many seconds before a thumb tap is accepted. " +
                                            "This distinguishes a deliberate thumb-tap-in-a-held-fist from the thumb " +
                                            "incidentally curling while you CLOSE a fist to grab an object (grab = fingers+" +
                                            "thumb close together, so the fist was never held first). Default 0.25.")]
    float m_FistDwellSeconds = 0.25f;

    [Header("Open-hand cancel (release the fist to abort aiming)")]
    [SerializeField, Tooltip("While AIMING, fully opening the hand cancels and exits aiming without teleporting.")]
    bool m_OpenHandCancels = true;

    [SerializeField, Range(0.05f, 0.5f), Tooltip("Aiming is cancelled when ALL 4 non-thumb fingers fall BELOW this curl " +
                                                  "(hand opened). Default 0.2.")]
    float m_OpenHandCurl = 0.2f;

    [Header("Swipe-turn (only fires in IDLE)")]
    [SerializeField, Tooltip("Enable thumb-swipe → snap-turn.")]
    bool m_EnableSwipeTurn = true;

    [SerializeField, Range(0.01f, 0.08f), Tooltip("Thumb tip must travel at least this many metres along the hand's lateral " +
                                                   "(radial-ulnar) axis to count as a swipe. Default 0.025.")]
    float m_SwipeMinDistance = 0.025f;

    [SerializeField, Range(0.05f, 0.5f), Tooltip("Sliding window for swipe detection (seconds).")]
    float m_SwipeWindowSeconds = 0.20f;

    [SerializeField, Range(15f, 90f), Tooltip("Snap-turn angle per swipe (degrees). Positive turns right.")]
    float m_SnapTurnDegrees = 45f;

    [SerializeField, Range(0.1f, 1.5f), Tooltip("Cooldown after a swipe fires (prevents one physical swipe firing twice).")]
    float m_SwipeCooldownSeconds = 0.50f;

    [SerializeField, Range(0f, 0.5f), Tooltip("Grace period after a tap before swipe detection resumes — keeps the small " +
                                               "lateral motion that accompanies a tap from being mis-classified as a swipe.")]
    float m_SwipeGraceAfterTap = 0.25f;

    [Header("Diagnostics — leave ON until verified on-device")]
    [SerializeField, Tooltip("Log every state transition + every fired tap / swipe.")]
    bool m_DebugLog = true;

    [SerializeField, Tooltip("Log per-second snapshot of thumb curl + thumb tip local X. Read on-device via 'adb logcat -s Unity'.")]
    bool m_VerboseLog = true;

    [SerializeField, Tooltip("Show a floating text readout IN THE HEADSET (parented to the camera) with the live " +
                              "thumb curl, tracked state, and aim state. THE key tool for diagnosing why a gesture " +
                              "isn't firing — you can SEE the curl value while you move your thumb. Turn off for production.")]
    bool m_ShowInHeadsetHUD = false;

    TextMesh m_Hud;
    float m_PeakCurlThisSecond;

    // ─── Runtime state ──────────────────────────────────────────────────────────────────────
    XRHandSubsystem m_HandSubsystem;
    AimState m_State = AimState.Idle;

    bool m_ThumbTapArmed = false;      // armed only while a HELD fist has the thumb extended; cleared on tap / leaving fist
    float m_LastTapTime = -10f;
    float m_FistHeldTime = 0f;   // how long the hand has continuously been in a fist (for the dwell gate)

    struct ThumbSample { public float localX; public float time; }
    readonly Queue<ThumbSample> m_ThumbWindow = new();
    float m_LastSwipeTime = -10f;

    float m_NextVerboseLogTime;
    int m_FrameCounter;

    void Awake()
    {
        if (m_TeleportationProvider == null)
            m_TeleportationProvider = FindFirstObjectByType<TeleportationProvider>();
        if (m_XROrigin == null)
            m_XROrigin = FindFirstObjectByType<XROrigin>();

        if (m_TeleportRay != null)
            m_TeleportRay.enabled = false;
    }

    void OnEnable()
    {
        ResolveHandSubsystem();
        CreateHudIfNeeded();
    }

    // Builds a world-space TextMesh parented to the XR camera so the readout always faces the
    // user, floating slightly below-center. Uses Unity's built-in legacy font so it works in
    // a build without any font asset wiring.
    void CreateHudIfNeeded()
    {
        if (!m_ShowInHeadsetHUD || m_Hud != null) return;

        Transform camT = (m_XROrigin != null && m_XROrigin.Camera != null)
            ? m_XROrigin.Camera.transform
            : (Camera.main != null ? Camera.main.transform : null);
        if (camT == null) return;

        var go = new GameObject("HandTeleportDebugHUD");
        go.transform.SetParent(camT, false);
        go.transform.localPosition = new Vector3(0f, -0.18f, 0.6f); // 0.6 m ahead, a bit low
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * 0.0035f;

        m_Hud = go.AddComponent<TextMesh>();
        m_Hud.fontSize = 90;
        m_Hud.characterSize = 0.1f;
        m_Hud.anchor = TextAnchor.UpperCenter;
        m_Hud.alignment = TextAlignment.Left;
        m_Hud.color = Color.green;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            m_Hud.font = font;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = font.material;
        }
        if (m_DebugLog) Debug.Log("[HandThumbTapTeleporter] In-headset debug HUD created.", this);
    }

    void OnDisable()
    {
        if (m_Hud != null)
        {
            Destroy(m_Hud.gameObject);
            m_Hud = null;
        }
    }

    void ResolveHandSubsystem()
    {
        if (m_HandSubsystem != null && m_HandSubsystem.running) return;

        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var s in subsystems)
            if (s != null && s.running) { m_HandSubsystem = s; break; }

        if (m_DebugLog)
            Debug.Log($"[HandThumbTapTeleporter] XRHandSubsystem = " +
                      $"{(m_HandSubsystem != null ? (m_HandSubsystem.running ? "running" : "stopped") : "NOT FOUND — hand tracking disabled?")}", this);
    }

    void Update()
    {
        m_FrameCounter++;

        if (m_Hud == null) CreateHudIfNeeded();   // camera may not have existed at OnEnable

        if (m_TeleportRay == null || m_TeleportationProvider == null)
        {
            SetHud("<refs missing>\nray or provider not wired", Color.red);
            return;
        }
        if (m_HandSubsystem == null || !m_HandSubsystem.running) ResolveHandSubsystem();
        if (m_HandSubsystem == null)
        {
            SetHud("NO XR HAND SUBSYSTEM\nhand tracking not running", Color.red);
            return;
        }

        XRHand hand = m_Hand == HandSide.Left ? m_HandSubsystem.leftHand : m_HandSubsystem.rightHand;

        // ── Hand untracked → cancel + bail ────────────────────────────────────────────────────
        if (!hand.isTracked)
        {
            if (m_State == AimState.Aiming) ExitAiming("hand untracked");
            m_ThumbTapArmed = false;   // disarm; re-arms only on a fresh held-fist + thumb-up
            ClearThumbWindow();
            SetHud($"{m_Hand} HAND NOT TRACKED\nraise hand into view", Color.yellow);
            return;
        }

        // ── Read thumb curl + thumb tip pose ──────────────────────────────────────────────────
        float thumbCurl = GetFullCurl(hand, XRHandFingerID.Thumb);

        Pose wristPose, thumbTipPose;
        bool gotWrist    = hand.GetJoint(XRHandJointID.Wrist   ).TryGetPose(out wristPose);
        bool gotThumbTip = hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out thumbTipPose);
        if (!gotWrist || !gotThumbTip)
        {
            SetHud($"joint pose missing\nwrist={gotWrist} thumbTip={gotThumbTip}", Color.yellow);
            return;
        }

        Vector3 thumbLocal = Quaternion.Inverse(wristPose.rotation) * (thumbTipPose.position - wristPose.position);
        float now = Time.unscaledTime;

        // ── Fist trigger-pose + open-hand cancel ──────────────────────────────────────────────
        // Read the 4 non-thumb finger curls. A tap only counts while a FIST is held (prevents
        // accidental teleports from a relaxed/open hand), and fully OPENING the hand cancels aiming.
        float idxCurl = GetFullCurl(hand, XRHandFingerID.Index);
        float midCurl = GetFullCurl(hand, XRHandFingerID.Middle);
        float rngCurl = GetFullCurl(hand, XRHandFingerID.Ring);
        float litCurl = GetFullCurl(hand, XRHandFingerID.Little);

        int fingersCurled = (idxCurl > m_FistFingerCurl ? 1 : 0) + (midCurl > m_FistFingerCurl ? 1 : 0)
                          + (rngCurl > m_FistFingerCurl ? 1 : 0) + (litCurl > m_FistFingerCurl ? 1 : 0);
        // The INDEX finger must be curled for a fist. The index is the discriminator between a
        // teleport fist (index curled with the rest) and a "point to press a button" pose (index
        // extended, other fingers curled). Without this, a pointing pose satisfied the >=3-fingers
        // count and a slight thumb move false-fired the teleport. Requiring the index curled rejects
        // the pointing pose before any thumb-tap logic runs; a deliberate fist curls the index anyway.
        bool inFist = !m_RequireFist || (idxCurl > m_FistFingerCurl && fingersCurled >= m_FistFingersRequired);
        bool handOpen = idxCurl < m_OpenHandCurl && midCurl < m_OpenHandCurl
                     && rngCurl < m_OpenHandCurl && litCurl < m_OpenHandCurl;

        // Fist-dwell: accumulate how long the fist has been continuously held. A thumb tap only
        // counts once the fist has been established for m_FistDwellSeconds — so closing a fist to
        // grab (fingers+thumb close together, fist not held first) never triggers a teleport.
        if (inFist) m_FistHeldTime += Time.unscaledDeltaTime;
        else        m_FistHeldTime = 0f;
        bool fistEstablished = m_FistHeldTime >= m_FistDwellSeconds;

        // Open-hand cancel: release the fist while aiming to abort without teleporting.
        if (m_State == AimState.Aiming && m_OpenHandCancels && handOpen)
        {
            ExitAiming("hand opened");
            m_ThumbTapArmed = false;   // disarm; re-arms only on a fresh held-fist + thumb-up
            SetHud($"{m_Hand} IDLE (cancelled)\nopen hand → cancel", Color.cyan);
            return;
        }

        // ── Drive the ray origin from the HAND ────────────────────────────────────────────────
        // The XRRayInteractor is on THIS GameObject and (with m_RayOriginTransform null) raycasts
        // from this transform. Parented under the controller-following "Right Hand" group, it was
        // firing from the desk controller (waist height) → teleport landed at the player's feet.
        // Drive this transform from the tracked wrist pose so the ray (and its visible line) starts
        // at the hand and aims where the fist points. wristPose.rotation's +Z points distally; the
        // tunable euler offset pitches it down so a forward-held fist arcs to the floor ahead.
        if (m_DriveOriginFromHand)
        {
            // CRITICAL: XR Hands joint poses are in the rig's TRACKING space (XR-Origin-relative),
            // NOT world space — the XRHandSkeletonDriver applies them as LOCAL poses under the rig.
            // The XR Origin sits at the player's world position (e.g. (-11,0,-11) after a teleport),
            // so using wristPose.position directly as a world position would put the ray ~11 m away
            // near the world origin. Convert through the rig's tracking transform (Camera Offset,
            // the same space controllers are placed in) to get the true world pose at the hand.
            Transform track = ResolveTrackingSpace();
            if (track != null)
            {
                Quaternion worldWristRot = track.rotation * wristPose.rotation;
                Vector3 worldWristPos = track.TransformPoint(wristPose.position);
                Quaternion aimRot = worldWristRot * Quaternion.Euler(m_AimEulerOffset);
                Vector3 aimPos = worldWristPos + aimRot * (Vector3.forward * m_OriginForwardOffset);
                transform.SetPositionAndRotation(aimPos, aimRot);
            }
        }

        // Track the peak curl seen in the last second — the per-second snapshot can MISS the
        // brief spike at the moment of a tap, so we report the peak too.
        if (thumbCurl > m_PeakCurlThisSecond) m_PeakCurlThisSecond = thumbCurl;

        // ── In-headset HUD ──────────────────────────────────────────────────────────────────────
        // IDLE: show live curl so we can see if the tap threshold is reached (green=armed, cyan=fired).
        // AIMING: show what the teleport ray is hitting — THIS is the diagnostic for "teleport
        //         doesn't work": it tells us whether the ray reaches a valid TeleportationArea.
        if (m_State == AimState.Aiming)
        {
            string hitInfo;
            Color aimColor;
            if (m_TeleportRay.TryGetCurrent3DRaycastHit(out RaycastHit aimHit))
            {
                var tp = aimHit.collider.GetComponentInParent<BaseTeleportationInteractable>();
                if (tp != null) { hitInfo = $"VALID target:\n{tp.name}"; aimColor = Color.green; }
                else            { hitInfo = $"hit NON-teleport:\n{aimHit.collider.name}"; aimColor = new Color(1f,0.5f,0f); }
            }
            else { hitInfo = "ray hits NOTHING\n(point down at floor)"; aimColor = Color.red; }

            SetHud($"AIMING  armed={(m_ThumbTapArmed ? "Y" : "N")}\ncurl={thumbCurl:F2}\n{hitInfo}\ntap again to teleport", aimColor);
        }
        else
        {
            SetHud($"{m_Hand} IDLE\ncurl={thumbCurl:F2}  peak1s={m_PeakCurlThisSecond:F2}\n" +
                   $"tapAt>{m_ThumbTapCurl:F2}  armed={(m_ThumbTapArmed ? "Y" : "N")}\n" +
                   $"fist={fingersCurled}/4 {(inFist ? "OK" : "make a fist")}",
                   inFist ? (m_ThumbTapArmed ? Color.green : Color.cyan) : Color.gray);
        }

        // ── Periodic verbose diagnostic ───────────────────────────────────────────────────────
        if (m_VerboseLog && now >= m_NextVerboseLogTime)
        {
            m_NextVerboseLogTime = now + 1.0f;
            Debug.Log($"[HandThumbTapTeleporter] tracked=True  thumbCurl={thumbCurl:F2}  peak1s={m_PeakCurlThisSecond:F2}  " +
                      $"thumbLocalX={thumbLocal.x:F3}  armed={m_ThumbTapArmed}  state={m_State}", this);
            m_PeakCurlThisSecond = 0f;   // reset peak each second
        }

        // ── Tap detection — ARM only on a HELD fist with the thumb EXTENDED, then FIRE on thumb-down ─
        // This is what actually distinguishes "tap" from "grab". A grab curls thumb+fingers TOGETHER
        // and holds them curled — it never reaches "established fist + thumb up", so it never ARMS,
        // so it never fires. A deliberate tap: hold a fist with the thumb up (ARMS), then tap the
        // thumb down (FIRES).
        //
        // The previous dwell-ONLY gate did NOT fix the grab problem — it only delayed it: a held
        // grab still satisfied (fistEstablished && armed && thumbCurl>tap) once the dwell elapsed,
        // because `armed` started true and nothing cleared it during the grab. Gating ARMING on
        // (fist held + thumb extended) is the real fix; the dwell still suppresses the brief
        // transient as you first close the fist.
        if (!inFist)
            m_ThumbTapArmed = false;                                  // leaving the fist disarms
        else if (fistEstablished && thumbCurl < m_ThumbTapResetCurl)
            m_ThumbTapArmed = true;                                   // arm: held fist + thumb extended

        bool tapFired = false;
        if (inFist && m_ThumbTapArmed && thumbCurl > m_ThumbTapCurl && now >= m_LastTapTime + m_TapCooldownSeconds)
        {
            m_ThumbTapArmed = false;
            m_LastTapTime = now;
            tapFired = true;
        }

        if (tapFired)
        {
            if (m_State == AimState.Idle)
                EnterAiming($"tap (curl={thumbCurl:F2})");
            else /* Aiming */
                CommitTeleport($"tap (curl={thumbCurl:F2})");
            ClearThumbWindow();
            return;
        }

        // ── Swipe-turn REMOVED per spec (2026-05-28) ──────────────────────────────────────────
        // Swipe now does NOTHING in either state — the player turns with their own body. This
        // also eliminates a real interference bug: a thumb tap is not a pure vertical motion, the
        // thumb tip also drifts sideways. The old code could read that lateral drift as a swipe
        // and fire a snap-turn INSTEAD of the tap entering aiming — which felt "inconsistent" and
        // polluted every tap attempt. With swipe gone, the only gesture is the thumb-curl tap.
        // (UpdateSwipeDetection / SnapTurn remain defined but are intentionally never called.)
    }

    // ─── Swipe detection (delta-based) ────────────────────────────────────────────────────────
    void UpdateSwipeDetection(float localX, float now)
    {
        m_ThumbWindow.Enqueue(new ThumbSample { localX = localX, time = now });
        while (m_ThumbWindow.Count > 0 && now - m_ThumbWindow.Peek().time > m_SwipeWindowSeconds)
            m_ThumbWindow.Dequeue();

        if (m_ThumbWindow.Count < 2) return;

        float oldestX = m_ThumbWindow.Peek().localX;
        float delta = localX - oldestX;
        if (Mathf.Abs(delta) < m_SwipeMinDistance) return;

        m_LastSwipeTime = now;
        ClearThumbWindow();

        // Right hand: +localX = radial (toward thumb) = user-perceived "right" with palm down.
        // Left hand: mirrored.
        bool turnRight = (m_Hand == HandSide.Right) ? (delta > 0f) : (delta < 0f);
        SnapTurn(turnRight ? +m_SnapTurnDegrees : -m_SnapTurnDegrees, turnRight ? "right" : "left", delta);
    }

    void ClearThumbWindow() => m_ThumbWindow.Clear();

    void SetHud(string text, Color color)
    {
        if (m_Hud == null) return;
        m_Hud.text = text;
        m_Hud.color = color;
    }

    // ─── State transitions ────────────────────────────────────────────────────────────────────
    void EnterAiming(string reason)
    {
        m_State = AimState.Aiming;
        if (m_TeleportRay != null) m_TeleportRay.enabled = true;
        // Enable the line visual so the ray + landing reticle render while aiming.
        if (m_LineVisual == null && m_TeleportRay != null) m_LineVisual = m_TeleportRay.GetComponent<XRInteractorLineVisual>();
        if (m_LineVisual != null) m_LineVisual.enabled = true;
        if (m_DebugLog) Debug.Log($"[HandThumbTapTeleporter] IDLE → AIMING — {reason}", this);
    }

    void ExitAiming(string reason)
    {
        m_State = AimState.Idle;
        if (m_TeleportRay != null) m_TeleportRay.enabled = false;
        // Disable the line visual too. Its OnDisable() does m_ReticleToUse.SetActive(false), which
        // is what removes the landing circle from the floor on cancel/commit — disabling only the
        // ray interactor does NOT clear the reticle (that was the "circle stuck on floor" bug).
        if (m_LineVisual == null && m_TeleportRay != null) m_LineVisual = m_TeleportRay.GetComponent<XRInteractorLineVisual>();
        if (m_LineVisual != null) m_LineVisual.enabled = false;
        if (m_DebugLog) Debug.Log($"[HandThumbTapTeleporter] AIMING → IDLE — {reason}", this);
    }

    void CommitTeleport(string source)
    {
        if (!m_TeleportRay.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            if (m_DebugLog) Debug.Log($"[HandThumbTapTeleporter] {source} — ray hit nothing, stay in AIMING.", this);
            return;
        }

        var teleportable = hit.collider.GetComponentInParent<BaseTeleportationInteractable>();
        if (teleportable == null)
        {
            if (m_DebugLog) Debug.Log($"[HandThumbTapTeleporter] {source} — hit '{hit.collider.name}' not a TeleportationArea/Anchor, stay in AIMING.", this);
            return;
        }

        var request = new TeleportRequest
        {
            destinationPosition = hit.point,
            destinationRotation = Quaternion.identity,
            matchOrientation = MatchOrientation.WorldSpaceUp,
            requestTime = Time.time,
        };
        bool queued = m_TeleportationProvider.QueueTeleportRequest(request);

        if (m_DebugLog)
            Debug.Log($"[HandThumbTapTeleporter] {source} → teleport queued={queued} dest={hit.point} on '{teleportable.name}'", this);

        ExitAiming("teleport committed");
    }

    // ─── Snap-turn ────────────────────────────────────────────────────────────────────────────
    void SnapTurn(float degrees, string direction, float delta)
    {
        if (m_XROrigin == null)
        {
            if (m_DebugLog) Debug.LogWarning($"[HandThumbTapTeleporter] swipe-{direction} but no XR Origin to rotate.", this);
            return;
        }
        Transform cam = m_XROrigin.Camera != null ? m_XROrigin.Camera.transform : null;
        Vector3 pivot = cam != null ? cam.position : m_XROrigin.transform.position;
        m_XROrigin.transform.RotateAround(pivot, Vector3.up, degrees);

        if (m_DebugLog) Debug.Log($"[HandThumbTapTeleporter] swipe-{direction} (Δ={delta:F3}m) → snap turn {degrees:+0;-0}°", this);
    }

    // The rig's tracking-space transform — the space XR Hands joint poses are reported in. Tracked
    // devices (controllers) are placed as children of the Camera Offset, so that is the tracking
    // space; converting a subsystem pose through it yields the correct world pose. Falls back to the
    // XR Origin root if the Camera Offset object isn't assigned.
    Transform ResolveTrackingSpace()
    {
        if (m_XROrigin == null) return null;
        return m_XROrigin.CameraFloorOffsetObject != null
            ? m_XROrigin.CameraFloorOffsetObject.transform
            : m_XROrigin.transform;
    }

    // ─── XR Hands helpers ─────────────────────────────────────────────────────────────────────
    static float GetFullCurl(XRHand hand, XRHandFingerID finger)
    {
        var shape = hand.CalculateFingerShape(finger, XRFingerShapeTypes.FullCurl);
        return shape.TryGetFullCurl(out float v) ? v : 0f;
    }
}
