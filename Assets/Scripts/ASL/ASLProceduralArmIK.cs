using UnityEngine;

/// <summary>
/// Manual analytic 2-bone IK. Writes shoulder + elbow LOCAL rotations directly
/// each LateUpdate. Bypasses Unity's Humanoid IK system entirely — no muscle
/// space, no bind pose, no AvatarBuilder quirks. Works on any avatar (Humanoid
/// or Generic) as long as the 3 bone transforms (shoulder→elbow→hand) exist and
/// are in a clean parent chain.
///
/// Why this exists: Unity's <c>OnAnimatorIK + SetIKPosition</c> path on a
/// runtime-built RPM Humanoid Avatar refuses to lower the arm below its imported
/// A-pose / bind pose, even after a T-pose fix and elbow hints, because the
/// solver works relative to the captured muscle-space reference. This component
/// is purely geometric — the hand lands EXACTLY at the world target regardless
/// of what pose the avatar was imported in.
///
/// Algorithm: Daniel Holden's analytic two-joint IK
/// (https://theorangeduck.com/page/simple-two-joint), adapted to Unity's
/// left-handed, Y-up coordinate system. It is a ONE-SHOT exact solver — after a
/// single application the tip sits on the target (clamped to arm reach). A pole
/// step then rotates the solved arm about the shoulder→target axis so the elbow
/// points toward a natural "down & back" hint position.
/// </summary>
public class ASLProceduralArmIK : MonoBehaviour
{
    // ── Configuration ────────────────────────────────────────────────────────
    [Tooltip("World-space target for the right hand.")]
    public Transform rightHandTarget;

    [Tooltip("World-space target for the left hand.")]
    public Transform leftHandTarget;

    [Range(0f, 1f)]
    [Tooltip("Blend weight: 0 = no IK (pose stays as animated), 1 = hand fully at target.")]
    public float weight = 1f;

    [Tooltip("Right elbow pole hint offset in BODY-LOCAL space, relative to the shoulder. " +
             "The elbow is pushed TOWARD this point. +X = out, -Y = below, -Z = behind.")]
    public Vector3 rightElbowPole = new Vector3(0.30f, -0.40f, -0.30f);

    [Tooltip("Left elbow pole auto-mirrors right (X negated) unless this is off.")]
    public bool leftMirrorsRight = true;

    [Range(0f, 1.5f)]
    [Tooltip("How much the elbow rises as the hand rises above the shoulder. " +
             "0 = legacy fixed pole. 0.7 reads as a natural shoulder shrug for face-level signs.")]
    public float elbowHeightFollow = 0.7f;

    [Range(0f, 1.5f)]
    [Tooltip("How far the elbow swings OUT to the side as the hand rises. " +
             "0.6 reads as a natural HELLO-salute / chin-forward THANK-YOU shape.")]
    public float elbowAbductFollow = 0.6f;

    [Tooltip("Also orient the HAND (wrist) bone from the target's rotation. The sign " +
             "keyframes author wrist orientation; without this the palm just dangles " +
             "from the elbow solve (usually palm-down).")]
    public bool driveHandRotation = true;

    [Range(0f, 1f)]
    [Tooltip("Fraction of the palm roll taken by the FOREARM (pronation), the rest by the wrist. " +
             "1 = all forearm (most natural — the big ~90° palm turn IS the forearm's job in a real arm). " +
             "Drop toward 0.85 only if the elbow mesh pinches (candy-wrapper).")]
    public float forearmFollow = 1f;

    [Tooltip("Right-hand bone-axis correction (Euler°, TARGET-frame). RPM wrist local axes differ " +
             "from the Mixamo rig the signs were authored on (~90° forward mismatch). " +
             "This constant rotates the neutral palm to face the viewer; tuned live.")]
    public Vector3 rightHandRotationOffset = Vector3.zero;

    [Tooltip("Left-hand bone-axis correction (Euler°, TARGET-frame). Separately tunable — " +
             "the corrective sign often differs from the right hand.")]
    public Vector3 leftHandRotationOffset = Vector3.zero;

    [Tooltip("Editor-only: draw palm-normal + camera-direction gizmos to make offset tuning exact.")]
    public bool debugDrawPalm = false;

    [Tooltip("Manually-resolved bones — leave blank to auto-resolve at Start.")]
    public Transform rightShoulder, rightElbow, rightHand;
    public Transform leftShoulder, leftElbow, leftHand;

    [Tooltip("Verbose per-resolve logging.")]
    public bool debugLog = false;

    // ── State ────────────────────────────────────────────────────────────────
    Animator _anim;
    bool _bonesResolved;

    void Awake()
    {
        _anim = GetComponent<Animator>();
    }

    void Start()
    {
        ResolveBones();
    }

