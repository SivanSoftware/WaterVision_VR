using System;
using Unity.Netcode;
using UnityEngine;

namespace XRMultiplayer
{
    /// <summary>
    /// XRAvatarVisuals adapted for Ready Player Me avatars
    /// Handles rendering, mouth movement, and visual customization
    /// </summary>
    [RequireComponent(typeof(XRINetworkPlayer_RPM))]
    public class XRAvatarVisuals_RPM : MonoBehaviour
    {
        [Header("RPM Renderer References")]
        [SerializeField, Tooltip("Head Renderer (Wolf3D_Head)")]
        protected SkinnedMeshRenderer m_HeadRenderer;

        [SerializeField, Tooltip("Teeth Renderer (Wolf3D_Teeth)")]
        protected SkinnedMeshRenderer m_TeethRenderer;

        [SerializeField, Tooltip("Body/Outfit Renderer")]
        protected SkinnedMeshRenderer m_BodyRenderer;

        [SerializeField, Tooltip("All head renderers for local player visibility")]
        protected Renderer[] m_HeadRends;

        [Header("Host Visuals")]
        [SerializeField, Tooltip("GameObject that gets enabled for the Host only")]
        protected GameObject m_HostVisuals;

        [SerializeField, Tooltip("Show Host Visuals")]
        protected bool m_ShowHostVisuals = true;

        [Header("Local Player Material Swap")]
        [SerializeField]
        protected LocalPlayerMaterialSwap_RPM m_LocalPlayerMaterialSwap;

        [Header("Voice Animation Settings")]
        [SerializeField, Tooltip("Enable mouth movement from voice")]
        protected bool m_EnableMouthAnimation = true;

        [SerializeField, Tooltip("Mouth animation smoothing")]
        protected float m_MouthSmoothSpeed = 10f;

        [SerializeField, Tooltip("Mouth animation multiplier")]
        protected float m_MouthAnimationScale = 100f;

        [Header("Player Color Customization")]
        [SerializeField, Tooltip("Material index for color customization")]
        protected int m_ColorMaterialIndex = 0;

        [SerializeField, Tooltip("Color property name in shader")]
        protected string m_ColorPropertyName = "_BaseColor";

        // ARKit Blendshape indices for mouth movement
        const string BLEND_JAWOPEN = "mouthOpen";
        const string BLEND_MOUTHCLOSE = "mouthClose";
        
        // Alternative blendshape names (RPM uses different naming conventions)
        string[] m_MouthBlendshapeNames = new string[] 
        {
            "mouthOpen",
            "jawOpen", 
            "viseme_aa",
            "viseme_O",
            "JawOpen"
        };

        protected XRINetworkPlayer_RPM m_NetworkPlayer;
        protected int m_MouthBlendshapeIndex = -1;
        protected float m_CurrentMouthValue = 0f;

        public virtual void Awake()
        {
            if (!TryGetComponent(out m_NetworkPlayer))
            {
                Debug.LogError("XRAvatarVisuals_RPM requires XRINetworkPlayer_RPM component! Disabling.");
                enabled = false;
                return;
            }

            // Auto-detect renderers if not assigned
            if (m_HeadRenderer == null || m_TeethRenderer == null || m_BodyRenderer == null)
            {
                AutoDetectRenderers();
            }

            // Find mouth blendshape
            FindMouthBlendshape();

            // Subscribe to events
            m_NetworkPlayer.onSpawnedLocal += PlayerSpawnedLocal;
            m_NetworkPlayer.onSpawnedAll += PlayerSpawnedAll;
            m_NetworkPlayer.onColorUpdated += SetPlayerColor;
            
            // Subscribe to host promotion if XRINetworkGameManager exists
            // if (XRINetworkGameManager.Instance != null)
            // {
            //     XRINetworkGameManager.Instance.OnSessionOwnerPromoted += HostUpdated;
            // }
        }

        public virtual void OnDestroy()
        {
            if (m_NetworkPlayer != null)
            {
                m_NetworkPlayer.onSpawnedLocal -= PlayerSpawnedLocal;
                m_NetworkPlayer.onSpawnedAll -= PlayerSpawnedAll;
                m_NetworkPlayer.onColorUpdated -= SetPlayerColor;
            }

            // if (XRINetworkGameManager.Instance != null)
            // {
            //     XRINetworkGameManager.Instance.OnSessionOwnerPromoted -= HostUpdated;
            // }
        }

        public virtual void Update()
        {
            if (m_EnableMouthAnimation)
            {
                UpdateMouth();
            }
        }

