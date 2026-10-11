using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using Meta.XR.MRUtilityKit;

namespace WaterVision.AR
{
    /// <summary>
    /// On AR enter, places the workspace (the two room menus) so they're reachable in the user's REAL space:
    /// snapped flat to the real wall the user faces (Meta MRUK scene), or — if no room scan exists — ~1.2 m in front
    /// of the user at eye height. Also brings the ExportTray within reach. Panels the user has already grabbed/placed
    /// (GrabbableUIPanel.IsPlaced) are left alone, so manual moves win. The tray is restored on exit.
    ///
    /// Wall-snap requires the user to have run Space Setup on the Quest; otherwise MRUK.Instance.GetCurrentRoom() is
    /// null and the in-front fallback runs. SAFE OFF-DEVICE/editor: no MRUK room → fallback path, no errors.
    /// Facing convention matches MenuController / the AR toolbar: LookRotation(panel - head) → readable -Z face to user.
    /// </summary>
    public class ARWorkspacePlacer : MonoBehaviour
    {
        [Header("Wiring (set by the setup tool)")]
        [SerializeField] private ARModeController m_Controller;
        [Tooltip("The room menus to relocate into the user's real space on AR enter (e.g. ModelMenuStation_Canvas, ExportStation_Canvas).")]
        [SerializeField] private List<Transform> m_Panels = new List<Transform>();
        [Tooltip("The ExportTray / assembly platform to bring within reach (optional).")]
        [SerializeField] private Transform m_Tray;

        [Header("Placement")]
        [SerializeField] private float m_FrontDistance = 1.2f; // fallback distance in front of the head
        [SerializeField] private float m_PanelSpacing = 1.2f;  // horizontal gap between menus
        [SerializeField] private float m_WallOffset = 0.05f;   // how far off the wall, toward the user
        [SerializeField] private float m_TrayDistance = 1.6f;  // tray distance in front
        [SerializeField] private float m_TrayDrop = 0.6f;      // tray height below the head (toward the floor)
        [SerializeField] private bool m_DebugLog = true;

        private Camera m_Cam;
        private bool m_TrayCaptured;
        private Vector3 m_TrayPos0;
        private Quaternion m_TrayRot0;
        private bool m_SceneCallbackRegistered;

        private void OnEnable()
        {
            if (m_Controller == null) m_Controller = FindFirstObjectByType<ARModeController>(FindObjectsInactive.Include);
            if (m_Controller != null) m_Controller.ARModeChanged += OnARModeChanged;
        }

        private void Start()
        {
            // MRUK.Instance is set in MRUK.Awake, so register the scene-loaded callback in Start (after all Awakes).
            if (!m_SceneCallbackRegistered && MRUK.Instance != null)
            {
                MRUK.Instance.RegisterSceneLoadedCallback(OnSceneLoaded);
                m_SceneCallbackRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (m_Controller != null) m_Controller.ARModeChanged -= OnARModeChanged;
        }

        private void OnARModeChanged(bool on)
        {
            if (on) PlaceWorkspace(false);
            else RestoreTray();
        }

        // If the room scan finishes loading while we're already in AR, re-snap the not-yet-moved panels onto the walls.
        private void OnSceneLoaded()
        {
            if (m_Controller != null && m_Controller.IsARModeActive) PlaceWorkspace(false);
        }

        /// <summary>Re-place ALL menus + the platform in front of the user, overriding manual placements. Wired to the
        /// toolbar's "Recenter" button.</summary>
        public void RecenterWorkspace() => PlaceWorkspace(true);

        private void PlaceWorkspace(bool force)
        {
            m_Cam = ResolveCamera();
            if (m_Cam == null) return;

            Vector3 head = m_Cam.transform.position;
            Vector3 fwd = m_Cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd); // user's right

            var room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;
            bool wallSnapped = false;

            if (room != null && room.WallAnchors != null && room.WallAnchors.Count > 0)
            {
                var ray = new Ray(head, fwd);
                if (room.Raycast(ray, 20f, out RaycastHit hit, out MRUKAnchor anchor) && room.WallAnchors.Contains(anchor))
                {
                    Vector3 wallRight = Vector3.Cross(Vector3.up, hit.normal); // along the wall
                    if (wallRight.sqrMagnitude < 1e-4f) wallRight = right;
                    wallRight.Normalize();
                    Vector3 basePos = hit.point + hit.normal * m_WallOffset;
                    basePos.y = head.y;
                    PlacePanelsAround(basePos, wallRight, head, force);
                    wallSnapped = true;
                    if (m_DebugLog) Debug.Log("[ARWorkspacePlacer] wall-snap (" + room.WallAnchors.Count + " walls) onto the wall in front.");
                }
            }

            if (!wallSnapped)
            {
                Vector3 basePos = head + fwd * m_FrontDistance;
                basePos.y = head.y;
                PlacePanelsAround(basePos, right, head, force);
                if (m_DebugLog) Debug.Log("[ARWorkspacePlacer] fallback (no scene) — menus ~" + m_FrontDistance + " m in front. Run Space Setup for wall-snap.");
            }

            PlaceTray(head, fwd, force);
        }

        // Lay the panels side by side around basePos along 'along', each facing the user.
        // force=false leaves panels the user already moved (IsPlaced) alone; force overrides them (Recenter).
        private void PlacePanelsAround(Vector3 basePos, Vector3 along, Vector3 head, bool force)
        {
            var movable = new List<Transform>();
            foreach (var p in m_Panels)
            {
                if (p == null) continue;
                var gp = p.GetComponent<GrabbableUIPanel>();
                if (!force && gp != null && gp.IsPlaced) continue; // respect the user's manual placement
                movable.Add(p);
            }

            int n = movable.Count;
            for (int i = 0; i < n; i++)
            {
                float offset = (i - (n - 1) * 0.5f) * m_PanelSpacing; // centered spread
                Vector3 pos = basePos + along * offset;
                Vector3 face = pos - head; face.y = 0f;
                if (face.sqrMagnitude < 1e-4f) face = Vector3.forward;
                movable[i].SetPositionAndRotation(pos, Quaternion.LookRotation(face.normalized, Vector3.up));
            }
        }

        private void PlaceTray(Vector3 head, Vector3 fwd, bool force)
        {
            if (m_Tray == null) return;
            var gpTray = m_Tray.GetComponent<GrabbableUIPanel>();
            if (!force && gpTray != null && gpTray.IsPlaced) return; // respect a manual tray move
            if (!m_TrayCaptured) { m_TrayPos0 = m_Tray.position; m_TrayRot0 = m_Tray.rotation; m_TrayCaptured = true; }
            Vector3 pos = head + fwd * m_TrayDistance;
            pos.y = head.y - m_TrayDrop; // bring it down toward the real floor, in front
            m_Tray.position = pos;        // keep original rotation (it's a volume); same size
            if (m_DebugLog) Debug.Log("[ARWorkspacePlacer] tray brought within reach (~" + m_TrayDistance + " m).");
        }

        private void RestoreTray()
        {
            if (m_Tray == null || !m_TrayCaptured) return;
            var gpTray = m_Tray.GetComponent<GrabbableUIPanel>();
            if (gpTray == null || !gpTray.IsPlaced) m_Tray.SetPositionAndRotation(m_TrayPos0, m_TrayRot0); // keep a manual move
            m_TrayCaptured = false;
        }

        private Camera ResolveCamera()
        {
            var origin = FindFirstObjectByType<XROrigin>();
            if (origin != null && origin.Camera != null) return origin.Camera;
            return Camera.main;
        }
    }
}
