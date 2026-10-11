using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace WaterVision.AR
{
    /// <summary>
    /// Makes a world-space UI panel grabbable/movable in AR via a handle bar. The handle's BoxCollider is the only
    /// collider on the panel, so the XRGrabInteractable (on this same root) grabs ONLY via the handle — poking the
    /// panel's buttons (TrackedDeviceGraphicRaycaster, a separate system) never starts a grab. Works for hands and
    /// controllers (same interactor path as the pipes). Panels are NOT networked → moving them is purely local.
    ///
    /// Placement PERSISTS within the app session: stored on release, restored on (re)enable; reset at app launch.
    /// Added + wired by ARGrabbablePanelSetup.
    /// </summary>
    public class GrabbableUIPanel : MonoBehaviour
    {
        [Tooltip("Stable persistence key (defaults to this GameObject's name).")]
        [SerializeField] private string m_Key;
        [Tooltip("The XRGrabInteractable on this root. Resolved from this GameObject if left null.")]
        [SerializeField] private XRGrabInteractable m_Grab;

        // Session-scoped placements; cleared at app launch (mirrors ARPassthroughAlphaFeature.ResetActive).
        private static readonly Dictionary<string, (Vector3 pos, Quaternion rot)> s_Placements =
            new Dictionary<string, (Vector3, Quaternion)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlacements() => s_Placements.Clear();

        /// <summary>True once the panel has been grabbed/placed this session (or restored from a stored placement).
        /// The toolbar reads this to stop head-following once the user has positioned it.</summary>
        public bool IsPlaced { get; private set; }

        private string Key => string.IsNullOrEmpty(m_Key) ? name : m_Key;

        private void OnEnable()
        {
            if (m_Grab == null) m_Grab = GetComponent<XRGrabInteractable>();
            if (m_Grab != null)
            {
                m_Grab.selectEntered.AddListener(OnGrabbed);
                m_Grab.selectExited.AddListener(OnReleased);
            }
            if (s_Placements.TryGetValue(Key, out var p))
            {
                transform.SetPositionAndRotation(p.pos, p.rot);
                IsPlaced = true;
            }
        }

        private void OnDisable()
        {
            if (m_Grab != null)
            {
                m_Grab.selectEntered.RemoveListener(OnGrabbed);
                m_Grab.selectExited.RemoveListener(OnReleased);
            }
        }

        private void OnGrabbed(SelectEnterEventArgs _) => IsPlaced = true;

        private void OnReleased(SelectExitEventArgs _) => s_Placements[Key] = (transform.position, transform.rotation);
    }
}
