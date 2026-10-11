using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-shot tool: re-renders the 50 PipeSet thumbnails with a TRANSPARENT background and tight
/// framing (the originals came from AssetPreview, which bakes a grey gradient + grid floor into the
/// PNG, leaving the actual model tiny and lost). Renders each prefab through a private camera using
/// the URP render-request API so the dark-teal "blueprint" cell shows through behind every model.
///
/// Source : Assets/Models/Asset/PipesPack/prefab/PipeSet
/// Output : Assets/Resources/Thumbnails   (overwrites PipeSet_*.png in place)
///
/// Run: Tools/Theme C/Rebake Pipe Thumbnails (Transparent)
/// </summary>
public static class ThemeC_RebakeThumbnails
{
    const string SRC = "Assets/Models/Asset/PipesPack/prefab/PipeSet";
    const string OUT = "Assets/Resources/Thumbnails";
    const int    SIZE = 512;
    const float  MARGIN = 1.08f; // >1 leaves a small border so models never touch the frame edge

    [MenuItem("Tools/Theme C/Rebake Pipe Thumbnails (Transparent)")]
    public static void Rebake()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { SRC });
        if (guids.Length == 0) { Debug.LogError($"[ThemeC] No prefabs under {SRC}"); return; }
        if (!Directory.Exists(OUT)) Directory.CreateDirectory(OUT);

        var stage = new GameObject("__ThumbStage") { hideFlags = HideFlags.HideAndDontSave };
        // Render far off-scene so nothing else leaks into the frame.
        var origin = new Vector3(0f, 5000f, 0f);

        var camGO = new GameObject("__ThumbCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // fully transparent
        cam.orthographic = false;
        cam.enabled = false;
        cam.fieldOfView = 25f;
        cam.allowHDR = false;

        var lightGO = new GameObject("__ThumbLight") { hideFlags = HideFlags.HideAndDontSave };
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var rt = new RenderTexture(SIZE, SIZE, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };

        Vector3 camDir = (new Vector3(1f, 0.7f, 1f)).normalized;
        int done = 0;

        try
        {
            foreach (var guid in guids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) continue;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.hideFlags = HideFlags.HideAndDontSave;
                inst.transform.SetParent(stage.transform, false);
                inst.transform.SetPositionAndRotation(origin, Quaternion.identity);

                var rends = inst.GetComponentsInChildren<Renderer>(true);
                if (rends.Length == 0) { Object.DestroyImmediate(inst); continue; }

                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

                Vector3 center = b.center;
                // True bounding-sphere radius so the whole model fits regardless of aspect/angle.
                float radius = b.extents.magnitude * MARGIN;
                radius = Mathf.Max(radius, 0.001f);
                float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
                float dist = radius / Mathf.Sin(fovRad * 0.5f);

                cam.transform.position = center + camDir * dist;
                cam.transform.LookAt(center);
                cam.nearClipPlane = Mathf.Max(0.001f, dist - radius * 3f);
                cam.farClipPlane  = dist + radius * 6f;

                RenderToTexture(cam, rt);

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, SIZE, SIZE), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                File.WriteAllBytes(Path.Combine(OUT, prefab.name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(inst);
                done++;
            }
        }
        finally
        {
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(lightGO);
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(stage);
        }

        AssetDatabase.Refresh();
        Debug.Log($"[ThemeC] Re-baked {done} pipe thumbnails (URP, transparent bg) -> {OUT}");
    }

    // URP doesn't support the legacy Camera.Render(); use the render-request API (falls back to built-in).
    static void RenderToTexture(Camera cam, RenderTexture rt)
    {
        var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, req))
        {
            RenderPipeline.SubmitRenderRequest(cam, req);
        }
        else
        {
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
        }
    }
}
