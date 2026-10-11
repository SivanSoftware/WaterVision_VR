using UnityEngine;
using Unity.Netcode;

namespace XRMultiplayer
{
    /// <summary>
    /// XRINetworkPlayer with Ready Player Me (RPM) Avatar Support.
    /// Handles XR synchronization and includes a fix for the common 90-degree rotation offset 
    /// often found between RPM models and XR Origin forward vectors.
    /// </summary>
    public class XRINetworkPlayer_RPM : NetworkBehaviour
    {
        public static XRINetworkPlayer_RPM LocalPlayer;

        [Header("Avatar Transform References")]
        public Transform head;
        public Transform leftHand;
        public Transform rightHand;

        // Events for UI and External logic
        public System.Action onSpawnedLocal;
        public System.Action onSpawnedAll;
        public System.Action<Color> onColorUpdated;
        public System.Action<string> onNameUpdated;

        // Player Data & Network synchronization variables
        private float m_PlayerVoiceAmp = 0f;
        public float playerVoiceAmp { get => m_PlayerVoiceAmp; private set => m_PlayerVoiceAmp = value; }

        private string m_PlayerName = "Player";
        public string playerName 
        { 
            get => m_PlayerName; 
            set { m_PlayerName = value; onNameUpdated?.Invoke(value); } 
        }

        private Color m_PlayerColor = Color.white;
        public Color playerColor 
        { 
            get => m_PlayerColor; 
            set { m_PlayerColor = value; onColorUpdated?.Invoke(value); } 
        }

        [Header("Ready Player Me Setup")]
        [SerializeField] protected GameObject m_RPMAvatarRoot;
        [SerializeField] protected bool m_EnableBodyIK = true;
        [SerializeField] protected float m_BodyRotationSpeed = 5f;
        
        [Header("Rotation Fix Settings")]
        [SerializeField, Tooltip("Rotation offset in degrees to align avatar with XR forward (usually -90, 0, or 90)")]
        protected float m_RotationOffsetY = 0f; 
        
        [SerializeField, Tooltip("Apply offset directly to avatar root transform instead of the parent player transform")]
        protected bool m_OffsetAvatarRoot = true;

        [SerializeField] protected Transform m_SpineBone;
        [SerializeField] protected Transform m_HipsBone;

        [Header("Local Player Appearance")]
        [SerializeField, Tooltip("Objects to hide for the local player (e.g., placeholder hands)")]
        protected GameObject[] m_HandsObjects;

        protected Animator m_Animator;
        protected float m_LeftHandIKWeight = 1f;
        protected float m_RightHandIKWeight = 1f;
        protected Transform m_LeftHandOrigin;
        protected Transform m_RightHandOrigin;
        protected Transform m_HeadOrigin;
        protected GameObject m_XRRig;

        protected void Awake()
        {
            SetupRPMAvatar();
        }

        /// <summary>
        /// Locates the RPM avatar, validates the animator, and applies initial rotation corrections.
        /// </summary>
        void SetupRPMAvatar()
        {
            if (m_RPMAvatarRoot == null)
            {
                // Auto-detect avatar by looking for a Humanoid Animator in children
                foreach (Transform child in transform)
                {
                    Animator anim = child.GetComponent<Animator>();
                    if (anim != null && anim.isHuman)
                    {
                        m_RPMAvatarRoot = child.gameObject;
                        Debug.Log($"Auto-detected RPM Avatar: {m_RPMAvatarRoot.name}");
                        break;
                    }
                }
            }

            if (m_RPMAvatarRoot != null)
            {
                m_Animator = m_RPMAvatarRoot.GetComponent<Animator>();
                
                if (m_Animator != null && m_Animator.isHuman)
                {
                    // Cache necessary bone transforms for IK and rotation logic
                    if (m_SpineBone == null) m_SpineBone = m_Animator.GetBoneTransform(HumanBodyBones.Spine);
                    if (m_HipsBone == null) m_HipsBone = m_Animator.GetBoneTransform(HumanBodyBones.Hips);
                    
                    // Fix #1: Apply initial rotation offset to Avatar Root if enabled
                    if (m_OffsetAvatarRoot)
                    {
                        Vector3 currentRot = m_RPMAvatarRoot.transform.localEulerAngles;
                        m_RPMAvatarRoot.transform.localEulerAngles = new Vector3(
                            currentRot.x, 
                            currentRot.y + m_RotationOffsetY, 
                            currentRot.z
                        );
                        Debug.Log($"Applied rotation offset {m_RotationOffsetY}° to avatar root");
                    }
                }
            }
        }

