using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;

namespace XRMultiplayer.EditorTools
{
    public static class WastewaterTreatmentPlantAnimationSetup
    {
        [MenuItem("WaterVision/Build Wastewater Treatment Plant Animation")]
        public static void BuildWastewaterTreatmentPlantAnimation()
        {
            GameObject parent = GameObject.Find("Simulation Area");
            if (parent == null)
            {
                parent = new GameObject("Simulation Area");
                Undo.RegisterCreatedObjectUndo(parent, "Create Simulation Area");
            }

            Transform existing = parent.transform.Find("Wastewater Treatment Plant Animation");
            GameObject root = existing != null ? existing.gameObject : new GameObject("Wastewater Treatment Plant Animation");
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(root, "Create Wastewater Treatment Plant Animation");
                root.transform.SetParent(parent.transform, false);
            }

            root.transform.localPosition = new Vector3(0f, 0f, 8f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Type animationType = Type.GetType("XRMultiplayer.WastewaterTreatmentPlantAnimation, NewAssembly");
            if (animationType == null)
            {
                animationType = Type.GetType("XRMultiplayer.WastewaterTreatmentPlantAnimation, Assembly-CSharp");
            }

            if (animationType == null)
            {
                Debug.LogError("Could not find XRMultiplayer.WastewaterTreatmentPlantAnimation. Make sure the runtime script compiled successfully.");
                return;
            }

            Component animation = root.GetComponent(animationType);
            if (animation == null)
            {
                animation = Undo.AddComponent(root, animationType);
            }

            animationType.GetMethod("RebuildPreview")?.Invoke(animation, null);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("WaterVision WWTP animation generated in SampleScene.");
        }
    }
}
