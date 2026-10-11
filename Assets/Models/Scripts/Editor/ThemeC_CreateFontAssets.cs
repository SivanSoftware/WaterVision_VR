using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// One-shot tool: builds three TextMeshPro SDF font assets from the TTFs in Assets/Fonts/
/// for the "Choose a Model" Theme-C reskin (Space Grotesk titles, IBM Plex Sans body,
/// IBM Plex Mono meta). 2048 atlas, SDFAA, ASCII baked, LiberationSans fallback.
///
/// Run: Tools/Theme C/Create TMP Font Assets   (idempotent — overwrites existing assets).
/// </summary>
public static class ThemeC_CreateFontAssets
{
    const string FALLBACK = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Tools/Theme C/Create TMP Font Assets")]
    public static void Create()
    {
        Make("Assets/Fonts/SpaceGrotesk.ttf",        "SpaceGrotesk SDF",  2048);
        Make("Assets/Fonts/IBMPlexSans.ttf",         "IBMPlexSans SDF",   2048);
        Make("Assets/Fonts/IBMPlexMono-Medium.ttf",  "IBMPlexMono SDF",   1024);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ThemeC] Created TMP font assets in Assets/Fonts/.");
    }

    static void Make(string ttfPath, string assetName, int atlas)
    {
        var src = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (src == null) { Debug.LogError($"[ThemeC] Source TTF not found: {ttfPath}"); return; }

        string outPath = $"Assets/Fonts/{assetName}.asset";
        // Remove any previous version so we start clean (idempotent re-run).
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath) != null)
            AssetDatabase.DeleteAsset(outPath);

        // Create an SDF font asset with a generous atlas; dynamic so we can bake the glyphs we need.
        var fa = TMP_FontAsset.CreateFontAsset(src, 90, 9, GlyphRenderMode.SDFAA, atlas, atlas,
                                               AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
        if (fa == null) { Debug.LogError($"[ThemeC] CreateFontAsset failed for {ttfPath}"); return; }
        fa.name = assetName;

        // Bake printable ASCII (32..126) + middle dot used in the design's meta text.
        var sb = new System.Text.StringBuilder();
        for (int c = 32; c <= 126; c++) sb.Append((char)c);
        sb.Append('·'); // ·
        fa.TryAddCharacters(sb.ToString());

        // Fallback to LiberationSans for any glyph not in the baked set.
        var fb = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FALLBACK);
        if (fb != null)
        {
            fa.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (!fa.fallbackFontAssetTable.Contains(fb)) fa.fallbackFontAssetTable.Add(fb);
        }

        // Persist the font asset + its atlas texture + material as one asset file.
        AssetDatabase.CreateAsset(fa, outPath);
        if (fa.atlasTexture != null)
        {
            fa.atlasTexture.name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
        }
        if (fa.material != null)
        {
            fa.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
        }
        EditorUtility.SetDirty(fa);
        Debug.Log($"[ThemeC] {assetName}: baked ASCII, fallback wired, saved → {outPath}");
    }
}
