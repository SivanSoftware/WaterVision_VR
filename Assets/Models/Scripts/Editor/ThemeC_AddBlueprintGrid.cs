using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds the Direction-C "blueprint" grid overlay to ModelSelectionPanel.prefab: a procedurally
/// generated, tileable teal line-grid drawn as a faint full-panel overlay Image (raycastTarget off
/// so it never blocks clicks). Idempotent — re-running regenerates the tile and updates the overlay.
/// Fully reversible: delete the "BlueprintGrid" child (or run Remove) to revert.
///
/// Run: Tools/Theme C/Add Blueprint Grid Overlay
/// </summary>
public static class ThemeC_AddBlueprintGrid
{
    const string PREFAB   = "Assets/Models/Asset/prefab/ModelSelectionPanel.prefab";
    const string UI_DIR   = "Assets/Models/Asset/UI/Theme_C";
    const string TILE_PNG = UI_DIR + "/blueprint_grid.png";

    const int   TILE = 48;   // grid spacing in sprite px
    const int   LINE = 2;    // line thickness in px
    static readonly Color LineCol = new Color(0.176f, 0.831f, 0.749f, 1f); // #2dd4bf
    static readonly Color OverlayTint = new Color(0.176f, 0.831f, 0.749f, 0.10f); // faint

    [MenuItem("Tools/Theme C/Add Blueprint Grid Overlay")]
    public static void AddGrid()
    {
        var sprite = BuildTileSprite();
        if (sprite == null) { Debug.LogError("[ThemeC] Failed to build grid sprite."); return; }

        var root = PrefabUtility.LoadPrefabContents(PREFAB);
        try
        {
            var existing = root.transform.Find("BlueprintGrid");
            GameObject go = existing != null ? existing.gameObject
                                             : new GameObject("BlueprintGrid", typeof(RectTransform), typeof(Image));
            if (existing == null) go.transform.SetParent(root.transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Tiled;
            img.color = OverlayTint;
            img.raycastTarget = false;
            img.maskable = true;

            go.transform.SetAsLastSibling(); // draw on top of everything = uniform overlay

            PrefabUtility.SaveAsPrefabAsset(root, PREFAB);
            Debug.Log("[ThemeC] Blueprint grid overlay added/updated on ModelSelectionPanel.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Theme C/Remove Blueprint Grid Overlay")]
    public static void RemoveGrid()
    {
        var root = PrefabUtility.LoadPrefabContents(PREFAB);
        try
        {
            var existing = root.transform.Find("BlueprintGrid");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, PREFAB);
                Debug.Log("[ThemeC] Blueprint grid overlay removed.");
            }
            else Debug.Log("[ThemeC] No BlueprintGrid overlay to remove.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Sprite BuildTileSprite()
    {
        if (!Directory.Exists(UI_DIR)) Directory.CreateDirectory(UI_DIR);

        var tex = new Texture2D(TILE, TILE, TextureFormat.RGBA32, false);
        var clear = new Color(0, 0, 0, 0);
        var px = new Color[TILE * TILE];
        for (int i = 0; i < px.Length; i++) px[i] = clear;
        // Lines on the top + left edges so adjacent tiles form a continuous grid.
        for (int x = 0; x < TILE; x++)
            for (int y = 0; y < TILE; y++)
                if (x < LINE || y < LINE) px[y * TILE + x] = LineCol;
        tex.SetPixels(px);
        tex.Apply();

        File.WriteAllBytes(TILE_PNG, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(TILE_PNG, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(TILE_PNG) is TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.spritePixelsPerUnit = 100f;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(TILE_PNG);
    }
}
