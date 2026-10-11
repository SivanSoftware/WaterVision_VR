using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace XRMultiplayer.EditorTools
{
    public static class PreliminaryScreeningUnitDigitalTwinSetup
    {
        [MenuItem("WaterVision/Build Component 01 - Preliminary Screening Unit")]
        public static void BuildPreliminaryScreeningUnit()
        {
            GameObject parent = GameObject.Find("Simulation Area");
            if (parent == null)
            {
                parent = new GameObject("Simulation Area");
                Undo.RegisterCreatedObjectUndo(parent, "Create Simulation Area");
            }

            Transform existing = parent.transform.Find("Component 01 - Preliminary Screening Unit");
            GameObject root = existing != null ? existing.gameObject : new GameObject("Component 01 - Preliminary Screening Unit");
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(root, "Create Preliminary Screening Unit");
                root.transform.SetParent(parent.transform, false);
            }

            root.transform.localPosition = new Vector3(0f, 0f, 16f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Type componentType = Type.GetType("XRMultiplayer.PreliminaryScreeningUnitDigitalTwin, NewAssembly");
            if (componentType == null)
            {
                componentType = Type.GetType("XRMultiplayer.PreliminaryScreeningUnitDigitalTwin, Assembly-CSharp");
            }

            if (componentType == null)
            {
                Debug.LogError("Could not find XRMultiplayer.PreliminaryScreeningUnitDigitalTwin. Make sure the runtime script compiled successfully.");
                return;
            }

            Component component = root.GetComponent(componentType);
            if (component == null)
            {
                component = Undo.AddComponent(root, componentType);
            }

            componentType.GetMethod("Rebuild")?.Invoke(component, null);
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("WaterVision Component 01 preliminary screening digital twin generated.");
        }
    }
}
