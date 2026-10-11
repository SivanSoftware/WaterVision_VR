using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BeitShanMaterialFixer
{
    const string FBX_PATH = "Assets/Models/Asset/Pipes/source/pipes.fbx";
    const string EXTRACT_FOLDER = "Assets/Models/Asset/Pipes/Materials_Extracted";

    [MenuItem("MCP/Beit Shan/Remap Renderers To Extracted Materials")]
    public static void RemapRenderers()
    {
        // 1. Set the FBX importer to use external materials (so future re-imports don't re-embed)
        var importer = AssetImporter.GetAtPath(FBX_PATH) as ModelImporter;
        if (importer != null)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            // SearchAndRemapMaterials looks for matching-name .mat files in the project and remaps internal slots.
            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName,
                                              ModelImporterMaterialSearch.RecursiveUp);
            importer.SaveAndReimport();
            Debug.Log("[BeitShanMaterialFixer] FBX importer reconfigured: External materials, SearchAndRemap done.");
        }

        // 2. Build a map: material name → asset
        var matMap = new Dictionary<string, Material>();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { EXTRACT_FOLDER }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) matMap[m.name] = m;
        }
        Debug.Log($"[BeitShanMaterialFixer] Built map with {matMap.Count} materials.");

        // 3. Scan every MeshRenderer under BeitShanSimulationArea; if material is null or points at the FBX,
        //    look at the GameObject name (Pipes6, Pipes6 (1), Pipes6 (2)…) and assign the matching .mat.
        var area = GameObject.Find("BeitShanSimulationArea");
        if (area == null)
        {
            Debug.LogWarning("[BeitShanMaterialFixer] BeitShanSimulationArea not found. Skipping scene-level remap.");
            return;
        }

        int fixedCount = 0;
        int skipped = 0;
        foreach (var mr in area.GetComponentsInChildren<MeshRenderer>(true))
        {
            // Extract base name (strip ' (N)' suffix and submesh numbers)
            string name = mr.gameObject.name;
            string baseName = name;
            int parenIdx = baseName.IndexOf(" (");
            if (parenIdx > 0) baseName = baseName.Substring(0, parenIdx);

            if (matMap.TryGetValue(baseName, out var mat))
            {
                bool needsAssign = false;
                var current = mr.sharedMaterials;
                for (int i = 0; i < current.Length; i++)
                {
                    if (current[i] == null) { needsAssign = true; break; }
                }
                if (needsAssign || current.Length == 0)
                {
                    var newArr = new Material[Mathf.Max(1, current.Length)];
                    for (int i = 0; i < newArr.Length; i++) newArr[i] = mat;
                    mr.sharedMaterials = newArr;
                    EditorUtility.SetDirty(mr);
                    fixedCount++;
                }
                else
                {
                    skipped++;
                }
            }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"[BeitShanMaterialFixer] Remapped {fixedCount} MeshRenderers, skipped {skipped} (already had materials).");
    }

    [MenuItem("MCP/Beit Shan/Fix Pipe Materials (Extract + URP)")]
    public static void FixPipeMaterials()
    {
        if (!File.Exists(FBX_PATH))
        {
            Debug.LogError($"[BeitShanMaterialFixer] FBX not found at {FBX_PATH}");
            return;
        }

        // Step 1: ensure target folder exists
        if (!AssetDatabase.IsValidFolder(EXTRACT_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/Models/Asset/Pipes", "Materials_Extracted");
            Debug.Log($"[BeitShanMaterialFixer] Created folder {EXTRACT_FOLDER}");
        }

        // Step 2: enumerate all materials embedded in the FBX and extract them
        var subAssets = AssetDatabase.LoadAllAssetsAtPath(FBX_PATH);
        int extracted = 0;
        int alreadyExtracted = 0;
        foreach (var asset in subAssets)
        {
            if (asset is Material mat && AssetDatabase.IsSubAsset(asset))
            {
                string safeName = mat.name.Replace('/', '_').Replace('\\', '_');
                string newPath = $"{EXTRACT_FOLDER}/{safeName}.mat";

                // Skip if a material at that path already exists and the asset already points there
                if (File.Exists(newPath))
                {
                    alreadyExtracted++;
                    continue;
                }

                string error = AssetDatabase.ExtractAsset(mat, newPath);
                if (string.IsNullOrEmpty(error))
                {
                    extracted++;
                    Debug.Log($"[BeitShanMaterialFixer] Extracted {mat.name} → {newPath}");
                }
                else
                {
                    Debug.LogWarning($"[BeitShanMaterialFixer] Could not extract {mat.name}: {error}");
                }
            }
        }

        AssetDatabase.WriteImportSettingsIfDirty(FBX_PATH);
        AssetDatabase.ImportAsset(FBX_PATH, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BeitShanMaterialFixer] Extracted {extracted} new materials, {alreadyExtracted} were already extracted. Total under {EXTRACT_FOLDER}");

        // Step 3: scan extracted materials and switch any Built-in shaders to URP/Lit
        int converted = 0;
        var guids = AssetDatabase.FindAssets("t:Material", new[] { EXTRACT_FOLDER });
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("[BeitShanMaterialFixer] Universal Render Pipeline/Lit shader not found — is URP installed?");
            return;
        }

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null) continue;
            string shaderName = m.shader.name;

            bool isBuiltIn = shaderName == "Standard" ||
                             shaderName == "Standard (Specular setup)" ||
                             shaderName.StartsWith("Legacy Shaders/") ||
                             shaderName == "Hidden/InternalErrorShader";

            if (isBuiltIn)
            {
                // Preserve main texture / color when possible
                Color color = m.HasProperty("_Color") ? m.color : Color.white;
                Texture mainTex = m.HasProperty("_MainTex") ? m.mainTexture : null;

                m.shader = urpLit;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                if (m.HasProperty("_BaseMap") && mainTex != null) m.SetTexture("_BaseMap", mainTex);

                EditorUtility.SetDirty(m);
                converted++;
                Debug.Log($"[BeitShanMaterialFixer] {m.name}: {shaderName} → URP/Lit");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BeitShanMaterialFixer] Done. Converted {converted} materials to URP/Lit. " +
                  "Renderers in the scene that referenced the FBX-embedded materials should now display correctly.");
    }
}
