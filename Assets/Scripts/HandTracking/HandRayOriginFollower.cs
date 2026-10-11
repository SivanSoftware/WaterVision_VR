// HandRayOriginFollower — pins a hand far-ray cast/line origin to the live tracked hand.
//
// PROBLEM (hand-tracking): the NearFarInteractor far-ray origin ("Aim Pose" transforms under the Left/Right
//   hand interactors) is driven by a RotationOnly TrackedPoseDriver (aim direction) but gets NO reliable
//   POSITION, so the origin stays parked at the rig root (~ the player's feet) while rotating correctly =>
//   "laser from the feet, aimed about right."
//
// FIX: drive the origin's WORLD POSITION ourselves from the live XR Hands skeleton, leaving ROTATION to the
//   existing aim driver. We resolve the hand DIRECTLY from the XRHandSubsystem (Palm joint, Wrist fallback) and
//   convert its tracking-space pose to WORLD via the XR Origin's Camera Offset — exactly the approach
//   AvatarModalityIKBinder.UpdateHandsWinModality() uses.
//
//   We deliberately do NOT read the XRHandSkeletonDriver joint Transform anymore: that Transform FREEZES
//   whenever the XRInputModalityManager deactivates the hand branch (it can stick in MotionController mode),
//   which is exactly what left this origin at the feet. Sampling the subsystem directly + converting ourselves
//   is independent of the modality manager and never freezes. NOTE: raw subsystem poses are in TRACKING space —
//   using them without the Camera-Offset conversion below has bitten this project before, so that conversion is
//   mandatory (it is the same one ResolveTrackingSpace performs in AvatarModalityIKBinder).
//
// We write the position in BOTH LateUpdate AND onBeforeRender so the origin is correct no matter which phase the
// line visual / caster samples: onBeforeRender [BeforeRenderOrder 100] runs immediately before
// XRInteractorLineVisual's onBeforeRender [101]; LateUpdate is a guaranteed-runs safety net.
//
// Attach one per hand to the far cast-origin "Aim Pose" transform (also the line visual's m_LineOriginTransform
// and the CurveInteractionCaster's m_CastOrigin):
//   Left  -> fileID 6409288253826840075  (m_Hand = Left)
//   Right -> fileID 5262580049104647543  (m_Hand = Right)

using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

[DefaultExecutionOrder(200)]
public class HandRayOriginFollower : MonoBehaviour
{
    public enum HandSide { Left, Right }

    [Header("Hand selection")]
    [Tooltip("Which hand this origin follows. Left -> the left 'Aim Pose' (fileID 6409288253826840075); " +
             "Right -> the right 'Aim Pose' (fileID 5262580049104647543).")]
    [SerializeField] private HandSide m_Hand = HandSide.Right;

    [Header("Behaviour")]
    [Tooltip("When the hand can't be resolved (hand not tracked / no running XR Hands subsystem), leave the " +
             "transform where it is instead of moving it. The hand far-ray only matters in hand-tracking.")]
    [SerializeField] private bool m_HoldWhenUnresolved = true;

    [Header("Debug")]
    [Tooltip("Throttled logs of resolution + the world position written. Leave on until verified in-headset.")]
    [SerializeField] private bool m_DebugLog = true;

    // Resolved straight from the XR Hands subsystem + converted to world via the XR Origin Camera Offset —
    // independent of XRInputModalityManager and the (freezable) XRHandSkeletonDriver.
    private static readonly List<XRHandSubsystem> s_SubsystemScratch = new List<XRHandSubsystem>();
    private XRHandSubsystem m_HandSubsystem;
    private XROrigin m_XROrigin;
    private string m_JointLabel = "none";

    private bool m_Registered;
    private int m_FrameCounter;

    private void OnEnable()
    {
        Application.onBeforeRender += OnBeforeRender;
        m_Registered = true;

        if (m_DebugLog)
            Debug.Log($"[{nameof(HandRayOriginFollower)}] ({m_Hand}) OnEnable — running; resolves the hand via " +
                      $"XRHandSubsystem (independent of the modality manager / skeleton driver).", this);
    }

