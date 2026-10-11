using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterVision;

/// <summary>
/// One-click wiring for networked avatar selection. Idempotent — safe to re-run.
///   1. Creates/updates Assets/Resources/AvatarCatalog.asset (6 entries; index 0 = base Ch08).
///   2. Creates/updates a NetworkPrefabsList with the 5 VARIANT prefabs (Ch08 is the auto-
///      registered PlayerPrefab, so it's intentionally omitted) and appends it to the open
///      scene's NetworkManager NetworkConfig so remotes can instantiate variants by hash.
///   3. Ensures one AvatarSpawnController exists in the open scene (it assigns NGO's
///      OnFetchLocalPlayerPrefabToSpawn at runtime).
///
/// Run with SampleScene open: Tools ▸ WaterVision ▸ Setup Avatar Selection.
/// </summary>
public static class AvatarSelectionSetup
{
    static readonly (string name, string path)[] k_Avatars =
    {
        ("Ch08 (Default)", "Assets/Mixamo/Mixamo_Network_Player_Avatar.prefab"),
        ("Ch07",           "Assets/Mixamo/Avatars/Ch07_Network_Player_Avatar.prefab"),
        ("Ch22",           "Assets/Mixamo/Avatars/Ch22_Network_Player_Avatar.prefab"),
        ("Ch23",           "Assets/Mixamo/Avatars/Ch23_Network_Player_Avatar.prefab"),
        ("Ch33",           "Assets/Mixamo/Avatars/Ch33_Network_Player_Avatar.prefab"),
        ("Ch37",           "Assets/Mixamo/Avatars/Ch37_Network_Player_Avatar.prefab"),
    };
    const string k_CatalogPath = "Assets/Resources/AvatarCatalog.asset";
    const string k_ListPath    = "Assets/Mixamo/Avatars/WaterVisionAvatarPrefabsList.asset";

    [MenuItem("Tools/WaterVision/Setup Avatar Selection")]
    public static void Setup()
    {
        // --- 1) AvatarCatalog (preserve any already-baked thumbnails) ---
        var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(k_CatalogPath);
        if (catalog == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
            AssetDatabase.CreateAsset(catalog, k_CatalogPath);
        }
        var newEntries = new List<AvatarCatalog.Entry>();
        foreach (var (name, path) in k_Avatars)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogWarning($"[AvatarSelectionSetup] prefab missing: {path}");
            var prior = catalog.entries?.FirstOrDefault(e => e != null && e.prefab == go);
            newEntries.Add(new AvatarCatalog.Entry { displayName = name, prefab = go, thumbnail = prior?.thumbnail });
        }
        catalog.entries = newEntries.ToArray();
        EditorUtility.SetDirty(catalog);

        // --- 2) Variant network-prefab registration is done at RUNTIME by AvatarSpawnController
        // (NetworkManager.AddNetworkPrefab). We deliberately do NOT create a NetworkPrefabsList asset
        // or modify the scene NetworkManager's NetworkConfig here: that override serialized under a
        // conflicting property path (NetworkConfig vs m_NetworkConfig) and silently failed to register
        // the variants, breaking player spawn. Nothing to do for registration in the editor. ---
        var nm = Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
        if (nm == null)
            Debug.LogWarning("[AvatarSelectionSetup] No NetworkManager in the open scene (only needed for the scene-dirty save).");

        // --- 3) Ensure an AvatarSpawnController exists in the scene ---
        var controller = Object.FindFirstObjectByType<AvatarSpawnController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            var go = new GameObject("AvatarSpawnController");
            go.AddComponent<AvatarSpawnController>();
            if (nm != null) EditorSceneManager.MarkSceneDirty(nm.gameObject.scene);
            else EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        AssetDatabase.SaveAssets();
        if (nm != null) EditorSceneManager.MarkSceneDirty(nm.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[AvatarSelectionSetup] Done. Catalog entries: {catalog.entries.Length}, " +
                  $"AvatarSpawnController: {(controller != null ? "existing" : "created")}. " +
                  $"(Variants register at runtime via AvatarSpawnController.)");
    }
}
