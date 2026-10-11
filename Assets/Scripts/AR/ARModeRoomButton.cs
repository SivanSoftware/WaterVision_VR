using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace WaterVision.AR
{
    /// <summary>
    /// Put on the in-room "Enter AR Mode" kiosk. On Start, finds the kiosk's XRSimpleInteractable (the pokable
    /// button) and the scene's ARModeController, then wires selectEntered → ToggleARMode(). selectEntered fires
    /// on BOTH finger-poke (via the button's XRPokeFilter) AND controller ray-select, so the button is poke-
    /// and ray-operable with no extra work — the same dual path the wrist watch button uses (WristMenuController).
    /// Safe/inert if no controller (warns, no crash); idempotent.
    /// </summary>
    public class ARModeRoomButton : MonoBehaviour
    {
        [Tooltip("Optional: the scene's ARModeController. Found at runtime if left empty.")]
        [SerializeField] private ARModeController m_Controller;
        [Tooltip("Optional: the pokable button. Found in children at runtime if left empty.")]
        [SerializeField] private XRSimpleInteractable m_Interactable;
        [SerializeField] private bool m_DebugLog = true;

        private void Start() => Wire();

        private void Wire()
        {
            if (m_Interactable == null) m_Interactable = GetComponentInChildren<XRSimpleInteractable>(true);
            if (m_Interactable == null)
            {
                if (m_DebugLog) Debug.LogWarning("[ARModeRoomButton] No XRSimpleInteractable in children — poke/ray won't toggle AR.", this);
                return;
            }

            if (m_Controller == null) m_Controller = FindFirstObjectByType<ARModeController>(FindObjectsInactive.Include);
            if (m_Controller == null)
            {
                if (m_DebugLog) Debug.LogWarning("[ARModeRoomButton] No ARModeController in scene — kiosk present but inert.", this);
                return;
            }

            var controller = m_Controller; // capture for the listener
            m_Interactable.selectEntered.RemoveAllListeners();
            m_Interactable.selectEntered.AddListener(_ => controller.ToggleARMode());
            if (m_DebugLog) Debug.Log("[ARModeRoomButton] Wired selectEntered → ARModeController.ToggleARMode (poke + ray).", this);
        }
    }
}
