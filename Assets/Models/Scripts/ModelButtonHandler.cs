using UnityEngine;

public class ModelButtonHandler : MonoBehaviour
{
    public NetworkModelSpawner spawner;
    public int modelIndex;

    // 0 = uncategorized (shown only in "All").
    // 1 = Straight & Bends, 2 = Junctions, 3 = Multi-pipe, 4 = Equipment.
    // Free-form int — the ModelCategoryTabs controller filters by exact match
    // or by the special "All" tab (categoryIndex = -1 / 0 shows everything).
    public int categoryIndex;

    void Awake()
    {
        // Auto-find the scene's NetworkModelSpawner if not wired in the prefab.
        // The picker UI lives in the Models Room and is no longer popped up from
        // the radial menu, so the scene's lone spawner instance is the canonical
        // target — no manual per-button wiring required.
        if (spawner == null)
        {
#if UNITY_2023_1_OR_NEWER
            spawner = Object.FindFirstObjectByType<NetworkModelSpawner>(FindObjectsInactive.Include);
#else
            spawner = Object.FindObjectOfType<NetworkModelSpawner>(true);
#endif
        }
    }

    public void SpawnModel()
    {
        Debug.Log(">>> SpawnModel CLICKED <<<");

        if (spawner == null)
        {
#if UNITY_2023_1_OR_NEWER
            spawner = Object.FindFirstObjectByType<NetworkModelSpawner>(FindObjectsInactive.Include);
#else
            spawner = Object.FindObjectOfType<NetworkModelSpawner>(true);
#endif
        }

        if (spawner == null)
        {
            Debug.LogError("Spawner is NULL!");
            return;
        }

        spawner.RequestSpawn(modelIndex);
    }

}
