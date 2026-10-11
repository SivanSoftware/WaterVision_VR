using UnityEngine;

/// <summary>
/// All tunable settings for the Sign-Language Bubble in ONE place.
/// Edit in the Inspector outside Play mode — values persist across Play sessions.
///
/// Asset lives at <c>Assets/Resources/ASL/ASLBubbleSettings.asset</c>
/// so <see cref="ASLSigningCharacterManager"/> can load it via
/// <see cref="Resources.Load"/> at startup. You can also create multiple
/// settings assets (different "profiles") and swap them at runtime.
/// </summary>
[CreateAssetMenu(
    fileName = "ASLBubbleSettings",
    menuName = "WaterVision/ASL/Bubble Settings",
    order = 100)]
public class ASLBubbleSettings : ScriptableObject
{
    // ── Avatar Spawn ─────────────────────────────────────────────────────────
    [Header("Avatar Spawn")]
    [Tooltip("Where in world space the hidden signing avatar spawns. Default Y=-499.6 = 0.4m above the safe hidden-far-below position.")]
    public Vector3 spawnPosition = new Vector3(0f, -499.6f, 0f);

    [Tooltip("Scale applied to the avatar root. RPM glTFast imports at ~0.5×, " +
             "so 2× brings it to ~life-size. Adjust for the specific avatar source.")]
    [Range(0.5f, 6.0f)]
    public float avatarScale = 2.0f;

    // ── Hand Ready Position ──────────────────────────────────────────────────
    [Header("Hand Ready Position (chest-relative)")]
    [Tooltip("Right hand ready position. X = out from body center, Y = height, Z = forward.")]
    public Vector3 readyRight = new Vector3( 0.20f, 1.20f, 0.30f);

    [Tooltip("Left hand ready position. Used only if Mirror Right is off.")]
    public Vector3 readyLeft  = new Vector3(-0.20f, 1.20f, 0.30f);

    [Tooltip("If on, the left hand ready position is auto-mirrored from the right (X is negated).")]
    public bool leftMirrorRight = true;

    // ── Camera Framing ───────────────────────────────────────────────────────
    [Header("Camera Framing")]
    [Tooltip("Distance from the avatar's Z=0 plane to the camera. Lower = tighter zoom.")]
    [Range(0.5f, 3.0f)]
    public float cameraDistance = 1.70f;

    [Tooltip("Camera Y position relative to the avatar's head bone height. " +
             "+0.05 = just above head top. -0.20 = at eye level.")]
    [Range(-0.5f, 1.0f)]
    public float cameraYAboveHead = 0.05f;

    [Tooltip("Where the camera looks, relative to head bone Y. " +
             "-0.30 = chest level (~30cm below head). -0.10 = mouth level.")]
    [Range(-1.0f, 0.5f)]
    public float lookYBelowHead = -0.30f;

    [Tooltip("Camera horizontal offset. Slight positive value reads as 'over-the-shoulder' angle.")]
    [Range(-0.5f, 0.5f)]
    public float cameraXOffset = 0.10f;

    [Tooltip("Field of view. 42 ≈ portrait framing, 35 = tighter, 60 = wider.")]
    [Range(20f, 80f)]
    public float cameraFOV = 42f;

    // ── IK Elbow Hints ───────────────────────────────────────────────────────
    [Header("IK Elbow Hints (prevents weird elbow flips)")]
    [Tooltip("How strongly to pull the elbow toward the hint position. 0 = no hint, 1 = fully constrain.")]
    [Range(0f, 1f)]
    public float elbowHintWeight = 0.6f;

    [Tooltip("Right-elbow hint offset from upper-arm in BODY-LOCAL space. " +
             "Negative Z = behind, negative Y = below. Left elbow mirrors X.")]
    public Vector3 rightElbowHintOffset = new Vector3(0.10f, -0.25f, -0.30f);

    [Tooltip("0 = legacy fixed elbow, 1 = elbow Y tracks hand Y 1:1. " +
             "~0.7 reads as natural shoulder lift for face-level signs (HELLO, THANK-YOU).")]
    [Range(0f, 1.5f)] public float elbowHeightFollow = 0.7f;

    [Tooltip("How far the elbow swings OUT to the side as the hand rises. " +
             "~0.6 reads as a natural salute.")]
    [Range(0f, 1.5f)] public float elbowAbductFollow = 0.6f;

    // ── Wrist / Palm Orientation ─────────────────────────────────────────────
    [Header("Wrist / Palm Orientation")]
    [Tooltip("Right hand-bone axis correction (Euler°, target-frame). Corrects RPM's wrist axis " +
             "convention vs the Mixamo rig the signs were authored on (~90° forward mismatch). " +
             "Dial until the palm faces the viewer at the ready pose.")]
    public Vector3 handRotationOffsetRight = new Vector3(0f, -90f, 0f);

    [Tooltip("Left hand-bone axis correction (Euler°, target-frame). Separately tunable — " +
             "the corrective sign often differs from the right hand.")]
    public Vector3 handRotationOffsetLeft = new Vector3(0f, 90f, 0f);

