using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WaterVision;

/// <summary>
/// Bakes a clean FRONT-facing, upper-body thumbnail for each avatar by rendering its FBX with a
/// dedicated camera (full control over angle + distance, unlike AssetPreview which is fixed), then
/// assigns the resulting Sprites to the AvatarCatalog. Fully synchronous.
///
/// Tweak the framing constants below for closer/further. Run: Tools ▸ WaterVision ▸ Bake Avatar Thumbnails.
/// </summary>
public static class AvatarThumbnailBaker
{
    const string k_CatalogPath = "Assets/Resources/AvatarCatalog.asset";
    const string k_OutDir = "Assets/Resources/AvatarThumbnails";

    // One FBX per catalog index (must match AvatarCatalog order: 0 = base Ch08, then variants).
    static readonly string[] k_Fbx =
    {
        "Assets/Mixamo/Character/Ch08_nonPBR.fbx",
        "Assets/Mixamo/Avatars/Ch07_nonPBR.fbx",
        "Assets/Mixamo/Avatars/Ch22_nonPBR.fbx",
        "Assets/Mixamo/Avatars/Ch23_nonPBR.fbx",
        "Assets/Mixamo/Avatars/Ch33_nonPBR.fbx",
        "Assets/Mixamo/Avatars/Ch37_nonPBR.fbx",
    };

    // --- Framing (front view, upper body). Smaller distance = closer. ---
    const int k_Size = 256;
    const float k_Fov = 24f;
    const float k_TargetHeight = 1.45f;  // look-at height (neck/chest) for a head-and-torso portrait
    const float k_Distance = 1.7f;       // camera distance from the avatar (closer = smaller)
    static readonly Vector3 k_Origin = new Vector3(0f, 1000f, 0f); // off-scene so only the avatar renders

    [MenuItem("Tools/WaterVision/Bake Avatar Thumbnails")]
    static void Bake()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(k_CatalogPath);
        if (catalog == null) { Debug.LogError("[AvatarThumbnailBaker] No AvatarCatalog at " + k_CatalogPath); return; }
        if (!Directory.Exists(k_OutDir)) Directory.CreateDirectory(k_OutDir);

        var rt = new RenderTexture(k_Size, k_Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var camGo = new GameObject("~ThumbCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.fieldOfView = k_Fov;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 20f;

        var lightGo = new GameObject("~ThumbLight");
        var li = lightGo.AddComponent<Light>();
        li.type = LightType.Directional;
        li.intensity = 1.3f;
        lightGo.transform.rotation = Quaternion.Euler(15f, 160f, 0f);

        int baked = 0;
        for (int i = 0; i < k_Fbx.Length; i++)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(k_Fbx[i]);
            if (src == null) { Debug.LogWarning("[AvatarThumbnailBaker] FBX missing: " + k_Fbx[i]); continue; }

            var inst = (GameObject)Object.Instantiate(src);
            inst.transform.SetPositionAndRotation(k_Origin, Quaternion.identity); // Mixamo faces +Z

            Vector3 target = k_Origin + new Vector3(0f, k_TargetHeight, 0f);
            cam.transform.position = target + new Vector3(0f, 0f, k_Distance); // +Z side = the avatar's front
            cam.transform.LookAt(target);

            RenderToTexture(cam, rt);

            RenderTexture.active = rt;
            var tex = new Texture2D(k_Size, k_Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, k_Size, k_Size), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            File.WriteAllBytes($"{k_OutDir}/AvatarThumb_{i}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(inst);
            baked++;
        }

        cam.targetTexture = null;
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(lightGo);
        rt.Release();
        Object.DestroyImmediate(rt);

        AssetDatabase.Refresh();
        int assigned = LinkInternal(catalog);
        Debug.Log($"[AvatarThumbnailBaker] Baked {baked} front-view thumbnails, {assigned} assigned to the catalog.");
    }

    // URP doesn't support Camera.Render(); render via the render-request API (falls back to built-in).
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

    /// <summary>Synchronous re-link: imports the existing PNGs as single Sprites and assigns to the catalog.</summary>
    [MenuItem("Tools/WaterVision/Link Avatar Thumbnails (no re-bake)")]
    static void Link()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(k_CatalogPath);
        if (catalog == null) { Debug.LogError("[AvatarThumbnailBaker] No AvatarCatalog at " + k_CatalogPath); return; }
        int assigned = LinkInternal(catalog);
        Debug.Log($"[AvatarThumbnailBaker] Linked {assigned}/{k_Fbx.Length} thumbnails to the catalog.");
    }

    static int LinkInternal(AvatarCatalog catalog)
    {
        for (int i = 0; i < k_Fbx.Length; i++)
        {
            string path = $"{k_OutDir}/AvatarThumb_{i}.png";
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool dirty = false;
                if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
                if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
        }
        AssetDatabase.Refresh();

        int assigned = 0;
        for (int i = 0; i < k_Fbx.Length; i++)
        {
            string path = $"{k_OutDir}/AvatarThumb_{i}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is Sprite sp) { sprite = sp; break; }
            if (sprite != null && catalog.entries != null && i < catalog.entries.Length)
            {
                catalog.entries[i].thumbnail = sprite;
                assigned++;
            }
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return assigned;
    }
}
