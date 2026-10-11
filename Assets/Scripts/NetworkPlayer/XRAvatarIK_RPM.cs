using Unity.Netcode;
using UnityEngine;

namespace XRMultiplayer
{
    /// <summary>
    /// XRAvatarIK with Ready Player Me support
    /// Handles head tracking, body rotation, and optional arm IK
    /// </summary>
    public class XRAvatarIK_RPM : MonoBehaviour
    {
        [Header("Transform References")]
        [SerializeField, Tooltip("Transform for the Network Player Head")]
        Transform m_HeadTransform;

        [SerializeField, Tooltip("Torso Parent Transform (Spine/Hips)")]
        Transform m_TorsoParentTransform;

        [SerializeField, Tooltip("Root of the Head Visuals")]
        Transform m_HeadVisualsRoot;

        [SerializeField, Tooltip("Neck Transform")]
        Transform m_Neck;

        [Header("Ready Player Me Settings")]
        [SerializeField, Tooltip("RPM Avatar Root GameObject (with Animator)")]
        GameObject m_RPMAvatarRoot;

        [SerializeField, Tooltip("Auto-detect RPM bones on Start")]
        bool m_AutoDetectBones = true;

        [SerializeField, Tooltip("Use Animator IK for arms")]
        bool m_UseAnimatorIK = true;

        [SerializeField, Tooltip("Left hand target for IK")]
        Transform m_LeftHandTarget;

        [SerializeField, Tooltip("Right hand target for IK")]
        Transform m_RightHandTarget;

        [Header("IK Settings")]
        [SerializeField, Tooltip("Offset to be applied to the head height")]
        float m_HeadHeightOffset = 0.3f;

        [Range(0, 360.0f)]
        [SerializeField, Tooltip("Threshold to where body rotation approximation is applied")]
        float m_RotateThreshold = 25.0f;

        [SerializeField, Tooltip("Speed at which the body rotates")]
        float m_RotateSpeed = 3.0f;

        [SerializeField, Tooltip("IK weight for left hand (0-1)")]
        [Range(0, 1)]
        float m_LeftHandIKWeight = 1f;

        [SerializeField, Tooltip("IK weight for right hand (0-1)")]
        [Range(0, 1)]
        float m_RightHandIKWeight = 1f;

        [SerializeField, Tooltip("Look at weight for head")]
        [Range(0, 1)]
        float m_LookAtWeight = 0.5f;

        // Private variables
        Transform m_Transform;
        Animator m_Animator;
        float m_DestinationY;

        // RPM Bone references
        Transform m_SpineBone;
        Transform m_ChestBone;
        Transform m_HipsBone;

        // Cached NetworkObject for ownership checks. On remote-player instances IsOwner will
        // be false and Update / OnAnimatorIK will early-out — letting NetworkTransform drive
        // the remote avatar instead of fighting it with locally-computed IK writes (this was
        // the real cause of the remote-avatar flicker).
        NetworkObject m_NetObj;

        // One-time diagnostic flag (temporary — remove after flicker is confirmed fixed).
        bool m_FlickerDiagLogged;

        private void Start()
        {
            m_Transform = transform;
            m_NetObj = GetComponentInParent<NetworkObject>();

            // Setup RPM Avatar
            if (m_AutoDetectBones)
            {
                SetupRPMAvatar();
            }

            if (m_HeadTransform != null)
            {
                m_DestinationY = m_HeadTransform.eulerAngles.y;
            }
        }

        /// <summary>
        /// Auto-detect and setup Ready Player Me avatar bones
        /// </summary>
        void SetupRPMAvatar()
        {
            m_Animator = GetComponentInChildren<Animator>();
            
            if (m_Animator != null && m_Animator.isHuman)
            {
                // AUTOMATIC ASSIGNMENT: Finds bones without manual Inspector setup
                if (m_TorsoParentTransform == null) m_TorsoParentTransform = m_Animator.GetBoneTransform(HumanBodyBones.Hips);
                if (m_Neck == null) m_Neck = m_Animator.GetBoneTransform(HumanBodyBones.Neck);
                
                // FIX ISSUE 1: Assign the Head Mesh so it's visible on other players
                if (m_HeadVisualsRoot == null) m_HeadVisualsRoot = m_Animator.GetBoneTransform(HumanBodyBones.Head);
                
                m_HipsBone = m_Animator.GetBoneTransform(HumanBodyBones.Hips);
            }
        }

