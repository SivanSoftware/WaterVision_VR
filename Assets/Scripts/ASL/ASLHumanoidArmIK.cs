using UnityEngine;

/// <summary>
/// Drives the avatar's hands via Unity's built-in Humanoid IK solver (the same
/// pattern <c>XRAvatarIK_RPM</c> uses in this project). Replaces the failed
/// Animation Rigging <c>TwoBoneIKConstraint</c> approach which is documented
/// as unreliable on Generic avatars (Unity Issue Tracker).
///
/// Requirements:
///   1. The <c>Animator</c> on this GameObject MUST have a Humanoid Avatar
///      (built via <c>AvatarBuilder.BuildHumanAvatar()</c> for RPM avatars
///      which import as Generic via glTFast).
///   2. The Animator's runtimeAnimatorController MUST have IK Pass enabled
///      on the layer that drives the body — otherwise Unity does NOT call
///      <see cref="OnAnimatorIK"/>.
///
/// Usage:
///   Attach to the avatar root. <see cref="ASLSigningCharacterManager"/> finds
///   this component at runtime and assigns <see cref="rightHandTarget"/> +
///   <see cref="leftHandTarget"/> Transforms that move with sign keyframes.
/// </summary>
[RequireComponent(typeof(Animator))]
public class ASLHumanoidArmIK : MonoBehaviour
{
    /// <summary>World-space target for the right hand. Set by ASLSigningCharacterManager.</summary>
    public Transform rightHandTarget;

    /// <summary>World-space target for the left hand. Set by ASLSigningCharacterManager.</summary>
    public Transform leftHandTarget;

    [Range(0f, 1f)]
    [Tooltip("IK blend weight. 0 = no IK (pose from controller). 1 = hand at target.")]
    public float weight = 1f;

    [Tooltip("Also pin the rotation of the wrist to the target's rotation.")]
    public bool driveRotation = true;

    [Range(0f, 1f)]
    [Tooltip("Elbow hint weight. Above 0, IK is pulled toward a 'natural human' " +
             "elbow position (behind+below shoulder). Prevents elbows from flipping " +
             "forward/upward into unnatural poses.")]
    public float elbowHintWeight = 0.6f;

    [Tooltip("Elbow hint offset in BODY-LOCAL space, relative to the upper-arm bone. " +
             "Negative Z = behind, negative Y = below. Right elbow uses these, left mirrors X.")]
    public Vector3 rightElbowHintOffset = new Vector3(0.10f, -0.25f, -0.30f);

    Animator _anim;

    // Log once for the very first call (whatever the target state),
    // and once again the first time the target becomes non-null.
    bool _loggedFirstCall = false;
    bool _loggedFirstWithTarget = false;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        if (_anim != null && !_anim.isHuman)
        {
            Debug.LogWarning("[ASL-ArmIK] Animator's Avatar is not Humanoid — built-in Humanoid IK requires Humanoid. " +
                             "Run Tools/ASL/Build RPM Signing Prefab to (re)build with a Humanoid Avatar.");
        }
    }

    void OnAnimatorIK(int layerIndex)
    {
        var handBone = _anim != null ? _anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (!_loggedFirstCall)
        {
            _loggedFirstCall = true;
            Debug.Log($"[ASL-ArmIK] FIRST OnAnimatorIK call — layer={layerIndex}, " +
                      $"_anim={(_anim != null ? "OK" : "NULL")}, isHuman={_anim?.isHuman}, " +
                      $"weight={weight}, " +
                      $"rightTarget={(rightHandTarget != null ? rightHandTarget.position.ToString("F3") : "NULL")}, " +
                      $"RightHand bone={(handBone != null ? handBone.position.ToString("F3") : "NULL")}");
        }
        if (!_loggedFirstWithTarget && rightHandTarget != null)
        {
            _loggedFirstWithTarget = true;
            Debug.Log($"[ASL-ArmIK] FIRST OnAnimatorIK with non-null target — " +
                      $"target world={rightHandTarget.position:F3}, " +
                      $"RightHand bone={(handBone != null ? handBone.position.ToString("F3") : "NULL")}, " +
                      $"weight={weight}");
        }
        if (_anim == null || !_anim.isHuman) return;

        if (rightHandTarget != null)
        {
            _anim.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            _anim.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget.position);
            if (driveRotation)
            {
                _anim.SetIKRotationWeight(AvatarIKGoal.RightHand, weight);
                _anim.SetIKRotation(AvatarIKGoal.RightHand, rightHandTarget.rotation);
            }
            // Elbow hint — IK picks unnatural elbow direction without this on RPM.
            ApplyElbowHint(AvatarIKHint.RightElbow, HumanBodyBones.RightUpperArm, mirrorX: false);
        }

        if (leftHandTarget != null)
        {
            _anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            _anim.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget.position);
            if (driveRotation)
            {
                _anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight);
                _anim.SetIKRotation(AvatarIKGoal.LeftHand, leftHandTarget.rotation);
            }
            ApplyElbowHint(AvatarIKHint.LeftElbow, HumanBodyBones.LeftUpperArm, mirrorX: true);
        }
    }

    /// <summary>Place an IK elbow hint behind+below the named upper-arm bone, in
    /// body-local space. This keeps the elbow pointing in the natural human
    /// direction during IK solve, preventing weird "elbow up/forward" poses.</summary>
    void ApplyElbowHint(AvatarIKHint hint, HumanBodyBones upperArmBone, bool mirrorX)
    {
        if (elbowHintWeight <= 0f) return;
        var upper = _anim.GetBoneTransform(upperArmBone);
        if (upper == null) return;
        Vector3 offset = rightElbowHintOffset;
        if (mirrorX) offset.x = -offset.x;
        Vector3 hintPos = upper.position + transform.rotation * offset;
        _anim.SetIKHintPositionWeight(hint, elbowHintWeight);
        _anim.SetIKHintPosition(hint, hintPos);
    }
}
