#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using WaterVision.AR;

namespace WaterVision.AR.EditorTools
{
    /// <summary>
    /// Makes the AR panels (pipe-catalog menu, export station, utility toolbar) grabbable via a handle bar, and adds
    /// the ExportTray boundary wireframe. Idempotent (find-or-create the handle/boundary; re-run safe). Run AFTER the
    /// AR UI Button / Utility Toolbar / Meta Passthrough setups so all targets exist.
    /// </summary>
    public static class ARGrabbablePanelSetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        static readonly string[] PanelNames = { "ModelMenuStation_Canvas", "ExportStation_Canvas", "AR Utility Toolbar" };
        const string HandleName = "GrabHandle";
        const string TrayName = "ExportTray";
        const string BoundaryName = "ExportTrayBoundary";
        static readonly Color CyanAccent = new Color(0.1f, 0.9f, 1f, 1f);

        [MenuItem("WaterVision/AR/Setup AR Interactions")]
        public static void Setup()
        {
            if (!OpenScene()) return;
            var scene = EditorSceneManager.GetActiveScene();

            foreach (var n in PanelNames)
            {
                var panel = GameObject.Find(n);
                if (panel == null) { Debug.LogWarning("[ARGrabbablePanelSetup] Panel '" + n + "' not found — skipped."); continue; }
                MakeGrabbable(panel);
            }

            // Make the assembly platform (ExportTray volume) grabbable too, so the user can reposition it.
            var trayGo = GameObject.Find(TrayName);
            if (trayGo != null) MakeGrabbable(trayGo);

            SetupTrayBoundary();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ARGrabbablePanelSetup] Done — grabbable panels + export-tray boundary.");
        }

        static void MakeGrabbable(GameObject panel)
        {
            // Kinematic rigidbody (no gravity) so XRGrabInteractable moves it cleanly, no physics drift.
            var rb = panel.GetComponent<Rigidbody>();
            if (rb == null) rb = panel.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Handle bar above the top edge. Its BoxCollider (from the Cube primitive) is the ONLY collider on the
            // panel, so XRGrabInteractable grabs only via the handle — poking the buttons (graphic raycaster) is separate.
            var rt = panel.GetComponent<RectTransform>();
            var existing = panel.transform.Find(HandleName);
            var handle = existing != null ? existing.gameObject : null;
            if (handle == null)
            {
                handle = GameObject.CreatePrimitive(PrimitiveType.Cube); // ships with MeshRenderer + BoxCollider
                handle.name = HandleName;
                handle.transform.SetParent(panel.transform, false);
            }

            float parentScale = Mathf.Max(1e-5f, panel.transform.lossyScale.x);
            Vector3 barWorld;
            Vector3 handleLocalPos;
            if (rt != null)
            {
                // UI canvas: a bar across the top edge (1.1x bigger so the far-grab ray can aim at it).
                float worldW = rt.rect.width * rt.lossyScale.x;
                barWorld = new Vector3(worldW * 0.6f, 0.066f, 0.044f);
                handleLocalPos = new Vector3(0f, rt.rect.height * 0.5f + 0.045f / parentScale, 0f);
            }
            else
            {
                // Non-canvas volume (the ExportTray): a grab bar floating just above the visible platform.
                var box = panel.GetComponent<BoxCollider>();
                var platform = panel.transform.Find("Platform");
                Vector3 c = box != null ? box.center : Vector3.zero;
                float yTop = (platform != null ? platform.localPosition.y : c.y) + 0.3f;
                barWorld = new Vector3(0.5f, 0.1f, 0.1f);
                handleLocalPos = new Vector3(c.x, yTop, c.z);
            }

            handle.transform.localRotation = Quaternion.identity;
            handle.transform.localScale = barWorld / parentScale;
            handle.transform.localPosition = handleLocalPos;

            var mr = handle.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (sh != null) mr.sharedMaterial = new Material(sh) { color = CyanAccent };
            }

            // XRGrabInteractable on the panel ROOT — auto-collects the handle collider at runtime (the only collider).
            var grab = panel.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = panel.AddComponent<XRGrabInteractable>();
            var gso = new SerializedObject(grab);
            SetEnum(gso, "m_MovementType", 2);  // Instantaneous
            SetEnum(gso, "m_FarAttachMode", 1); // Near → far ray-grab reels the panel to the hand
            SetBool(gso, "m_ThrowOnDetach", false);
            SetBool(gso, "m_TrackPosition", true);
            SetBool(gso, "m_TrackRotation", true);
            gso.ApplyModifiedPropertiesWithoutUndo();

            // GrabbableUIPanel for session persistence.
            var gp = panel.GetComponent<GrabbableUIPanel>();
            if (gp == null) gp = panel.AddComponent<GrabbableUIPanel>();
            var pso = new SerializedObject(gp);
            var keyProp = pso.FindProperty("m_Key"); if (keyProp != null) keyProp.stringValue = panel.name;
            var grabProp = pso.FindProperty("m_Grab"); if (grabProp != null) grabProp.objectReferenceValue = grab;
            pso.ApplyModifiedPropertiesWithoutUndo();

            // If this panel is the toolbar, wire its m_GrabPanel so it stops head-following once placed.
            var toolbar = panel.GetComponent<ARUtilityToolbar>();
            if (toolbar != null)
            {
                var tso = new SerializedObject(toolbar);
                var prop = tso.FindProperty("m_GrabPanel");
                if (prop != null) { prop.objectReferenceValue = gp; tso.ApplyModifiedPropertiesWithoutUndo(); }
            }

            EditorUtility.SetDirty(panel);
            Debug.Log("[ARGrabbablePanelSetup] Made '" + panel.name + "' grabbable (handle + XRGrabInteractable + GrabbableUIPanel).");
        }

        static void SetupTrayBoundary()
        {
            var tray = GameObject.Find(TrayName);
            if (tray == null) { Debug.LogWarning("[ARGrabbablePanelSetup] '" + TrayName + "' not found — tray boundary skipped."); return; }
            var box = tray.GetComponent<BoxCollider>();

            var existing = tray.transform.Find(BoundaryName);
            var go = existing != null ? existing.gameObject : null;
            if (go == null)
            {
                go = new GameObject(BoundaryName);
                go.transform.SetParent(tray.transform, false);
            }

            var boundary = go.GetComponent<ExportTrayBoundary>();
            if (boundary == null) boundary = go.AddComponent<ExportTrayBoundary>(); // RequireComponent adds the LineRenderer

            var bso = new SerializedObject(boundary);
            var vProp = bso.FindProperty("m_Volume");
            if (vProp != null && box != null) vProp.objectReferenceValue = box;
            bso.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(go);
            Debug.Log("[ARGrabbablePanelSetup] ExportTray boundary added/updated.");
        }

        static void SetBool(SerializedObject so, string field, bool val)
        {
            var p = so.FindProperty(field);
            if (p != null) p.boolValue = val;
            else Debug.LogWarning("[ARGrabbablePanelSetup] '" + field + "' not found on " + so.targetObject.GetType().Name + ".");
        }

        static void SetEnum(SerializedObject so, string field, int enumIndex)
        {
            var p = so.FindProperty(field);
            if (p != null) p.enumValueIndex = enumIndex;
            else Debug.LogWarning("[ARGrabbablePanelSetup] '" + field + "' not found on " + so.targetObject.GetType().Name + ".");
        }

        static bool OpenScene()
        {
            if (EditorSceneManager.GetActiveScene().path == ScenePath) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath);
            return EditorSceneManager.GetActiveScene().path == ScenePath;
        }
    }
}
#endif