    /// <summary>Auto-find the 6 arm bones (shoulder/elbow/hand × 2). Tries the
    /// Humanoid map first, then bone-name search. Idempotent.</summary>
    public void ResolveBones()
    {
        if (_anim == null) _anim = GetComponent<Animator>();
        if (_anim != null && _anim.avatar != null && _anim.avatar.isHuman)
        {
            if (rightShoulder == null) rightShoulder = _anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (rightElbow    == null) rightElbow    = _anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (rightHand     == null) rightHand     = _anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (leftShoulder  == null) leftShoulder  = _anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (leftElbow     == null) leftElbow     = _anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (leftHand      == null) leftHand      = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
        }
        // Name-based fallback (RPM clean names / Mixamo-without-prefix)
        if (rightShoulder == null) rightShoulder = FindDeep("RightArm");
        if (rightElbow    == null) rightElbow    = FindDeep("RightForeArm");
        if (rightHand     == null) rightHand     = FindDeep("RightHand");
        if (leftShoulder  == null) leftShoulder  = FindDeep("LeftArm");
        if (leftElbow     == null) leftElbow     = FindDeep("LeftForeArm");
        if (leftHand      == null) leftHand      = FindDeep("LeftHand");

        _bonesResolved = rightShoulder && rightElbow && rightHand
                      && leftShoulder  && leftElbow  && leftHand;
        Debug.Log($"[ASL-ProcIK] Bones resolved: {_bonesResolved} " +
                  $"(R: {rightShoulder?.name}/{rightElbow?.name}/{rightHand?.name}, " +
                  $"L: {leftShoulder?.name}/{leftElbow?.name}/{leftHand?.name})");
    }

    void LateUpdate()
    {
        if (!_bonesResolved || weight <= 0f) return;

        if (rightHandTarget != null)
        {
            Vector3 pole = ComposeElbowPole(rightShoulder, rightHandTarget, rightElbowPole, +1f);
            SolveArm(rightShoulder, rightElbow, rightHand, rightHandTarget, pole, rightHandRotationOffset);
        }
        if (leftHandTarget != null)
        {
            // For the LEFT arm, sideSign = -1 naturally mirrors X in the pole composition,
            // so no separate offset vector is needed (leftMirrorsRight is implicit).
            Vector3 pole = ComposeElbowPole(leftShoulder, leftHandTarget, rightElbowPole, -1f);
            SolveArm(leftShoulder, leftElbow, leftHand, leftHandTarget, pole, leftHandRotationOffset);
        }
    }

    /// <summary>
    /// Compose the elbow pole position for one arm. Body-local "down-out-back" base offset
    /// from the shoulder PLUS a stateless anatomical follow term that lifts and abducts
    /// the elbow as the hand rises above the shoulder. Pure function of (shoulder, target).
    /// </summary>
    /// <param name="shoulder">Upper-arm bone (pole anchor).</param>
    /// <param name="target">Hand target Transform (driver of the follow term).</param>
    /// <param name="baseOffset">Body-local pole offset at rest (the legacy "ElbowHintOffset").</param>
    /// <param name="sideSign">+1 for right arm, -1 for left arm (mirrors X).</param>
    Vector3 ComposeElbowPole(Transform shoulder, Transform target, Vector3 baseOffset, float sideSign)
    {
        // 1 — Legacy base pole, body-local, anchored at the shoulder.
        Vector3 basePole = shoulder.position + transform.rotation *
            new Vector3(sideSign * baseOffset.x, baseOffset.y, baseOffset.z);

        // 2 — Body-local Y of the hand target above the shoulder. Negative if hand is below.
        Vector3 toTargetBody = Quaternion.Inverse(transform.rotation) * (target.position - shoulder.position);
        float lift = Mathf.Max(0f, toTargetBody.y);   // metres above shoulder (≤ ~0.5 m for face-level signs)

        // 3 — Follow term: as the hand rises the elbow swings OUT (sideways) AND UP,
        //     and pulls a little forward (reducing the "back" component). Stateless — pure
        //     function of `lift`, so no oscillation, no drift, matches the wrist solver.
        Vector3 follow = new Vector3(sideSign * elbowAbductFollow * lift,
                                                elbowHeightFollow * lift,
                                          0.5f * elbowHeightFollow * lift);

        return basePole + transform.rotation * follow;
    }

    /// <summary>Solve one arm (position + optional wrist orientation) with a SINGLE weight blend.
    /// Snapshots the three bones' local rotations, solves at full weight, then blends back by
    /// <see cref="weight"/> — so position and orientation share one consistent blend (no double-blend).</summary>
    void SolveArm(Transform root, Transform mid, Transform tip, Transform target,
                  Vector3 pole, Vector3 handOffsetEuler)
    {
        Quaternion rootPre = root.localRotation;
        Quaternion midPre  = mid.localRotation;
        Quaternion tipPre  = tip.localRotation;

        SolveTwoBone(root, mid, tip, target.position, pole);            // position, full weight
        if (driveHandRotation)
            ApplyWristOrientation(mid, tip, target.rotation, handOffsetEuler, forearmFollow);

        if (weight < 0.999f)
        {
            root.localRotation = Quaternion.Slerp(rootPre, root.localRotation, weight);
            mid.localRotation  = Quaternion.Slerp(midPre,  mid.localRotation,  weight);
            tip.localRotation  = Quaternion.Slerp(tipPre,  tip.localRotation,  weight);
        }
    }

