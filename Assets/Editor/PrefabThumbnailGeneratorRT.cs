using System.IO;
using UnityEditor;
using UnityEngine;

public class PrefabThumbnailGeneratorRT : EditorWindow
{
    DefaultAsset prefabsFolder;
    DefaultAsset outputFolder;

    int size = 512;
    float padding = 0.9f; // מומלץ להתחיל 0.85-1.0
    Color background = new Color(0, 0, 0, 0); // שקוף

    [MenuItem("Tools/Thumbnails/Generate Prefab Thumbnails (Camera)")]
    static void Open() => GetWindow<PrefabThumbnailGeneratorRT>("Prefab Thumbnails (Camera)");

    void OnGUI()
    {
        prefabsFolder = (DefaultAsset)EditorGUILayout.ObjectField("Prefabs Folder", prefabsFolder, typeof(DefaultAsset), false);
        outputFolder  = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder",  outputFolder,  typeof(DefaultAsset), false);

        size = EditorGUILayout.IntField("Size (px)", size);
        padding = EditorGUILayout.FloatField("Padding", padding);
        background = EditorGUILayout.ColorField("Background", background);

        if (GUILayout.Button("Generate"))
            Generate();
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

        var stage = new GameObject("__ThumbStage");
        stage.hideFlags = HideFlags.HideAndDontSave;

        var camGO = new GameObject("__ThumbCam");
        camGO.hideFlags = HideFlags.HideAndDontSave;
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.orthographic = false;
        cam.enabled = false;
        cam.fieldOfView = 25f; // זום אין

        var lightGO = new GameObject("__ThumbLight");
        lightGO.hideFlags = HideFlags.HideAndDontSave;
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        Vector3 camDir = (new Vector3(1, 0.8f, 1)).normalized;

        foreach (var guid in guids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) continue;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.hideFlags = HideFlags.HideAndDontSave;
            inst.transform.SetParent(stage.transform, false);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;

            var rends = inst.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0)
            {
                Debug.LogWarning($"No renderers in {prefab.name}, skipping.");
                DestroyImmediate(inst);
                continue;
            }

            // Bounds לפני יישור
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            // יישור: הבסיס על Y=0, ומרכז ב-XZ
            Vector3 center0 = b.center;
            Vector3 bottom = new Vector3(center0.x, b.min.y, center0.z);
            inst.transform.position -= bottom;

            // Bounds אחרי יישור (חייב)
            rends = inst.GetComponentsInChildren<Renderer>(true);
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            Vector3 center = b.center;
            float radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z) * padding;

            // מרחק לפי FOV (מונע חיתוך ומבטיח פריים מלא)
            float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
            float dist = radius / Mathf.Sin(fovRad * 0.5f);

            cam.transform.position = center + camDir * dist;
            cam.transform.LookAt(center);

            cam.nearClipPlane = Mathf.Max(0.001f, dist - radius * 3f);
            cam.farClipPlane  = dist + radius * 6f;

            // Render
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            cam.Render();

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();

            RenderTexture.active = prev;

            byte[] png = tex.EncodeToPNG();
            DestroyImmediate(tex);

            string file = Path.Combine(outPath, prefab.name + ".png");
            File.WriteAllBytes(file, png);

            DestroyImmediate(inst);
        }

        DestroyImmediate(rt);
        DestroyImmediate(lightGO);
        DestroyImmediate(camGO);
        DestroyImmediate(stage);

        AssetDatabase.Refresh();
        Debug.Log("Done generating camera thumbnails.");
    }
}
