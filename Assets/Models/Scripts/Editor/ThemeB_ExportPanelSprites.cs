using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the procedural sprite assets for the "Export Assembly to 3D" panel reskin
/// (Direction B · Blueprint). Pure asset generation — never touches the scene. All PNGs are written
/// to Assets/Models/Asset/UI/Theme_C/ and imported as Sprites (Bilinear, PPU 100, alphaIsTransparency).
///
/// Line-art (iso cube + icons) is rendered WHITE so the UI Image.color can tint it per state;
/// the iso-cube file is rendered teal directly (it's only ever shown in accent). Anti-aliased via a
/// distance-to-segment smoothstep (same idea as WWTPTourBuild.EnsureSoftParticleTexture).
///
/// Run: Tools/Theme B/Generate Export Panel Sprites   (idempotent — overwrites).
/// </summary>
public static class ThemeB_ExportPanelSprites
{
    const string DIR = "Assets/Models/Asset/UI/Theme_C";
    static readonly Color Teal  = new Color(0.176f, 0.831f, 0.749f, 1f);
    static readonly Color White = Color.white;

    [MenuItem("Tools/Theme B/Generate Export Panel Sprites")]
    public static void Generate()
    {
        if (!Directory.Exists(DIR)) Directory.CreateDirectory(DIR);
        BuildPanelFill();
        BuildPanelBorder();
        BuildGlow();
        BuildSheen();
        BuildIsoCube();
        BuildBracket();
        BuildButton();
        BuildDownload();
        BuildCheck();
        BuildRetry();
        BuildSpinner();
        AssetDatabase.Refresh();
        Debug.Log("[ThemeB] Generated export-panel sprites → " + DIR);
    }

    // ============================================================ sprite builders

    static void BuildIsoCube()
    {
        const int S = 256; var cv = new Canvas(S, S);
        System.Func<float, float, Vector2> M = (x, y) => Map(x, y, 100f, S, 20f);
        float w = 5f;
        // outer hexagon (closed)
        Vector2[] hex = { M(50,12), M(84,31), M(84,69), M(50,88), M(16,69), M(16,31) };
        Polyline(cv, hex, w, Teal, true);
        // inner spokes from the top + center
        Seg(cv, M(50,12), M(50,50), w, Teal);
        Seg(cv, M(50,50), M(84,31), w, Teal);
        Seg(cv, M(50,50), M(16,31), w, Teal);
        // vertical front edge, half opacity
        Seg(cv, M(50,50), M(50,88), w, new Color(Teal.r, Teal.g, Teal.b, 0.5f));
        Save(cv, DIR + "/ep_isocube.png", TextureWrapMode.Clamp);
    }

    static void BuildBracket()
    {
        const int S = 40; var cv = new Canvas(S, S);
        int thick = 5;
        // L occupying the TOP + LEFT edges (texture y=0 is bottom, so "top" = high y).
        for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
                if (y >= S - thick || x < thick) cv.Set(x, y, Teal);
        Save(cv, DIR + "/ep_corner_bracket.png", TextureWrapMode.Clamp);
    }