    [Tooltip("Fraction of the palm roll taken by the FOREARM (pronation), the rest by the wrist. " +
             "1 = all forearm (most natural). Drop toward 0.85 only if the elbow mesh pinches.")]
    [Range(0f, 1f)]
    public float forearmFollow = 1f;

    [Tooltip("Relaxed wrist orientation (Euler°) the RIGHT hand holds BETWEEN signs (idle). " +
             "Separate from sign keyframes so idle palms relax inward instead of facing the viewer. " +
             "(0,-90,0) ≈ palm toward body. While signing, the sign's own keyframes drive the wrist.")]
    public Vector3 readyWristEulerRight = new Vector3(0f, -90f, 0f);

    [Tooltip("Relaxed idle wrist orientation (Euler°) for the LEFT hand.")]
    public Vector3 readyWristEulerLeft = new Vector3(0f, 90f, 0f);

    // ── Palm-direction table (per-sign palm orientation, Wave 2.7) ──────────
    // Each sign keyframe carries a semantic PalmDir tag (Forward / Inward /
    // SideLeft / SideRight / Up / Down). The Player resolves the tag through
    // this table at runtime, so per-sign palm orientation is ONE tag instead
    // of a per-Euler number to dial. All six Eulers are live-tunable here —
    // the whole catalog updates immediately when you change a row.
    [Header("Palm Direction Table (right hand — left mirrors X automatically)")]
    [Tooltip("Palm faces the VIEWER. Baseline that HELLO / THANK-YOU already use.")]
    public Vector3 palmEulerForward    = new Vector3(0f,   0f, 0f);
    [Tooltip("Palm faces the SIGNER'S body (PLEASE, SORRY, MORE).")]
    public Vector3 palmEulerInward     = new Vector3(0f, 180f, 0f);
    [Tooltip("Pinky-edge toward viewer (left side of hand visible).")]
    public Vector3 palmEulerSideLeft   = new Vector3(0f, -90f, 0f);
    [Tooltip("Thumb-edge toward viewer (YES, NO, EAT, DRINK).")]
    public Vector3 palmEulerSideRight  = new Vector3(0f,  90f, 0f);
    [Tooltip("Palm faces the CEILING (left hand of HELP, WHAT).")]
    public Vector3 palmEulerUp         = new Vector3(-90f, 0f, 0f);
    [Tooltip("Palm faces the FLOOR.")]
    public Vector3 palmEulerDown       = new Vector3( 90f, 0f, 0f);

    /// <summary>Look up the Euler for a PalmDir. Returns Vector3.zero for Default.</summary>
    public Vector3 GetPalmEuler(PalmDir dir)
    {
        switch (dir)
        {
            case PalmDir.Forward:   return palmEulerForward;
            case PalmDir.Inward:    return palmEulerInward;
            case PalmDir.SideLeft:  return palmEulerSideLeft;
            case PalmDir.SideRight: return palmEulerSideRight;
            case PalmDir.Up:        return palmEulerUp;
            case PalmDir.Down:      return palmEulerDown;
            default:                return Vector3.zero;
        }
    }

    // ── Face & Life (Wave 2) ─────────────────────────────────────────────────
    // NOTE: this RPM avatar only has mouthSmile + mouthOpen blendshapes (no blink/
    // brows/jaw). Expressions = smile + mouth-open + eye gaze + head tilt + breath.
    [Header("Face & Life")]
    [Tooltip("How fast the face lerps to a sign's expression (higher = snappier).")]
    [Range(1f, 20f)] public float faceLerpSpeed = 8f;

    [Tooltip("Clamp on the mouthSmile blendshape (0..100).")]
    [Range(0f, 100f)] public float smileMax = 100f;

    [Tooltip("Clamp on the mouthOpen blendshape (0..100).")]
    [Range(0f, 100f)] public float mouthOpenMax = 100f;

    [Tooltip("Overall eye-gaze strength (0 = eyes locked forward, 1 = full saccades/bias).")]
    [Range(0f, 2f)] public float gazeStrength = 1f;

    [Tooltip("Max eye micro-saccade amplitude between signs (degrees).")]
    [Range(0f, 12f)] public float saccadeAmount = 4f;

    [Tooltip("How much of each sign's head-tilt to apply (0 = off, 1 = full).")]
    [Range(0f, 2f)] public float headTiltInfluence = 1f;

    [Tooltip("Breathing chest-scale amplitude (0 = off). 0.012 ≈ subtle.")]
    [Range(0f, 0.05f)] public float breathAmplitude = 0.012f;

    [Tooltip("Breaths per second (0.25 ≈ a calm 4-second breath cycle).")]
    [Range(0.1f, 1f)] public float breathHz = 0.25f;

    // ── Helper ───────────────────────────────────────────────────────────────
    /// <summary>Resolve the actual READY_LEFT_POS taking mirroring into account.</summary>
    public Vector3 GetReadyLeft()
        => leftMirrorRight
            ? new Vector3(-readyRight.x, readyRight.y, readyRight.z)
            : readyLeft;
}
