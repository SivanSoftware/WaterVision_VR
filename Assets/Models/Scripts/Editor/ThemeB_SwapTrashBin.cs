using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Replaces the plain red cylinder visual of the Recycle Bin (Room/TrashBin/Body) with the office
/// trash-can model, recoloured to a recycle teal-green that matches the app theme. Keeps the trigger
/// collider, the PipeTrashBin script and the "RECYCLE BIN" label untouched. Idempotent (re-runnable);
/// operates on the OPEN scene and DOES NOT SAVE — review the dirty scene, then save manually.
///
/// Run: Tools/Theme B/Swap Recycle Bin Model
/// </summary>
public static class ThemeB_SwapTrashBin
{
    const string MODEL = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)TrashCan.prefab";
    const string MAT   = "Assets/Models/Asset/UI/Theme_C/BinTeal.mat";
    const float  TARGET_HEIGHT = 1.5f; // world units, a touch shorter than the old 1.7 cylinder

    static readonly Color BinTeal = new Color(0.022f, 0.12f, 0.145f, 1f); // Export-panel background teal (#04161b)

    [MenuItem("Tools/Theme B/Swap Recycle Bin Model")]
    public static void Swap()
    {
        // locate TrashBin via its component (robust)
        GameObject bin = null;
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent("PipeTrashBin") != null) { bin = t.gameObject; break; }
            if (bin != null) break;
        }
        if (bin == null) { Debug.LogError("[ThemeB] TrashBin (PipeTrashBin) not found in the open scene."); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL);
        if (prefab == null) { Debug.LogError("[ThemeB] Trash-can model missing: " + MODEL); return; }
        var mat = GetOrCreateMat();

        // Hide the old red cylinder (kept, just disabled, for easy revert).
        var body = bin.transform.Find("Body");
        if (body != null) body.gameObject.SetActive(false);

        // Fresh instance each run (idempotent).
        var old = bin.transform.Find("BinModel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.name = "BinModel";
        inst.transform.SetParent(bin.transform, false);
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        var rends = inst.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) { Debug.LogWarning("[ThemeB] Model has no renderers."); return; }

        // Scale to target height.
        Bounds b = rends[0].bounds; for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        float h = Mathf.Max(0.0001f, b.size.y);
        float s = TARGET_HEIGHT / h;
        inst.transform.localScale = Vector3.one * s;

        // Re-measure, then seat base on the bin floor + centre on the bin in XZ.
        rends = inst.GetComponentsInChildren<Renderer>(true);
        b = rends[0].bounds; for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        Vector3 binPos = bin.transform.position;
        Vector3 offset = new Vector3(binPos.x - b.center.x, binPos.y - b.min.y, binPos.z - b.center.z);
        inst.transform.position += offset;

        // Recolour every material slot to the recycle teal-green.
        foreach (var r in rends)
        {
            var arr = r.sharedMaterials;
            for (int i = 0; i < arr.Length; i++) arr[i] = mat;
            r.sharedMaterials = arr;
        }

        EditorUtility.SetDirty(bin);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[ThemeB] Swapped Recycle Bin → office trash-can (teal). Scene UNSAVED — review, then save.");
    }

    static Material GetOrCreateMat()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MAT);
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(sh) { name = "BinTeal" };
            AssetDatabase.CreateAsset(mat, MAT);
        }
        mat.color = BinTeal;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", BinTeal);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.15f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