        /// <summary>
        /// Auto-detect RPM renderers in the avatar
        /// </summary>
        void AutoDetectRenderers()
        {
            SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            
            foreach (var renderer in renderers)
            {
                string name = renderer.name.ToLower();
                
                if (name.Contains("head") && m_HeadRenderer == null)
                {
                    m_HeadRenderer = renderer;
                    Debug.Log($"XRAvatarVisuals_RPM: Auto-detected head renderer: {renderer.name}");
                }
                else if (name.Contains("teeth") && m_TeethRenderer == null)
                {
                    m_TeethRenderer = renderer;
                    Debug.Log($"XRAvatarVisuals_RPM: Auto-detected teeth renderer: {renderer.name}");
                }
                else if ((name.Contains("body") || name.Contains("outfit")) && m_BodyRenderer == null)
                {
                    m_BodyRenderer = renderer;
                    Debug.Log($"XRAvatarVisuals_RPM: Auto-detected body renderer: {renderer.name}");
                }
            }

            // If m_HeadRends not set, use head and teeth
            if (m_HeadRends == null || m_HeadRends.Length == 0)
            {
                if (m_HeadRenderer != null && m_TeethRenderer != null)
                {
                    m_HeadRends = new Renderer[] { m_HeadRenderer, m_TeethRenderer };
                }
                else if (m_HeadRenderer != null)
                {
                    m_HeadRends = new Renderer[] { m_HeadRenderer };
                }
            }
        }

        /// <summary>
        /// Find the mouth blendshape index
        /// </summary>
        void FindMouthBlendshape()
        {
            if (m_HeadRenderer == null) return;

            Mesh mesh = m_HeadRenderer.sharedMesh;
            if (mesh == null) return;

            // Try to find mouth blendshape
            foreach (string blendshapeName in m_MouthBlendshapeNames)
            {
                m_MouthBlendshapeIndex = mesh.GetBlendShapeIndex(blendshapeName);
                if (m_MouthBlendshapeIndex >= 0)
                {
                    Debug.Log($"XRAvatarVisuals_RPM: Found mouth blendshape '{blendshapeName}' at index {m_MouthBlendshapeIndex}");
                    return;
                }
            }

            Debug.LogWarning("XRAvatarVisuals_RPM: Could not find mouth blendshape. Voice animation disabled.");
        }

        /// <summary>
        /// Update mouth animation based on voice amplitude
        /// </summary>
        public virtual void UpdateMouth()
        {
            if (m_HeadRenderer == null || m_MouthBlendshapeIndex < 0) return;

            // Get voice amplitude from network player
            float voiceAmp = m_NetworkPlayer.playerVoiceAmp;
            
            // Calculate target mouth value (inverse: 0 = open, 100 = closed)
            float targetMouth = 100f - (voiceAmp * m_MouthAnimationScale);
            targetMouth = Mathf.Clamp(targetMouth, 0f, 100f);

            // Smooth transition
            m_CurrentMouthValue = Mathf.Lerp(m_CurrentMouthValue, targetMouth, Time.deltaTime * m_MouthSmoothSpeed);

            // Apply blendshape
            m_HeadRenderer.SetBlendShapeWeight(m_MouthBlendshapeIndex, m_CurrentMouthValue);
        }

