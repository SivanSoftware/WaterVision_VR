using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;                     // HoverEnterEventArgs, HoverExitEventArgs
using UnityEngine.XR.Interaction.Toolkit.UI;                  // UIHoverEventArgs
using UnityEngine.XR.Interaction.Toolkit.Interactors;         // NearFarInteractor
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals; // XRInteractorLineVisual

/// <summary>
/// Quest-shell-style "smart" hand ray. The hand's line + reticle (an <see cref="XRInteractorLineVisual"/>)
/// are shown ONLY while the <see cref="NearFarInteractor"/> is hovering at least one valid target —
/// a 3D interactable (XRGrabInteractable / XRSimpleInteractable / TeleportationArea / …) OR a UI
/// element — or while a selection is in progress (pinch held). Otherwise the ray is hidden so
/// floating lines don't clutter the view.
///
/// Local-only presentation: this lives on the local XR rig's hand interactor and replicates nothing.
///
/// XRI 3.3 has no built-in "hide on no valid target" flag, so this is the canonical pattern: count
/// hovers across BOTH the 3D channel (<c>hoverEntered</c>/<c>hoverExited</c>) AND the UI channel
/// (<c>uiHoverEntered</c>/<c>uiHoverExited</c> — the ONLY channel UI hits fire), then toggle the
/// visual's <see cref="Behaviour.enabled"/> flag, which owns both the line and the reticle as a bundle.
/// </summary>
[RequireComponent(typeof(NearFarInteractor), typeof(XRInteractorLineVisual))]
public class SmartHandRayVisibility : MonoBehaviour
{
    [Tooltip("Keep the ray visible while a selection is active (pinch held = grabbing / clicking / " +
             "teleport-pending), even if the hover set briefly empties. Prevents flicker mid-interaction.")]
    [SerializeField] private bool stayVisibleDuringSelection = true;

    [Tooltip("Log ray show/hide transitions to help tune/diagnose. Leave off in normal use.")]
    [SerializeField] private bool debugLog = false;

    NearFarInteractor m_Interactor;
    XRInteractorLineVisual m_LineVisual;
    int m_ValidHoverCount;

    void Awake()
    {
        m_Interactor = GetComponent<NearFarInteractor>();
        m_LineVisual = GetComponent<XRInteractorLineVisual>();
    }

    void OnEnable()
    {
        // Reset to a clean state every time the hand branch re-activates. XRInputModalityManager
        // deactivates/re-activates the hand GameObject when switching modality, so a stale count must
        // not leave the ray stuck on.
        m_ValidHoverCount = 0;

        if (m_Interactor != null)
        {
            m_Interactor.hoverEntered.AddListener(OnHoverEntered);
            m_Interactor.hoverExited.AddListener(OnHoverExited);
            m_Interactor.uiHoverEntered.AddListener(OnUIHoverEntered);
            m_Interactor.uiHoverExited.AddListener(OnUIHoverExited);
        }
        ApplyVisibility();
    }

    void OnDisable()
    {
        if (m_Interactor != null)
        {
            m_Interactor.hoverEntered.RemoveListener(OnHoverEntered);
            m_Interactor.hoverExited.RemoveListener(OnHoverExited);
            m_Interactor.uiHoverEntered.RemoveListener(OnUIHoverEntered);
            m_Interactor.uiHoverExited.RemoveListener(OnUIHoverExited);
        }
    }

    // 3D interactable hover channel.
    void OnHoverEntered(HoverEnterEventArgs _) { m_ValidHoverCount++; ApplyVisibility(); }
    void OnHoverExited(HoverExitEventArgs _)   { m_ValidHoverCount = Mathf.Max(0, m_ValidHoverCount - 1); ApplyVisibility(); }

    // UI hover channel (UI hits fire ONLY these, never the 3D hover events).
    void OnUIHoverEntered(UIHoverEventArgs _) { m_ValidHoverCount++; ApplyVisibility(); }
    void OnUIHoverExited(UIHoverEventArgs _)  { m_ValidHoverCount = Mathf.Max(0, m_ValidHoverCount - 1); ApplyVisibility(); }

    void Update()
    {
        // hasSelection can change WITHOUT a hover event — e.g. you release a pinch (selection ends)
        // while the ray no longer hovers anything, so no hover-exit fires. A cheap per-frame re-eval
        // (idempotent set below) hides the ray promptly when the selection ends. Only needed when the
        // stay-during-selection behavior is on; pure hover visibility is fully event-driven.
        if (stayVisibleDuringSelection)
            ApplyVisibility();
    }

    void ApplyVisibility()
    {
        if (m_LineVisual == null) return;

        bool show = m_ValidHoverCount > 0
                 || (stayVisibleDuringSelection && m_Interactor != null && m_Interactor.hasSelection);

        if (m_LineVisual.enabled != show)
        {
            m_LineVisual.enabled = show; // toggles the line AND the reticle as a bundle
            if (debugLog)
                Debug.Log($"[SmartHandRayVisibility] {name}: ray {(show ? "SHOWN" : "hidden")} " +
                          $"(hovers={m_ValidHoverCount}, selecting={(m_Interactor != null && m_Interactor.hasSelection)})", this);
        }
    }
}