        private void Update()
        {
            // TEMP DIAGNOSTIC (one log per avatar instance), placed BEFORE any early-out so it
            // always fires. Confirms whether the IsOwner guard is active. Search Console for
            // "FLICKER-DIAG".
            //  - Local (your) avatar   → "RUNS IK (owner)"
            //  - Remote (other) avatar → "SKIPS IK (remote)"  ← this is what fixes the flicker
            //  - "NetObj=NULL" → guard NOT working (no NetworkObject found) = the bug to chase
            //  - "HeadTransform=NULL" → IK never runs anyway (different problem)
            if (!m_FlickerDiagLogged)
            {
                m_FlickerDiagLogged = true;
                string netState = m_NetObj != null ? $"FOUND IsOwner={m_NetObj.IsOwner}" : "NULL";
                bool willRun = (m_NetObj == null || m_NetObj.IsOwner) && m_HeadTransform != null;
                Debug.Log($"🩺 [FLICKER-DIAG] XRAvatarIK_RPM.Update on '{name}': NetObj={netState}, " +
                          $"HeadTransform={(m_HeadTransform != null ? "set" : "NULL")} → " +
                          (willRun ? "RUNS IK (owner)" : "SKIPS IK (remote or no head)"));
            }

            if (m_HeadTransform == null) return;

            // On remote replicas NetworkTransform syncs the body/head from the owner —
            // running our local IK math here would overwrite the synced values every
            // frame and produce visible jitter.
            if (m_NetObj != null && !m_NetObj.IsOwner) return;

            UpdateHead();
            UpdateBody();
        }

        /// <summary>
        /// Update head position and rotation
        /// </summary>
        void UpdateHead()
        {
            if (m_HeadVisualsRoot == null) return;

            // Position head with offset
            m_HeadVisualsRoot.position = m_HeadTransform.position - (m_HeadTransform.up * m_HeadHeightOffset);

            // Rotate neck to match head
            if (m_Neck != null)
            {
                m_Neck.rotation = m_HeadTransform.rotation;
            }

            // Update scale
            m_HeadVisualsRoot.localScale = m_HeadTransform.localScale;
        }

        /// <summary>
        /// Update body position and rotation
        /// </summary>
        void UpdateBody()
        {
            if (m_TorsoParentTransform == null || m_HeadTransform == null) return;

            // Fix position without causing physics flying.
            // (Owner-only: Update() early-outs on remote replicas via the IsOwner guard, so
            // this no longer fights NetworkTransform on the remote — that fight, where the
            // root was dragged toward the head bone every frame, was the real flicker cause.)
            m_Transform.position = m_HeadTransform.position;

            // Smooth body rotation
            Quaternion targetRotation = Quaternion.Euler(0, m_DestinationY, 0);
            m_TorsoParentTransform.rotation = Quaternion.Slerp(
                m_TorsoParentTransform.rotation, 
                targetRotation, 
                Time.deltaTime * m_RotateSpeed
            );

            // Check if body should rotate to follow head
            float angleDifference = Mathf.Abs(
                Mathf.DeltaAngle(m_TorsoParentTransform.eulerAngles.y, m_HeadTransform.eulerAngles.y)
            );

            if (angleDifference >= m_RotateThreshold)
            {
                m_DestinationY = m_HeadTransform.eulerAngles.y;
            }
        }