        /// <summary>
        /// Called when local player spawns
        /// </summary>
        public virtual void PlayerSpawnedLocal()
        {
            // Swap materials for local player
            if (m_LocalPlayerMaterialSwap != null)
            {
                m_LocalPlayerMaterialSwap.SwapMaterials();
            }

            // Set head renderers to Mirror layer (for mirrors in VR)
            int mirrorLayer = LayerMask.NameToLayer("Mirror");
            if (mirrorLayer >= 0)
            {
                foreach (var renderer in m_HeadRends)
                {
                    if (renderer != null)
                    {
                        renderer.gameObject.layer = mirrorLayer;
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                }
            }

            Debug.Log("XRAvatarVisuals_RPM: Local player spawned - materials swapped");
        }

        /// <summary>
        /// Called when all players finish spawning
        /// </summary>
        public virtual void PlayerSpawnedAll()
        {
            if (m_HostVisuals != null && m_NetworkPlayer.NetworkObject != null)
            {
                // Check if this is the host (client ID 0 is usually the host)
                bool isHost = m_NetworkPlayer.NetworkObject.OwnerClientId == 0;
                m_HostVisuals.SetActive(m_ShowHostVisuals && isHost);
                
                if (isHost)
                {
                    Debug.Log($"XRAvatarVisuals_RPM: Player {m_NetworkPlayer.playerName} is HOST");
                }
            }
        }

        /// <summary>
        /// Set player color on outfit/body
        /// </summary>
        public virtual void SetPlayerColor(Color newColor)
        {
            if (m_BodyRenderer == null) return;

            Material[] materials = m_BodyRenderer.materials;
            
            if (m_ColorMaterialIndex < materials.Length)
            {
                materials[m_ColorMaterialIndex].SetColor(m_ColorPropertyName, newColor);
                m_BodyRenderer.materials = materials;
                
                Debug.Log($"XRAvatarVisuals_RPM: Player color set to {newColor}");
            }
            else
            {
                Debug.LogWarning($"XRAvatarVisuals_RPM: Color material index {m_ColorMaterialIndex} out of range!");
            }
        }

        /// <summary>
        /// Called when host changes
        /// </summary>
        public virtual void HostUpdated(ulong newHostId)
        {
            if (m_HostVisuals != null && m_NetworkPlayer != null && m_NetworkPlayer.NetworkObject != null)
            {
                bool isHost = m_NetworkPlayer.NetworkObject.OwnerClientId == newHostId;
                m_HostVisuals.SetActive(m_ShowHostVisuals && isHost);
                
                if (isHost)
                {
                    Debug.Log($"XRAvatarVisuals_RPM: Player is now HOST");
                }
            }
        }

        /// <summary>
        /// Manually set head renderer
        /// </summary>
        public void SetHeadRenderer(SkinnedMeshRenderer renderer)
        {
            m_HeadRenderer = renderer;
            FindMouthBlendshape();
        }

        /// <summary>
        /// Manually set body renderer
        /// </summary>
        public void SetBodyRenderer(SkinnedMeshRenderer renderer)
        {
            m_BodyRenderer = renderer;
        }

        /// <summary>
        /// Enable/disable mouth animation
        /// </summary>
        public void SetMouthAnimationEnabled(bool enabled)
        {
            m_EnableMouthAnimation = enabled;
        }
    }
}

/// <summary>
/// Helper class for swapping materials on Ready Player Me avatars
/// Handles local player transparency and material changes
/// </summary>
[Serializable]
public class LocalPlayerMaterialSwap_RPM
{
    [Header("RPM Renderers")]
    [Tooltip("Head renderer (Wolf3D_Head)")]
    public SkinnedMeshRenderer headRenderer;
    
    [Tooltip("Teeth renderer (Wolf3D_Teeth)")]
    public SkinnedMeshRenderer teethRenderer;
    
    [Tooltip("Body/Outfit renderer")]
    public SkinnedMeshRenderer bodyRenderer;
    
    [Tooltip("Hand renderers (if separate from body)")]
    public Renderer[] handRenderers;

    [Header("Materials")]
    [Tooltip("Materials for head (remote players)")]
    public Material[] headMaterials;
    
    [Tooltip("Materials for head when in VR (transparent/invisible)")]
    public Material[] hmdMaterials;
    
    [Tooltip("Materials for teeth")]
    public Material[] teethMaterials;
    
    [Tooltip("Material for host crown/indicator")]
    public Material hostMaterial;
    
    [Tooltip("Material for hands")]
    public Material handMaterial;

    [Header("Settings")]
    [Tooltip("Make head invisible for local player (VR)")]
    public bool hideHeadForLocalPlayer = true;

    public void SwapMaterials()
    {
        // Swap hand materials
        if (handRenderers != null && handMaterial != null)
        {
            foreach (var hand in handRenderers)
            {
                if (hand != null)
                    hand.material = handMaterial;
            }
        }

        // Swap head materials (make invisible for VR)
        if (headRenderer != null)
        {
            if (hideHeadForLocalPlayer && hmdMaterials != null && hmdMaterials.Length > 0)
            {
                headRenderer.materials = hmdMaterials;
            }
            else if (headMaterials != null && headMaterials.Length > 0)
            {
                headRenderer.materials = headMaterials;
            }
        }

        // Swap teeth materials
        if (teethRenderer != null && teethMaterials != null && teethMaterials.Length > 0)
        {
            teethRenderer.materials = teethMaterials;
        }

        Debug.Log("LocalPlayerMaterialSwap_RPM: Materials swapped for local player");
    }

    /// <summary>
    /// Auto-detect renderers in a GameObject
    /// </summary>
    public void AutoDetectRenderers(GameObject avatarRoot)
    {
        if (avatarRoot == null) return;

        SkinnedMeshRenderer[] renderers = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
        
        foreach (var renderer in renderers)
        {
            string name = renderer.name.ToLower();
            
            if (name.Contains("head") && headRenderer == null)
            {
                headRenderer = renderer;
            }
            else if (name.Contains("teeth") && teethRenderer == null)
            {
                teethRenderer = renderer;
            }
            else if ((name.Contains("body") || name.Contains("outfit")) && bodyRenderer == null)
            {
                bodyRenderer = renderer;
            }
        }

        Debug.Log($"LocalPlayerMaterialSwap_RPM: Auto-detected renderers - Head: {headRenderer != null}, Teeth: {teethRenderer != null}, Body: {bodyRenderer != null}");
    }
}