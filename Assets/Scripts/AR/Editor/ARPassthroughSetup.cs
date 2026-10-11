#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterVision.AR;

namespace WaterVision.AR.EditorTools
{
    /// <summary>
    /// Creates the inactive "MetaPassthrough" GameObject (OVRManager + OVRPassthroughLayer, located by REFLECTION so
    /// this compiles with no hard reference to the Meta SDK assembly), enables Insight Passthrough, and assigns it to
    /// ARModeController.m_PassthroughRoot (+ the keep-alive menu canvases). ARModeController activates this object in
    /// AR so the grayscale Quest-2 passthrough underlay shows behind the transparent eye buffer. If the Meta types
    /// aren't found, it logs how to add them via the Meta XR "Passthrough" Building Block. Idempotent.
    /// </summary>
    public static class ARPassthroughSetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string RootName = "MetaPassthrough";

        [MenuItem("WaterVision/AR/Setup Meta Passthrough")]
        public static void Setup()
        {
            if (!OpenScene()) return;
            var scene = EditorSceneManager.GetActiveScene();

            var existing = GameObject.Find(RootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            var ovrManagerType = FindType("OVRManager");
            var passthroughLayerType = FindType("OVRPassthroughLayer");

            var root = new GameObject(RootName);
            if (ovrManagerType != null && passthroughLayerType != null)
            {
                var mgr = root.AddComponent(ovrManagerType);
                root.AddComponent(passthroughLayerType); // OVRPassthroughLayer defaults: Reconstructed, Underlay
                TrySetBool(mgr, "isInsightPassthroughEnabled", true);
                Debug.Log("[ARPassthroughSetup] Added OVRManager + OVRPassthroughLayer to '" + RootName + "'. " +
                          "Verify in the inspector: OVRManager 'Insight Passthrough Enabled' = ON, OVRPassthroughLayer placement = Underlay.");
            }
            else
            {
                Debug.LogWarning("[ARPassthroughSetup] Meta SDK OVRManager/OVRPassthroughLayer not found by reflection. Add them manually: " +
                                 "Meta XR ▸ Tools ▸ Building Blocks ▸ Passthrough (adds OVRManager + OVRPassthroughLayer), or add both " +
                                 "components to the '" + RootName + "' object — set Insight Passthrough ON + the layer to Underlay.");
            }

            root.SetActive(false); // ARModeController activates it on Enter AR

            var ctrl = UnityEngine.Object.FindFirstObjectByType<ARModeController>(FindObjectsInactive.Include);
            if (ctrl != null)
            {
                var so = new SerializedObject(ctrl);
                var rootProp = so.FindProperty("m_PassthroughRoot");
                if (rootProp != null) rootProp.objectReferenceValue = root;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ctrl);
                Debug.Log("[ARPassthroughSetup] Wired '" + RootName + "' → ARModeController.m_PassthroughRoot.");
            }
            else
            {
                Debug.LogWarning("[ARPassthroughSetup] No ARModeController found — assign '" + RootName + "' to its m_PassthroughRoot manually.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static bool OpenScene()
        {
            if (EditorSceneManager.GetActiveScene().path == ScenePath) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath);
            return EditorSceneManager.GetActiveScene().path == ScenePath;
        }

        static Type FindType(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(simpleName, throwOnError: false);
                if (t != null) return t;
                foreach (var tt in SafeGetTypes(asm))
                    if (tt.Name == simpleName) return tt;
            }
            return null;
        }

        static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch { return Array.Empty<Type>(); }
        }

        static void TrySetBool(Component c, string serializedField, bool value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(serializedField);
            if (p != null) { p.boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning("[ARPassthroughSetup] Couldn't find serialized '" + serializedField + "' on " + c.GetType().Name +
                                  " — set it ON manually in the inspector.");
        }
    }
}
#endif