    /// <summary>Body-local pole offset → world position, anchored at the given shoulder.</summary>
    Vector3 PoleWorld(Transform shoulder, Vector3 bodyLocalOffset, bool mirrorX)
    {
        Vector3 origin = shoulder != null ? shoulder.position : transform.position;
        Vector3 ofs = mirrorX ? new Vector3(-bodyLocalOffset.x, bodyLocalOffset.y, bodyLocalOffset.z) : bodyLocalOffset;
        return origin + transform.rotation * ofs;
    }

    /// <summary>
    /// Analytic two-joint IK (Daniel Holden). Writes <c>root.localRotation</c> and
    /// <c>mid.localRotation</c> so the tip lands on <paramref name="target"/>
    /// (clamped to reach), with the elbow biased toward <paramref name="pole"/>.
    /// One-shot exact: a single call reaches the target — no iteration needed.
    /// Always solves at FULL weight; the caller (<see cref="SolveArm"/>) owns weight blending
    /// so position + wrist orientation share one consistent blend.
    /// </summary>
    public static void SolveTwoBone(Transform root, Transform mid, Transform tip,
                                    Vector3 target, Vector3 pole)
    {
        Vector3 a = root.position;
        Vector3 b = mid.position;
        Vector3 c = tip.position;

        float lab = (b - a).magnitude;                 // upper-arm length
        float lcb = (c - b).magnitude;                 // forearm length
        if (lab < 1e-5f || lcb < 1e-5f) return;

        float reach = lab + lcb;
        float lat   = Mathf.Clamp((target - a).magnitude, 1e-4f, reach - 1e-4f);

        // Current interior angles (radians).
        float ac_ab_0 = SafeAcos(Vector3.Dot((c - a).normalized, (b - a).normalized));
        float ba_bc_0 = SafeAcos(Vector3.Dot((a - b).normalized, (c - b).normalized));
        float ac_at_0 = SafeAcos(Vector3.Dot((c - a).normalized, (target - a).normalized));

        // Desired interior angles (law of cosines).
        float ac_ab_1 = SafeAcos((lab * lab + lat * lat - lcb * lcb) / (2f * lab * lat));
        float ba_bc_1 = SafeAcos((lab * lab + lcb * lcb - lat * lat) / (2f * lab * lcb));

        // Bend axis = normal of the current (a,b,c) triangle plane.
        Vector3 axis0 = Vector3.Cross(c - a, b - a);
        if (axis0.sqrMagnitude < 1e-8f)               // arm is straight → use pole to pick a plane
            axis0 = Vector3.Cross(c - a, pole - a);
        if (axis0.sqrMagnitude < 1e-8f)
            axis0 = Vector3.Cross(c - a, Vector3.up);
        axis0.Normalize();

        // Axis to swing the whole chain so the tip direction matches the target direction.
        Vector3 axis1 = Vector3.Cross(c - a, target - a);
        if (axis1.sqrMagnitude < 1e-8f) axis1 = axis0; else axis1.Normalize();

        // Bring world axes into each bone's LOCAL space (post-multiply onto localRotation).
        Quaternion r0 = Quaternion.AngleAxis((ac_ab_1 - ac_ab_0) * Mathf.Rad2Deg,
                            Quaternion.Inverse(root.rotation) * axis0);
        Quaternion r1 = Quaternion.AngleAxis((ba_bc_1 - ba_bc_0) * Mathf.Rad2Deg,
                            Quaternion.Inverse(mid.rotation) * axis0);
        Quaternion r2 = Quaternion.AngleAxis(ac_at_0 * Mathf.Rad2Deg,
                            Quaternion.Inverse(root.rotation) * axis1);

        root.localRotation = root.localRotation * r0 * r2;
        mid.localRotation  = mid.localRotation * r1;

        // Pole step: rotate the solved arm about the shoulder→target axis so the
        // elbow points toward the pole hint (natural "down & back" elbow).
        Vector3 axisT = (target - a).normalized;
        Vector3 elbowNow = mid.position;
        Vector3 projElbow = Vector3.ProjectOnPlane(elbowNow - a, axisT);
        Vector3 projPole  = Vector3.ProjectOnPlane(pole - a, axisT);
        if (projElbow.sqrMagnitude > 1e-6f && projPole.sqrMagnitude > 1e-6f)
        {
            Quaternion poleRot = Quaternion.FromToRotation(projElbow, projPole);
            // Rotate root (and its children) about point a in world space.
            root.rotation = poleRot * root.rotation;
        }
    }

