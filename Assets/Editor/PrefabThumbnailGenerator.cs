using System.IO;
using UnityEditor;
using UnityEngine;

public class PrefabThumbnailGenerator : EditorWindow
{
    DefaultAsset prefabsFolder;
    DefaultAsset outputFolder;

    int size = 512;

    [MenuItem("Tools/Thumbnails/Generate Prefab Thumbnails")]
    static void Open() => GetWindow<PrefabThumbnailGenerator>("Prefab Thumbnails");

    void OnGUI()
    {
        prefabsFolder = (DefaultAsset)EditorGUILayout.ObjectField("Prefabs Folder", prefabsFolder, typeof(DefaultAsset), false);
        outputFolder  = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder",  outputFolder,  typeof(DefaultAsset), false);

        size = EditorGUILayout.IntField("Size (px)", size);

        if (GUILayout.Button("Generate"))
        {
            Generate();
        }
    }

    void Generate()
    {
        if (prefabsFolder == null || outputFolder == null)
        {
            Debug.LogError("Choose both Prefabs Folder and Output Folder.");
            return;
        }

        string prefabsPath = AssetDatabase.GetAssetPath(prefabsFolder);
        string outPath = AssetDatabase.GetAssetPath(outputFolder);

        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabsPath });
        Debug.Log($"Found {guids.Length} prefabs.");

        foreach (var guid in guids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) continue;

            var tex = AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab);
            if (tex == null)
            {
                // לפעמים צריך להמתין שה-AssetPreview ייטען, פשוט דלג ותפעיל שוב
                Debug.LogWarning($"No preview yet for {prefab.name}. Run again if needed.");
                continue;
            }

            // הפוך ל-Texture2D readable
            Texture2D readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            // Resize ל-size שבחרת (פשוט: נשתמש ב-EncodePNG כמו שהוא, או אפשר לשפר)
            byte[] png = readable.EncodeToPNG();
            Object.DestroyImmediate(readable);

            string file = Path.Combine(outPath, prefab.name + ".png");
            File.WriteAllBytes(file, png);
        }

        AssetDatabase.Refresh();
        Debug.Log("Done. (If some were skipped due to preview loading, click Generate again.)");
    }
}