        protected virtual void Update()
        {
            if (IsOwner)
            {
                // Ensure we have a reference to the local XR Camera/Rig
                if (m_HeadOrigin == null)
                {
                    Camera mainCam = Camera.main;
                    if (mainCam != null)
                    {
                        m_HeadOrigin = mainCam.transform;
                        m_XRRig = mainCam.transform.root.gameObject;
                        FindHandControllers();
                    }
                    return;
                }

                // Synchronize player position to match the XR Head (keeping the floor height from the Rig)
                Vector3 newPos = m_HeadOrigin.position;
                newPos.y = (m_XRRig != null) ? m_XRRig.transform.position.y : 0f;
                transform.position = newPos;

                // Synchronize body rotation based on head direction
                if (m_HipsBone != null)
                {
                    UpdateBodyRotation();
                }
            }
        }

        /// <summary>
        /// Fix #2: Calculates and interpolates body rotation with the specified offset.
        /// </summary>
        void UpdateBodyRotation()
        {
            if (m_HeadOrigin == null) return;

            // Project head forward onto the horizontal plane (flatten Y)
            Vector3 headForward = m_HeadOrigin.forward;
            headForward.y = 0;
            
            if (headForward.sqrMagnitude < 0.01f) return;
            headForward.Normalize();

            // Calculate target rotation based on flattened forward vector
            Quaternion targetRotation = Quaternion.LookRotation(headForward, Vector3.up);
            
            // If the offset wasn't applied to the Root transform, apply it to the parent rotation here
            if (!m_OffsetAvatarRoot)
            {
                targetRotation *= Quaternion.Euler(0, m_RotationOffsetY, 0);
            }
            
            // Smoothly rotate the player body towards the target
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRotation, 
                Time.deltaTime * m_BodyRotationSpeed
            );
        }

        protected virtual void LateUpdate()
        {
            if (!IsOwner) return;

            // Final visual synchronization of tracking points
            if (m_HeadOrigin != null)
            {
                Vector3 targetPosition = m_HeadOrigin.position;
                targetPosition.y = (m_XRRig != null) ? m_XRRig.transform.position.y : 0f;
                transform.position = targetPosition;

                if (head != null) head.position = m_HeadOrigin.position;
                if (leftHand != null && m_LeftHandOrigin != null) leftHand.position = m_LeftHandOrigin.position;
                if (rightHand != null && m_RightHandOrigin != null) rightHand.position = m_RightHandOrigin.position;
            }
        }
        