    /// <summary>
    /// Orient the wrist/palm: distribute the palm ROLL into the forearm (pronation about the
    /// elbow→wrist axis) — that's the joint that does it in a real arm — then pin the hand bone
    /// to the exact desired orientation. Stateless / closed-form ⇒ a stable per-frame fixed point
    /// (no drift, no smearing). Position is preserved because the roll axis passes through the
    /// elbow pivot and the wrist lies on it.
    /// </summary>
    /// <param name="mid">Forearm bone (pivot = elbow).</param>
    /// <param name="tip">Hand bone (wrist lies on the roll axis).</param>
    /// <param name="targetRotation">World rotation carrying the authored wrist orientation.</param>
    /// <param name="handLocalAxisOffsetEuler">Euler° (target-frame) correcting the RPM hand-bone axis convention.</param>
    /// <param name="forearmFollow">0..1 fraction of the roll taken by the forearm (1 = all forearm).</param>
    static void ApplyWristOrientation(Transform mid, Transform tip,
        Quaternion targetRotation, Vector3 handLocalAxisOffsetEuler, float forearmFollow)
    {
        Quaternion wantHand = targetRotation * Quaternion.Euler(handLocalAxisOffsetEuler);

        Vector3 axisRaw = tip.position - mid.position;        // elbow → wrist
        float len = axisRaw.magnitude;
        if (len < 1e-6f) { tip.rotation = wantHand; return; } // degenerate limb guard
        Vector3 axis = axisRaw / len;

        // World-frame delta forearm→desired-hand; keep only its ROLL about the limb axis.
        Quaternion delta = wantHand * Quaternion.Inverse(mid.rotation);
        Quaternion roll  = ExtractTwist(delta, axis);
        Quaternion forearmRoll = (forearmFollow >= 0.999f)
            ? roll
            : Quaternion.Slerp(Quaternion.identity, roll, forearmFollow);

        // Pre-multiply (world) rotates the forearm basis about 'axis' through the elbow → wrist unmoved.
        mid.rotation = forearmRoll * mid.rotation;
        // Pin the hand to the exact orientation; residual wrist bend is now small (no snap).
        tip.rotation = wantHand;
    }

    /// <summary>Swing-twist: the pure "twist" (roll) component of <paramref name="q"/> about unit
    /// <paramref name="axis"/>. Singularity-safe (≈180°) and forces the short-way result.</summary>
    static Quaternion ExtractTwist(Quaternion q, Vector3 axis)
    {
        Vector3 v = new Vector3(q.x, q.y, q.z);
        Vector3 proj = Vector3.Dot(v, axis) * axis;
        float sqr = proj.sqrMagnitude + q.w * q.w;
        if (sqr < 1e-12f) return Quaternion.identity;       // ≈180° singular case → no roll
        float inv = 1f / Mathf.Sqrt(sqr);
        Quaternion t = new Quaternion(proj.x * inv, proj.y * inv, proj.z * inv, q.w * inv);
        if (t.w < 0f) { t.x = -t.x; t.y = -t.y; t.z = -t.z; t.w = -t.w; } // short way
        return t;
    }

    static float SafeAcos(float x) => Mathf.Acos(Mathf.Clamp(x, -1f, 1f));

    /// <summary>Recursive depth-first transform search by exact (case-insensitive) name.</summary>
    Transform FindDeep(string name) => FindDeepRec(transform, name);
    static Transform FindDeepRec(Transform t, string name)
    {
        if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase)) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeepRec(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

#if UNITY_EDITOR
    /// <summary>Tuning aid: draw each hand bone's local axes (R=red/X, G=green/Y, B=blue/Z) so you can
    /// see which one is the palm normal, plus a yellow ray toward the camera. Align the palm-normal
    /// axis with the yellow ray by dialing the rotation offset.</summary>
    void OnDrawGizmos()
    {
        if (!debugDrawPalm) return;
        DrawHandAxes(rightHand);
        DrawHandAxes(leftHand);
    }

    static void DrawHandAxes(Transform hand)
    {
        if (hand == null) return;
        const float L = 0.12f;
        Vector3 p = hand.position;
        Gizmos.color = Color.red;   Gizmos.DrawLine(p, p + hand.right   * L);
        Gizmos.color = Color.green; Gizmos.DrawLine(p, p + hand.up      * L);
        Gizmos.color = Color.blue;  Gizmos.DrawLine(p, p + hand.forward * L);
        var cam = Camera.main;
        if (cam != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(p, p + (cam.transform.position - p).normalized * L);
        }
    }
#endif
}
