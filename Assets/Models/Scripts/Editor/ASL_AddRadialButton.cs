using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds a "Sign" button (RadialButton.prefab instance + SignLanguageRadialButton auto-wire component)
/// to the RadialMenu prefab so users can toggle the ASL bubble. Prefab edit only — no scene touched.
///
/// The RadialMenu/RadialButton prefabs contain a pre-existing MISSING SCRIPT (a leftover dead component
/// whose .cs was removed), which blocks Unity from saving the prefab. "Repair" removes those dead
/// (null) components — they do nothing at runtime — so the prefab becomes valid + saveable.
///
/// Run: Tools/ASL/Report Missing Scripts  (diagnose), then  Tools/ASL/Repair + Add Sign Button.
/// </summary>
public static class ASL_AddRadialButton
{
    const string MENU = "Assets/UI/RadialMenu/Prefabs/RadialMenu.prefab";
    const string BTN  = "Assets/UI/RadialMenu/Prefabs/RadialButton.prefab";

    [MenuItem("Tools/ASL/Report Missing Scripts")]
    public static void Report()
    {
        var sb = new StringBuilder("[ASL] Missing-script report:\n");
        foreach (var path in new[] { BTN, MENU })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int total = 0;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (n > 0) { sb.Append($"  {path} : {GetPath(t)} -> {n} missing\n"); total += n; }
                }
                sb.Append($"  == {path}: {total} missing ==\n");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/ASL/Repair + Add Sign Button")]
    public static void RepairAndAdd()
    {
        // 1) Repair the base RadialButton.prefab (its dead components propagate to every instance).
        RepairPrefab(BTN);

        // 2) Repair + add the button to RadialMenu.prefab.
        var btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BTN);
        if (btnPrefab == null) { Debug.LogError("[ASL] RadialButton prefab missing: " + BTN); return; }

        var root = PrefabUtility.LoadPrefabContents(MENU);
        try
        {
            int removed = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            var existing = root.transform.Find("RadialButton_SignLanguage");
            GameObject inst = existing != null
                ? existing.gameObject
                : (GameObject)PrefabUtility.InstantiatePrefab(btnPrefab, root.transform);
            inst.name = "RadialButton_SignLanguage";

            if (inst.GetComponent<SignLanguageRadialButton>() == null)
                inst.AddComponent<SignLanguageRadialButton>();

            var label = inst.transform.Find("Label ");
            if (label != null) { var tmp = label.GetComponent<TextMeshProUGUI>(); if (tmp) tmp.text = "Sign"; }

            PrefabUtility.SaveAsPrefabAsset(root, MENU);
            Debug.Log($"[ASL] RadialMenu: removed {removed} dead component(s), added RadialButton_SignLanguage. Saved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void RepairPrefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int removed = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            if (removed > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); Debug.Log($"[ASL] Repaired {path}: removed {removed} dead component(s)."); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static string GetPath(Transform t)
    {
        var sb = new StringBuilder(t.name);
        while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
        return sb.ToString();
    }
}
