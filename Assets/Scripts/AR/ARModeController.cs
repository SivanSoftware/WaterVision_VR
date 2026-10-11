using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace WaterVision.AR
{
    /// <summary>
    /// Toggles "AR mode" in the Models Room: switches the Quest to passthrough (the real room becomes visible)
    /// and hides ONLY the virtual room backdrop (walls/doors/posters) + the TrashBin, while every functional
    /// element under "Room" (the pipe-catalog menu, export station, teleport anchor, export tray) stays active and
    /// usable in place. Local view choice only — pipes stay networked via the existing spawner.
    ///
    /// Passthrough is enabled by toggling a separate "MetaPassthrough" GameObject (OVRManager + OVRPassthroughLayer
    /// Underlay) active + flipping the URP alpha feature so the empty eye-buffer alpha is 0 and the Underlay shows
    /// through. This class NEVER references Meta types, so it compiles everywhere. SAFE OFF-DEVICE: when
    /// XR_FB_passthrough is absent (editor/desktop) the passthrough toggle is skipped and only the backdrop is hidden.
    ///
    /// Because we now hide only the backdrop children (NOT the whole Room), the menus stay parented under the still-
    /// active Room and keep their world pose — no reparent/keep-alive hack is needed. See the plan file.
    /// </summary>
    public class ARModeController : MonoBehaviour
    {
        [Header("Hide in AR (the virtual backdrop only — walls/doors/posters + TrashBin)")]
        [Tooltip("Assign the backdrop children of the model room (walls, doors, posters, TrashBin). NEVER assign the whole 'Room' — that would hide the menus too. If empty, the controller resolves them by name under m_EnvironmentRootName.")]
        [SerializeField] private List<GameObject> m_EnvironmentRoots = new List<GameObject>();
        [Tooltip("Name of the model-room root used only by the name-fallback below to locate the backdrop children.")]
        [SerializeField] private string m_EnvironmentRootName = "Room";

        [Header("Passthrough")]
        [Tooltip("The inactive 'MetaPassthrough' GameObject (OVRManager + OVRPassthroughLayer Underlay). Activated in AR; assigned by the AR setup tool. Leave null on platforms without it.")]
        [SerializeField] private GameObject m_PassthroughRoot;
        [Tooltip("Camera background while in AR. Alpha MUST be 0 so passthrough shows through the eye buffer.")]
        [SerializeField] private Color m_PassthroughClearColor = new Color(0f, 0f, 0f, 0f);

        [Header("Comfort")]
        [Tooltip("Tunneling vignette used for a brief comfort blink on AR enter/exit. Auto-resolved from the scene if left null.")]
        [SerializeField] private TunnelingVignetteController m_ComfortVignette;
        [Tooltip("Seconds to hold the vignette before easing it back out.")]
        [SerializeField] private float m_ComfortHoldSeconds = 0.35f;

        [Header("Debug")]
        [SerializeField] private bool m_DebugLog = true;

        // Meta's passthrough OpenXR extension (present on Quest; absent in the editor → clean no-op).
        const string PassthroughExtension = "XR_FB_passthrough";

        // Direct children of the model room that form the virtual backdrop, hidden in AR so passthrough shows.
        // TrashBin is included because the user wants it gone in AR (the toolbar Delete replaces it).
        private static readonly string[] s_BackdropChildNames = { "walls", "doors", "posters", "TrashBin" };

        /// <summary>Fired when AR mode turns on (true) / off (false). The floating toolbar + tray boundary listen.</summary>
        public event Action<bool> ARModeChanged;

        private bool m_Active;
        private Camera m_Cam;
        private CameraClearFlags m_ClearFlags0;
        private Color m_BgColor0;
        private bool m_CameraCaptured;
        private readonly List<GameObject> m_HiddenThisSession = new List<GameObject>();

        // Comfort-fade provider (returns null params → uses the controller's configured defaults).
        private sealed class NullVignetteProvider : ITunnelingVignetteProvider
        {
            public VignetteParameters vignetteParameters => null;
        }
        private ITunnelingVignetteProvider m_VignetteProvider;

        public bool IsARModeActive => m_Active;

        public void ToggleARMode()
        {
            if (m_Active) ExitARMode();
            else EnterARMode();
        }

        public void EnterARMode()
        {
            if (m_Active) return;

            ComfortBlink(); // brief vignette over the swap
            m_Cam = ResolveCamera();
            HideEnvironment();

            if (!PassthroughSupported())
            {
                m_Active = true;
                ARModeChanged?.Invoke(true);
                if (m_DebugLog) Debug.Log("[ARModeController] Entered AR mode — passthrough UNAVAILABLE here, hid the virtual backdrop only (expected in editor/desktop).");
                return;
            }

            if (m_Cam != null && !m_CameraCaptured)
            {
                m_ClearFlags0 = m_Cam.clearFlags;
                m_BgColor0 = m_Cam.backgroundColor;
                m_CameraCaptured = true;
            }
            EnablePassthrough();

            m_Active = true;
            ARModeChanged?.Invoke(true);
            if (m_DebugLog) Debug.Log("[ARModeController] Entered AR mode (passthrough on).");
        }

        public void ExitARMode()
        {
            if (!m_Active) return;

            ComfortBlink(); // brief vignette over the swap back to VR
            DisablePassthrough(); // always safe — just deactivates the passthrough layer GameObject + the URP alpha flag
            if (m_Cam != null && m_CameraCaptured)
            {
                m_Cam.clearFlags = m_ClearFlags0;
                m_Cam.backgroundColor = m_BgColor0;
                m_CameraCaptured = false;
            }

            ShowEnvironment();
            m_Active = false;
            ARModeChanged?.Invoke(false);
            if (m_DebugLog) Debug.Log("[ARModeController] Exited AR mode (passthrough off).");
        }

        private void OnDisable()
        {
            // Never leave the headset stuck in passthrough / the backdrop hidden.
            if (m_Active) ExitARMode();
        }

        // ---------------------------------------------------------------------
        // Passthrough model (verified against the Meta + Unity OpenXR package source):
        //
        // * MetaPassthrough (OVRManager + OVRPassthroughLayer Underlay) is activated on AR enter and left active on
        //   exit (tearing OVRManager down/up at runtime is the fragile path Meta samples avoid). The Underlay is
        //   harmless/invisible in normal VR because the opaque eye buffer (alpha = 1) fully occludes it; it only
        //   becomes visible once we punch alpha to 0.
        //
        // * AR enter/exit toggles eye-buffer transparency: camera clears to alpha 0 AND
        //   ARPassthroughAlphaFeature.Active = true → a URP pass writes alpha 0 on the empty background so the
        //   compositor (projection layer submitted with SourceAlpha) shows the Underlay there.
        //   This class still never references Meta types (it only ensures the root is active + flips the URP flag).
        // ---------------------------------------------------------------------
        private void EnablePassthrough()
        {
            if (m_PassthroughRoot != null && !m_PassthroughRoot.activeSelf) m_PassthroughRoot.SetActive(true);
            ARPassthroughAlphaFeature.Active = true; // URP punches background alpha → 0 so the Underlay shows through
            if (m_Cam != null)
            {
                m_Cam.clearFlags = CameraClearFlags.SolidColor;
                m_Cam.backgroundColor = m_PassthroughClearColor; // alpha 0 → passthrough shows through
            }
        }

        private void DisablePassthrough()
        {
            // Leave MetaPassthrough active (keeps OVRManager/passthrough initialized). Restoring the opaque eye buffer
            // (alpha 1 via clear flags + this flag off) is what hides passthrough — back to normal VR.
            ARPassthroughAlphaFeature.Active = false;
        }

        private static bool PassthroughSupported()
        {
            try { return OpenXRRuntime.IsExtensionEnabled(PassthroughExtension); }
            catch { return false; }
        }

        private Camera ResolveCamera()
        {
            var origin = FindFirstObjectByType<XROrigin>();
            if (origin != null && origin.Camera != null) return origin.Camera;
            return Camera.main;
        }

        // ---------------------------------------------------------------------
        // Comfort fade — reuses the scene's TunnelingVignetteController for a brief blink over the AR<->VR swap.
        // ---------------------------------------------------------------------
        private void ComfortBlink()
        {
            if (!isActiveAndEnabled) return; // need to be active to run the end-coroutine; skip on OnDisable force-exit
            if (m_ComfortVignette == null)
                m_ComfortVignette = FindFirstObjectByType<TunnelingVignetteController>(FindObjectsInactive.Include);
            if (m_ComfortVignette == null) return;
            m_VignetteProvider ??= new NullVignetteProvider();
            m_ComfortVignette.BeginTunnelingVignette(m_VignetteProvider);
            StartCoroutine(EndComfortBlink());
        }

        private IEnumerator EndComfortBlink()
        {
            yield return new WaitForSeconds(m_ComfortHoldSeconds);
            if (m_ComfortVignette != null && m_VignetteProvider != null)
                m_ComfortVignette.EndTunnelingVignette(m_VignetteProvider);
        }

        // ---------------------------------------------------------------------
        // Backdrop hide/show. Only the assigned backdrop children are toggled — the Room (and its menus) stay active.
        // ---------------------------------------------------------------------
        private void HideEnvironment()
        {
            m_HiddenThisSession.Clear();
            bool any = false;
            foreach (var go in m_EnvironmentRoots)
            {
                if (go == null || !go.activeSelf) continue;
                go.SetActive(false);
                m_HiddenThisSession.Add(go);
                any = true;
            }

            if (any) return;

            // Name-fallback (safety net): resolve the backdrop children under the room by name. NEVER hides the whole
            // Room — only its walls/doors/posters/TrashBin children.
            if (string.IsNullOrEmpty(m_EnvironmentRootName)) return;
            var room = GameObject.Find(m_EnvironmentRootName);
            if (room == null) return;
            foreach (var n in s_BackdropChildNames)
            {
                var child = room.transform.Find(n);
                if (child != null && child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                    m_HiddenThisSession.Add(child.gameObject);
                }
            }
        }

        private void ShowEnvironment()
        {
            foreach (var go in m_HiddenThisSession)
                if (go != null) go.SetActive(true);
            m_HiddenThisSession.Clear();
        }
    }
}
