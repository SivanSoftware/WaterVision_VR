#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Meta.XR.MRUtilityKit;
using WaterVision.AR;

namespace WaterVision.AR.EditorTools
{
    /// <summary>
    /// Adds the MRUK singleton (loads the real room scan from device on start — defaults are DataSource=Device,
    /// LoadSceneOnStartup=true) and the ARWorkspacePlacer (snaps the room menus onto the real walls / in front of the
    /// user on AR enter, and brings the ExportTray within reach). Idempotent. Run AFTER the other AR setups so the
    /// menus, ExportTray, and ARModeController already exist.
    /// </summary>
    public static class ARWorkspaceSetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string MrukName = "MR Utility Kit";

        [MenuItem("WaterVision/AR/Setup AR Workspace (walls)")]
        public static void Setup()
        {
            if (!OpenScene()) return;
            var scene = EditorSceneManager.GetActiveScene();

            // 1) MRUK singleton. Defaults (DataSource=Device, LoadSceneOnStartup=true) auto-load the real scan on start.
            var mruk = Object.FindFirstObjectByType<MRUK>(FindObjectsInactive.Include);
            if (mruk == null)
            {
                var go = new GameObject(MrukName);
                go.AddComponent<MRUK>();
                Debug.Log("[ARWorkspaceSetup] Added MRUK singleton (loads the room scan from device on start).");
            }

            // 2) ARWorkspacePlacer on the ARModeController object, wired to the menus + tray.
            var ctrl = Object.FindFirstObjectByType<ARModeController>(FindObjectsInactive.Include);
            if (ctrl == null) { Debug.LogWarning("[ARWorkspaceSetup] No ARModeController found — run 'Setup AR UI Button' first."); return; }

            var placer = ctrl.GetComponent<ARWorkspacePlacer>();
            if (placer == null) placer = ctrl.gameObject.AddComponent<ARWorkspacePlacer>();

            var so = new SerializedObject(placer);
            so.FindProperty("m_Controller").objectReferenceValue = ctrl;

            var panels = so.FindProperty("m_Panels");
            panels.arraySize = 0;
            foreach (var n in new[] { "ModelMenuStation_Canvas", "ExportStation_Canvas" })
            {
                var go = GameObject.Find(n);
                if (go == null) { Debug.LogWarning("[ARWorkspaceSetup] Menu '" + n + "' not found — skipped."); continue; }
                int i = panels.arraySize;
                panels.arraySize = i + 1;
                panels.GetArrayElementAtIndex(i).objectReferenceValue = go.transform;
            }

            var tray = GameObject.Find("ExportTray");
            so.FindProperty("m_Tray").objectReferenceValue = tray != null ? tray.transform : null;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(placer);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ARWorkspaceSetup] Done — MRUK + ARWorkspacePlacer wired (" + panels.arraySize + " menus, tray=" + (tray != null) + ").");
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
