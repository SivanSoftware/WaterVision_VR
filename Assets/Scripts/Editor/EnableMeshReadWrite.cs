using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time tool: marks all 3D model assets (FBX, OBJ, etc.) under Assets/Models/ as
/// Read/Write Enabled so their mesh vertex data is kept on the CPU after build.
///
/// Without this, GLTFast's GameObjectExport cannot read mesh data on Android/Quest
/// (Unity discards CPU mesh data in builds when Read/Write is disabled), resulting in
/// empty / degenerate geometry in the exported GLB file.
///
/// Run once before building for Quest: Tools/WaterVision/Enable Mesh Read-Write for Export
/// </summary>
public static class EnableMeshReadWrite
{
    [MenuItem("Tools/WaterVision/Enable Mesh Read-Write for Export (run before Quest build)")]
    public static void EnableAllModelMeshReadWrite()
    {
        // Target only the pipe/model assets used by PipeAssemblyExporter.
        // We mark every model inside Assets/Models/ — a superset that's safe to enable.
        string[] searchFolders = { "Assets/Models" };
        var guids = AssetDatabase.FindAssets("t:Model", searchFolders);

        int marked = 0, already = 0, skipped = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) { skipped++; continue; }

            if (importer.isReadable) { already++; continue; }

            importer.isReadable = true;
            importer.SaveAndReimport();
            marked++;
            Debug.Log($"[EnableMeshReadWrite] Marked readable: {path}");
        }

        Debug.Log($"[EnableMeshReadWrite] Done. Marked {marked} model(s) as Read/Write. " +
                  $"({already} already readable, {skipped} skipped/non-model)");

        EditorUtility.DisplayDialog(
            "Enable Mesh Read-Write",
            $"Done!\n\nMarked {marked} model(s) as Read/Write Enabled.\n" +
            $"{already} were already enabled.\n\n" +
            "Now do a Build & Run for Quest — the pipe export will work correctly.",
            "OK");
    }
}
