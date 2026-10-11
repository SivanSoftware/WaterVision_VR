using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Pulses the emission intensity of a target renderer when the attached
/// <see cref="XRSimpleInteractable"/> is hovered. The rest-state intensity
/// matches the value baked into the WristButton_Body material; on hover the
/// component lerps the emission color up to <see cref="m_HoverIntensity"/>,
/// then back down on hover-exit. Uses a MaterialPropertyBlock so per-instance
/// glow does not fork the source material asset.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class WristButtonHoverGlow : MonoBehaviour
{
    [SerializeField, Tooltip("MeshRenderer whose material emission intensity will pulse on hover.")]
    Renderer m_GlowRenderer;
    [SerializeField] float m_RestIntensity = 0.3f;
    [SerializeField] float m_HoverIntensity = 1.0f;
    [SerializeField] float m_LerpSpeed = 12f;

    XRSimpleInteractable m_Interactable;
    bool m_IsHovered;
    MaterialPropertyBlock m_Block;

    void Awake()
    {
        m_Interactable = GetComponent<XRSimpleInteractable>();
        if (m_GlowRenderer == null) m_GlowRenderer = GetComponentInChildren<Renderer>();
        m_Block = new MaterialPropertyBlock();
        m_Interactable.hoverEntered.AddListener(_ => m_IsHovered = true);
        m_Interactable.hoverExited.AddListener(_ => m_IsHovered = false);
    }

    void Update()
    {
        if (m_GlowRenderer == null) return;
        float target = m_IsHovered ? m_HoverIntensity : m_RestIntensity;
        m_GlowRenderer.GetPropertyBlock(m_Block);
        float current = m_Block.GetColor("_EmissionColor").maxColorComponent;
        float lerped = Mathf.Lerp(current, target, Time.deltaTime * m_LerpSpeed);
        m_Block.SetColor("_EmissionColor", new Color(0.29f, 0.54f, 1.0f) * lerped);
        m_GlowRenderer.SetPropertyBlock(m_Block);
    }
}