        /// <summary>
        /// Standard Unity IK pass to map character hands and head look-at to XR tracking data.
        /// </summary>
        void OnAnimatorIK(int layerIndex)
        {
            if (!m_EnableBodyIK || m_Animator == null) return;
            if (!IsOwner) return;

            // Map Left Hand
            if (m_LeftHandOrigin != null)
            {
                m_Animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, m_LeftHandIKWeight);
                m_Animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, m_LeftHandIKWeight);
                m_Animator.SetIKPosition(AvatarIKGoal.LeftHand, m_LeftHandOrigin.position);
                m_Animator.SetIKRotation(AvatarIKGoal.LeftHand, m_LeftHandOrigin.rotation);
            }

            // Map Right Hand
            if (m_RightHandOrigin != null)
            {
                m_Animator.SetIKPositionWeight(AvatarIKGoal.RightHand, m_RightHandIKWeight);
                m_Animator.SetIKRotationWeight(AvatarIKGoal.RightHand, m_RightHandIKWeight);
                m_Animator.SetIKPosition(AvatarIKGoal.RightHand, m_RightHandOrigin.position);
                m_Animator.SetIKRotation(AvatarIKGoal.RightHand, m_RightHandOrigin.rotation);
            }

            // Map Head Look-At
            if (m_HeadOrigin != null)
            {
                m_Animator.SetLookAtWeight(0.5f, 0.3f, 1f, 0f, 0.5f);
                m_Animator.SetLookAtPosition(m_HeadOrigin.position + m_HeadOrigin.forward * 2f);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            if (IsOwner)
            {
                LocalPlayer = this;
                Debug.Log("✅ LOCAL PLAYER spawned");
                SetupLocalPlayer();
            }
            else
            {
                Debug.Log("🌐 REMOTE PLAYER spawned");
                if (m_RPMAvatarRoot != null) m_RPMAvatarRoot.SetActive(true);
            }

            onSpawnedAll?.Invoke();
        }

        /// <summary>
        /// Disables physics/controllers and sets up layers/visuals specifically for the local owner.
        /// </summary>
        protected virtual void SetupLocalPlayer()
        {
            if (TryGetComponent(out CharacterController cc)) cc.enabled = false;
            if (TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                m_HeadOrigin = mainCam.transform;
                m_XRRig = mainCam.transform.root.gameObject;
                FindHandControllers();
            }

            if (IsOwner)
            {
                // Move local player to a specific layer (prevents seeing inside your own head)
                int localLayer = LayerMask.NameToLayer("LocalHead");
                if (localLayer != -1)
                {
                    SetLayerRecursively(gameObject, localLayer);
                }

                // Hide generic hand objects as the local user usually sees their own high-quality hands/controllers
                if (m_HandsObjects != null)
                {
                    foreach (var hand in m_HandsObjects)
                    {
                        if (hand != null) hand.SetActive(false);
                    }
                }
            }
        }

        void SetLayerRecursively(GameObject obj, int newLayer)
        {
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
                SetLayerRecursively(child.gameObject, newLayer);
        }

        /// <summary>
        /// Searches the XR Rig for common hand controller naming conventions.
        /// </summary>
        void FindHandControllers()
        {
            if (m_XRRig == null) return;

            string[] leftHandNames = { "LeftHand Controller", "Left Controller", "LeftHandController", "Hand_L" };
            string[] rightHandNames = { "RightHand Controller", "Right Controller", "RightHandController", "Hand_R" };

            Transform[] allTransforms = m_XRRig.GetComponentsInChildren<Transform>();

            foreach (Transform t in allTransforms)
            {
                string name = t.name;
                
                foreach (string leftName in leftHandNames)
                {
                    if (name.Contains(leftName))
                    {
                        m_LeftHandOrigin = t;
                        Debug.Log($"Found left hand: {name}");
                        break;
                    }
                }

                foreach (string rightName in rightHandNames)
                {
                    if (name.Contains(rightName))
                    {
                        m_RightHandOrigin = t;
                        Debug.Log($"Found right hand: {name}");
                        break;
                    }
                }
            }
        }

        public void SetHandOrigins(Transform left, Transform right)
        {
            m_LeftHandOrigin = left;
            m_RightHandOrigin = right;
        }

        public void SetBodyIKEnabled(bool enabled) => m_EnableBodyIK = enabled;
        public void SetLeftHandIKWeight(float weight) => m_LeftHandIKWeight = Mathf.Clamp01(weight);
        public void SetRightHandIKWeight(float weight) => m_RightHandIKWeight = Mathf.Clamp01(weight);
        public void SetBodyRotationSpeed(float speed) => m_BodyRotationSpeed = Mathf.Max(0, speed);
        
        /// <summary>
        /// Updates the rotation offset at runtime. Useful for debugging alignment issues.
        /// </summary>
        public void SetRotationOffset(float offsetY)
        {
            m_RotationOffsetY = offsetY;
            if (m_RPMAvatarRoot != null && m_OffsetAvatarRoot)
            {
                Vector3 currentRot = m_RPMAvatarRoot.transform.localEulerAngles;
                m_RPMAvatarRoot.transform.localEulerAngles = new Vector3(currentRot.x, offsetY, currentRot.z);
            }
        }

        public void SetPlayerName(string name)
        {
            playerName = name;
            gameObject.name = name;
            onNameUpdated?.Invoke(name);
        }
    }
}