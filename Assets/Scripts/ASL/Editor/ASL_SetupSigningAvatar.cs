using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WaterVision;

/// <summary>
/// Creates Assets/Resources/ASL/SigningAvatar.prefab — a networking-stripped copy of the first
/// AvatarCatalog entry — which ASLSigningCharacterManager loads at runtime without needing a
/// NetworkManager.  Run once (or re-run after importing new avatars):
///   Tools → ASL → Create Signing Avatar Prefab
/// </summary>
public static class ASL_SetupSigningAvatar
{
    const string MENU         = "Tools/ASL/Create Signing Avatar Prefab";
    const string OUT_FOLDER   = "Assets/Resources/ASL";
    const string OUT_PATH     = "Assets/Resources/ASL/SigningAvatar.prefab";
    const string CATALOG_PATH = "Assets/Resources/AvatarCatalog.asset";

    [MenuItem(MENU)]
    public static void Create()
    {
        // 1 ── load catalog
        var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(CATALOG_PATH);
        if (catalog == null)
        {
            Debug.LogError("[ASL-Setup] AvatarCatalog not found at: " + CATALOG_PATH +
                           "\nCreate it first with Tools/ASL/... or check path.");
            return;
        }
        if (catalog.Count == 0)
        {
            Debug.LogError("[ASL-Setup] AvatarCatalog has no entries.");
            return;
        }

        var srcPrefab = catalog.GetPrefab(0);
        if (srcPrefab == null)
        {
            Debug.LogError("[ASL-Setup] AvatarCatalog entry 0 has a null prefab.");
            return;
        }
        string srcPath = AssetDatabase.GetAssetPath(srcPrefab);
        if (string.IsNullOrEmpty(srcPath))
        {
            Debug.LogError("[ASL-Setup] Source prefab has no asset path.");
            return;
        }

        // 2 ── ensure output folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(OUT_FOLDER))
            AssetDatabase.CreateFolder("Assets/Resources", "ASL");

        // 3 ── load prefab contents into a temp scene for editing
        var root = PrefabUtility.LoadPrefabContents(srcPath);
        try
        {
            int removed = 0;

            // 3a — remove missing-script stubs (harmless but tidy)
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            // 3b — collect all MonoBehaviours whose type contains "Network" then remove them
            //       (covers NetworkObject, NetworkBehaviour, NetworkTransform, NetworkAnimator, etc.)
            //       NetworkObject is a MonoBehaviour in NGO so this catches it.
            var netComponents = new List<MonoBehaviour>();
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string fn = mb.GetType().FullName ?? "";
                if (fn.Contains("Network")) netComponents.Add(mb);
            }
            // Remove NetworkObject LAST (other NetworkBehaviours depend on it)
            netComponents.Sort((a, b) =>
            {
                bool aIsNO = a.GetType().Name == "NetworkObject";
                bool bIsNO = b.GetType().Name == "NetworkObject";
                if (aIsNO && !bIsNO) return  1;
                if (!aIsNO && bIsNO) return -1;
                return 0;
            });
            foreach (var mb in netComponents)
            {
                if (mb != null)
                {
                    Object.DestroyImmediate(mb);
                    removed++;
                }
            }

            // 3c — remove physics / CharacterController
            foreach (var cc in root.GetComponentsInChildren<CharacterController>(true))
            { Object.DestroyImmediate(cc); removed++; }
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
            { Object.DestroyImmediate(rb); removed++; }

            // 4 ── save stripped prefab
            var saved = PrefabUtility.SaveAsPrefabAsset(root, OUT_PATH);
            AssetDatabase.Refresh();

            if (saved != null)
                Debug.Log($"[ASL-Setup] ✓ Signing avatar prefab saved to {OUT_PATH} " +
                          $"(removed {removed} components).\n" +
                          $"Source: {srcPath}  |  Avatar: {catalog.GetName(0)}");
            else
                Debug.LogError("[ASL-Setup] SaveAsPrefabAsset returned null — check Console for errors.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem(MENU, validate = true)]
    static bool Validate() => !EditorApplication.isPlaying;
}