    static void BuildButton()
    {
        const int S = 64; var cv = new Canvas(S, S);
        float r = 16f, aa = 1.2f;
        Vector2 c = new Vector2(S * 0.5f, S * 0.5f);
        Vector2 half = new Vector2(S * 0.5f, S * 0.5f);
        for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - c;
                Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (half - new Vector2(r, r));
                float outside = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
                float cov = 1f - SS(-aa, aa, outside);
                if (cov > 0) cv.Blend(x, y, White, cov);
            }
        // 9-slice border just larger than the corner radius
        Save(cv, DIR + "/ep_btn_round.png", TextureWrapMode.Clamp, new Vector4(18, 18, 18, 18));
    }

    static void BuildPanelFill()
    {
        const int S = 96; float r = 26f, aa = 1.2f; var cv = new Canvas(S, S);
        Vector2 half = new Vector2(S * 0.5f, S * 0.5f);
        for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            {
                float d = RRSDF(new Vector2(x + 0.5f, y + 0.5f) - half, half, r);
                float cov = 1f - SS(-aa, aa, d);
                if (cov > 0) cv.Blend(x, y, White, cov);
            }
        Save(cv, DIR + "/ep_panel_round.png", TextureWrapMode.Clamp, new Vector4(30, 30, 30, 30));
    }

    static void BuildPanelBorder()
    {
        const int S = 96; float r = 26f, stroke = 3f, aa = 1.1f; var cv = new Canvas(S, S);
        Vector2 half = new Vector2(S * 0.5f, S * 0.5f);
        for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            {
                float d = RRSDF(new Vector2(x + 0.5f, y + 0.5f) - half, half, r);
                float cov = 1f - SS(stroke * 0.5f - aa, stroke * 0.5f + aa, Mathf.Abs(d));
                if (cov > 0) cv.Blend(x, y, White, cov);
            }
        Save(cv, DIR + "/ep_panel_border.png", TextureWrapMode.Clamp, new Vector4(30, 30, 30, 30));
    }

    // Soft radial glow (white, fades to transparent) — placed behind the iso cube for a holographic halo.
    static void BuildGlow()
    {
        const int S = 128; var cv = new Canvas(S, S);
        Vector2 c = new Vector2(S * 0.5f, S * 0.5f); float rad = S * 0.5f;
        for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            {
                float dist = (new Vector2(x + 0.5f, y + 0.5f) - c).magnitude / rad; // 0..~1
                float a = Mathf.Clamp01(1f - dist); a = a * a; // smooth falloff
                if (a > 0.001f) cv.Blend(x, y, new Color(1, 1, 1, a), 1f);
            }
        Save(cv, DIR + "/ep_glow.png", TextureWrapMode.Clamp);
    }

    // Vertical white gradient — brightest at the top, fading out by mid-height. The button's top sheen.
    static void BuildSheen()
    {
        const int W = 32, H = 64; var cv = new Canvas(W, H);
        for (int y = 0; y < H; y++)
        {
            float t = y / (float)(H - 1);                 // 0 bottom .. 1 top
            float a = Mathf.Clamp01((t - 0.4f) / 0.6f); a *= a;
            for (int x = 0; x < W; x++) cv.Set(x, y, new Color(1, 1, 1, a));
        }
        Save(cv, DIR + "/ep_btn_sheen.png", TextureWrapMode.Clamp);
    }

    // Signed distance to a rounded rectangle centred at origin (negative inside).
    static float RRSDF(Vector2 p, Vector2 half, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (half - new Vector2(r, r));
        return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
    }

    static void BuildDownload()
    {
        const int S = 64; var cv = new Canvas(S, S);
        System.Func<float, float, Vector2> M = (x, y) => Map(x, y, 24f, S, 8f);
        float w = 5.5f;
        Seg(cv, M(12,3),  M(12,15), w, White);                 // shaft
        Polyline(cv, new[] { M(7,11), M(12,16), M(17,11) }, w, White, false); // chevron
        Seg(cv, M(4,20),  M(20,20), w, White);                 // base tray
        Save(cv, DIR + "/ep_ico_download.png", TextureWrapMode.Clamp);
    }

    static void BuildCheck()
    {
        const int S = 64; var cv = new Canvas(S, S);
        System.Func<float, float, Vector2> M = (x, y) => Map(x, y, 24f, S, 10f);
        Polyline(cv, new[] { M(4,13), M(9,18), M(20,6) }, 6f, White, false);
        Save(cv, DIR + "/ep_ico_check.png", TextureWrapMode.Clamp);
    }

    static void BuildRetry()
    {
        const int S = 64; var cv = new Canvas(S, S);
        System.Func<float, float, Vector2> M = (x, y) => Map(x, y, 24f, S, 9f);
        Vector2 c = M(12, 12);
        float rad = (M(20, 12) - M(12, 12)).magnitude; // radius ~8 vb units in px
        // open ring: arc with a gap near the top-right
        Arc(cv, c, rad, 60f, 330f, 5.5f, White);
        // arrowhead at the 60° end, pointing roughly tangential
        Vector2 end = c + new Vector2(Mathf.Cos(60f * Mathf.Deg2Rad), Mathf.Sin(60f * Mathf.Deg2Rad)) * rad;
        Seg(cv, end, end + new Vector2(-1f, 8f), 5.5f, White);
        Seg(cv, end, end + new Vector2(9f, 3f),  5.5f, White);
        Save(cv, DIR + "/ep_ico_retry.png", TextureWrapMode.Clamp);
    }

    static void BuildSpinner()
    {
        const int S = 64; var cv = new Canvas(S, S);
        System.Func<float, float, Vector2> M = (x, y) => Map(x, y, 24f, S, 8f);
        Vector2 c = M(12, 12);
        float rad = (M(21, 12) - M(12, 12)).magnitude;
        Arc(cv, c, rad, 0f, 360f, 4.5f, new Color(1, 1, 1, 0.25f)); // faint full ring
        Arc(cv, c, rad, 90f, 180f, 4.5f, White);                    // bright quarter
        Save(cv, DIR + "/ep_ico_spinner.png", TextureWrapMode.Clamp);
    }

    // ============================================================ raster primitives

    class Canvas
    {
        public int W, H; public Color[] px;
        public Canvas(int w, int h) { W = w; H = h; px = new Color[w * h]; for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0); }
        public void Set(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) px[y * W + x] = c; }
        public void Blend(int x, int y, Color c, float cov)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || cov <= 0) return;
            int i = y * W + x; float a = Mathf.Clamp01(cov * c.a);
            if (a > px[i].a) px[i] = new Color(c.r, c.g, c.b, a); // same-colour line art → keep strongest
        }
    }

    // SVG (y-down, 0..vb) → texture pixel (y-up), with uniform padding.
    static Vector2 Map(float x, float y, float vb, int size, float pad)
    {
        float s = (size - 2f * pad) / vb;
        return new Vector2(pad + x * s, (size - 1) - (pad + y * s));
    }

    static void Polyline(Canvas cv, Vector2[] pts, float w, Color col, bool closed)
    {
        for (int i = 0; i < pts.Length - 1; i++) Seg(cv, pts[i], pts[i + 1], w, col);
        if (closed && pts.Length > 2) Seg(cv, pts[pts.Length - 1], pts[0], w, col);
    }

    static void Arc(Canvas cv, Vector2 c, float r, float startDeg, float endDeg, float w, Color col)
    {
        int steps = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(endDeg - startDeg) / 4f));
        Vector2 prev = Vector2.zero; bool has = false;
        for (int i = 0; i <= steps; i++)
        {
            float a = Mathf.Lerp(startDeg, endDeg, i / (float)steps) * Mathf.Deg2Rad;
            Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            if (has) Seg(cv, prev, p, w, col);
            prev = p; has = true;
        }
    }

    static void Seg(Canvas cv, Vector2 a, Vector2 b, float w, Color col)
    {
        float half = w * 0.5f, aa = 1.1f;
        int minx = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - half - aa - 1);
        int maxx = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + half + aa + 1);
        int miny = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - half - aa - 1);
        int maxy = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + half + aa + 1);
        for (int y = miny; y <= maxy; y++)
            for (int x = minx; x <= maxx; x++)
            {
                float d = DistToSeg(new Vector2(x + 0.5f, y + 0.5f), a, b);
                float cov = 1f - SS(half - aa, half + aa, d);
                if (cov > 0) cv.Blend(x, y, col, cov);
            }
    }

    // GLSL-style smoothstep (Unity's Mathf.SmoothStep interpolates from→to instead, so we need our own).
    static float SS(float e0, float e1, float x)
    {
        if (e1 == e0) return x < e0 ? 0f : 1f;
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float len2 = ab.sqrMagnitude;
        float t = len2 <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return (p - (a + ab * t)).magnitude;
    }

    static void Save(Canvas cv, string path, TextureWrapMode wrap, Vector4? border = null)
    {
        var tex = new Texture2D(cv.W, cv.H, TextureFormat.RGBA32, false);
        tex.SetPixels(cv.px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.wrapMode = wrap;
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.spritePixelsPerUnit = 100f;
            if (border.HasValue) ti.spriteBorder = border.Value;
            ti.SaveAndReimport();
        }
    }
}
