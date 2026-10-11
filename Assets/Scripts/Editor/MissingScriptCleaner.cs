using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Editor utility that walks the currently-open Prefab Stage and removes every
/// "Missing Mono Script" component from every GameObject in the prefab.
///
/// Background: when a Plastic SCM merge dropped several Sample scripts (Hand
/// Smoothing Post Processor, etc.) the prefab kept dangling MonoBehaviour
/// references to those deleted scripts. Unity refuses to save a prefab with
/// missing scripts, so editing the prefab in any way was blocked until those
/// references were stripped.
///
/// Usage:
///   1. Open the broken prefab in Prefab Stage (double-click it in Project).
///   2. Run "Tools / Cleanup / Remove Missing Scripts In Open Prefab".
///   3. Save the prefab (Ctrl+S). It should now save without errors.
/// </summary>
public static class MissingScriptCleaner
{
    const string k_MenuPath = "Tools/Cleanup/Remove Missing Scripts In Open Prefab";

    [MenuItem(k_MenuPath)]
    public static void CleanCurrentPrefabStage()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null)
        {
            EditorUtility.DisplayDialog(
                "No Prefab Stage Open",
                "Open the prefab you want to clean first (double-click it in the Project window), then run this menu again.",
                "OK");
            return;
        }

        var prefabRoot = stage.prefabContentsRoot;
        Debug.Log($"[MissingScriptCleaner] Walking prefab '{prefabRoot.name}' …");

        int goCount = 0;
        int totalRemoved = 0;
        var stack = new Stack<Transform>();
        stack.Push(prefabRoot.transform);

        while (stack.Count > 0)
        {
            var t = stack.Pop();
            goCount++;
            int removedHere = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            if (removedHere > 0)
            {
                totalRemoved += removedHere;
                Debug.Log($"[MissingScriptCleaner] Removed {removedHere} missing script(s) from '{GetHierarchyPath(t)}'.", t.gameObject);
            }
            for (int i = 0; i < t.childCount; i++)
                stack.Push(t.GetChild(i));
        }

        EditorSceneManager.MarkSceneDirty(stage.scene);

        string msg = $"Walked {goCount} GameObjects in '{prefabRoot.name}'.\n" +
                     $"Removed {totalRemoved} missing-script component(s).\n\n" +
                     (totalRemoved > 0
                        ? "Now save the prefab (Ctrl+S in the Prefab Stage)."
                        : "Nothing to remove — the prefab was already clean.");
        Debug.Log($"[MissingScriptCleaner] DONE. {msg.Replace("\n\n", " | ").Replace("\n", " ")}");
        EditorUtility.DisplayDialog("Missing Script Cleaner", msg, "OK");
    }

    static string GetHierarchyPath(Transform t)
    {
        if (t.parent == null) return t.name;
        return GetHierarchyPath(t.parent) + "/" + t.name;
    }
}