        /// <summary>
        /// Unity Animator IK callback for hand tracking
        /// </summary>
        void OnAnimatorIK(int layerIndex)
        {
            if (!m_UseAnimatorIK || m_Animator == null) return;
            if (m_NetObj != null && !m_NetObj.IsOwner) return;

            // Left Hand IK
            if (m_LeftHandTarget != null)
            {
                m_Animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, m_LeftHandIKWeight);
                m_Animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, m_LeftHandIKWeight);
                m_Animator.SetIKPosition(AvatarIKGoal.LeftHand, m_LeftHandTarget.position);
                m_Animator.SetIKRotation(AvatarIKGoal.LeftHand, m_LeftHandTarget.rotation);
            }

            // Right Hand IK
            if (m_RightHandTarget != null)
            {
                m_Animator.SetIKPositionWeight(AvatarIKGoal.RightHand, m_RightHandIKWeight);
                m_Animator.SetIKRotationWeight(AvatarIKGoal.RightHand, m_RightHandIKWeight);
                m_Animator.SetIKPosition(AvatarIKGoal.RightHand, m_RightHandTarget.position);
                m_Animator.SetIKRotation(AvatarIKGoal.RightHand, m_RightHandTarget.rotation);
            }

            // Look At (optional)
            if (m_HeadTransform != null && m_LookAtWeight > 0)
            {
                Vector3 lookAtPosition = m_HeadTransform.position + m_HeadTransform.forward * 2f;
                m_Animator.SetLookAtWeight(m_LookAtWeight, 0.3f, 1f, 0f, 0.5f);
                m_Animator.SetLookAtPosition(lookAtPosition);
            }
        }

        /// <summary>
        /// Set hand targets for IK (called from XRINetworkPlayer or XRHandPoseReplicator)
        /// </summary>
        public void SetHandTargets(Transform leftHand, Transform rightHand)
        {
            m_LeftHandTarget = leftHand;
            m_RightHandTarget = rightHand;
            Debug.Log("XRAvatarIK: Hand targets set for IK");
        }

        /// <summary>
        /// Set head transform (called from XRINetworkPlayer)
        /// </summary>
        public void SetHeadTransform(Transform head)
        {
            m_HeadTransform = head;
            if (head != null)
            {
                m_DestinationY = head.eulerAngles.y;
            }
        }

        /// <summary>
        /// Enable/disable Animator IK at runtime
        /// </summary>
        public void SetAnimatorIKEnabled(bool enabled)
        {
            m_UseAnimatorIK = enabled;
        }

        /// <summary>
        /// Set IK weights for hands
        /// </summary>
        public void SetHandIKWeights(float leftWeight, float rightWeight)
        {
            m_LeftHandIKWeight = Mathf.Clamp01(leftWeight);
            m_RightHandIKWeight = Mathf.Clamp01(rightWeight);
        }

        /// <summary>
        /// Set body rotation speed
        /// </summary>
        public void SetRotationSpeed(float speed)
        {
            m_RotateSpeed = Mathf.Max(0, speed);
        }

        /// <summary>
        /// Set body rotation threshold
        /// </summary>
        public void SetRotationThreshold(float threshold)
        {
            m_RotateThreshold = Mathf.Clamp(threshold, 0, 360);
        }

        /// <summary>
        /// Set head height offset
        /// </summary>
        public void SetHeadHeightOffset(float offset)
        {
            m_HeadHeightOffset = offset;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Draw debug gizmos
        /// </summary>
        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            // Draw head position
            if (m_HeadTransform != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(m_HeadTransform.position, 0.1f);
                Gizmos.DrawLine(m_HeadTransform.position, m_HeadTransform.position + m_HeadTransform.forward * 0.3f);
            }

            // Draw hand targets
            if (m_LeftHandTarget != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(m_LeftHandTarget.position, 0.05f);
            }

            if (m_RightHandTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(m_RightHandTarget.position, 0.05f);
            }

            // Draw body rotation
            if (m_TorsoParentTransform != null)
            {
                Gizmos.color = Color.yellow;
                Vector3 bodyForward = m_TorsoParentTransform.forward;
                bodyForward.y = 0;
                Gizmos.DrawLine(m_TorsoParentTransform.position, m_TorsoParentTransform.position + bodyForward * 0.5f);
            }
        }
#endif
    }
}