    private void OnDisable()
    {
        if (m_Registered)
        {
            Application.onBeforeRender -= OnBeforeRender;
            m_Registered = false;
        }
    }

    // Safety-net write (always runs while active). onBeforeRender is the low-latency, render-accurate one.
    private void LateUpdate() => ApplyOrigin();

    [BeforeRenderOrder(100)] // immediately before XRInteractorLineVisual's onBeforeRender (101)
    private void OnBeforeRender()
    {
        if (isActiveAndEnabled) ApplyOrigin();
    }

    private void ApplyOrigin()
    {
        if (!TryResolveHandWorldPosition(out Vector3 worldPos))
        {
            // Unresolved -> hold (leave the transform alone). If this keeps firing in hand-tracking, the hand
            // isn't tracked or no XR Hands subsystem is running — NOT the modality manager / driver anymore.
            if (m_DebugLog && (m_FrameCounter++ % 120) == 0)
                Debug.Log($"[{nameof(HandRayOriginFollower)}] ({m_Hand}) hand UNRESOLVED — origin held at parent " +
                          $"(the 'feet' state). hold={m_HoldWhenUnresolved}.", this);
            return;
        }

        // WORLD position only. Rotation stays with the aim TrackedPoseDriver (RotationOnly), preserving the
        // correct aim direction and never fighting it.
        transform.position = worldPos;

        if (m_DebugLog && (m_FrameCounter++ % 120) == 0)
            Debug.Log($"[{nameof(HandRayOriginFollower)}] ({m_Hand}) origin -> {m_JointLabel} world {worldPos} " +
                      $"(y={worldPos.y:F2}).", this);
    }

    // Live WORLD-space hand origin (Palm preferred, Wrist fallback), sampled straight from the XRHandSubsystem
    // and converted tracking-space -> world via the XR Origin Camera Offset. Mirrors
    // AvatarModalityIKBinder.UpdateHandsWinModality() / ResolveTrackingSpace() — the project's known-good path.
    private bool TryResolveHandWorldPosition(out Vector3 worldPos)
    {
        worldPos = default;

        // Lazily acquire a running XR Hands subsystem (re-acquire if the cached one stopped).
        if (m_HandSubsystem == null || !m_HandSubsystem.running)
        {
            m_HandSubsystem = null;
            SubsystemManager.GetSubsystems(s_SubsystemScratch);
            for (int i = 0; i < s_SubsystemScratch.Count; i++)
                if (s_SubsystemScratch[i] != null && s_SubsystemScratch[i].running) { m_HandSubsystem = s_SubsystemScratch[i]; break; }
            if (m_HandSubsystem == null) return false;
        }

        XRHand hand = m_Hand == HandSide.Left ? m_HandSubsystem.leftHand : m_HandSubsystem.rightHand;
        if (!hand.isTracked) return false;

        // Tracking-space pose: Palm (natural ray origin), Wrist fallback (always present; used by the wrist menu).
        Pose pose;
        if (hand.GetJoint(XRHandJointID.Palm).TryGetPose(out pose)) m_JointLabel = "Palm";
        else if (hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out pose)) m_JointLabel = "Wrist";
        else return false;

        Transform track = ResolveTrackingSpace();
        if (track == null) return false;

        worldPos = track.TransformPoint(pose.position); // tracking-space -> WORLD (mandatory conversion)
        return true;
    }

    // The rig's tracking-space transform — the space XR Hands joint poses are reported in. Tracked devices are
    // children of the Camera Offset, so that is the tracking space (falls back to the XR Origin root).
    private Transform ResolveTrackingSpace()
    {
        if (m_XROrigin == null)
        {
            m_XROrigin = FindFirstObjectByType<XROrigin>();
            if (m_XROrigin == null) return null;
        }
        return m_XROrigin.CameraFloorOffsetObject != null
            ? m_XROrigin.CameraFloorOffsetObject.transform
            : m_XROrigin.transform;
    }
}
