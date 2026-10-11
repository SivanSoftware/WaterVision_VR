using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Restyles the in-scene "Export Assembly to 3D" panel (ModelsRoom/ExportStation_Canvas) to
/// Direction B · Blueprint: deep-teal panel, left isometric-cube column + divider + caption, right
/// eyebrow/title/button/status stack, 4 corner brackets, a (hidden) progress bar, and a button icon.
/// Also wires the optional PipeAssemblyExporter visual-state refs so the live states work.
///
/// Idempotent (find-or-create by name). Operates on the OPEN scene and DOES NOT SAVE — review the
/// dirty scene yourself (SampleScene is merge-sensitive). Run sprites first: Tools/Theme B/Generate
/// Export Panel Sprites.
///
/// Run: Tools/Theme B/Build Export Panel (Blueprint)
/// </summary>
public static class ThemeB_BuildExportPanel
{
    const string UI = "Assets/Models/Asset/UI/Theme_C";
    const string FDIR = "Assets/Fonts";

    static readonly Color Panel    = new Color(0.022f, 0.12f, 0.145f, 1f);
    static readonly Color Accent   = new Color(0.176f, 0.831f, 0.749f, 1f);
    static readonly Color OnAccent = new Color(0.012f, 0.169f, 0.153f, 1f);
    static readonly Color TitleCol = new Color(0.843f, 0.984f, 0.957f, 1f);
    static readonly Color Muted    = new Color(0.373f, 0.682f, 0.624f, 1f);
    static readonly Color Divider  = new Color(0.176f, 0.831f, 0.749f, 0.5f);
    static readonly Color Bracket  = new Color(0.176f, 0.831f, 0.749f, 0.8f);

    [MenuItem("Tools/Theme B/Build Export Panel (Blueprint)")]
    public static void Build()
    {
        // Locate the panel via its exporter component (robust to nesting under ModelsRoom).
        GameObject canvasGO = null;
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent("PipeAssemblyExporter") != null) { canvasGO = t.gameObject; break; }
            if (canvasGO != null) break;
        }
        if (canvasGO == null) { Debug.LogError("[ThemeB] ExportStation_Canvas (PipeAssemblyExporter) not found in the open scene."); return; }

        var spaceGrotesk = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FDIR}/SpaceGrotesk SDF.asset");
        var plexSans     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FDIR}/IBMPlexSans SDF.asset");
        var plexMono     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FDIR}/IBMPlexMono SDF.asset");
        var spCube   = Spr("ep_isocube");
        var spBtn    = Spr("ep_btn_round");
        var spBracket= Spr("ep_corner_bracket");
        var spDown   = Spr("ep_ico_download");
        var spSpin   = Spr("ep_ico_spinner");
        var spCheck  = Spr("ep_ico_check");
        var spRetry  = Spr("ep_ico_retry");
        var spPanel  = Spr("ep_panel_round");
        var spBorder = Spr("ep_panel_border");
        var spGlow   = Spr("ep_glow");
        var spSheen  = Spr("ep_btn_sheen");

        Transform canvas = canvasGO.transform;

        // ---- Existing children ----
        var bg     = canvas.Find("Background");
        var title  = canvas.Find("Title_Text");
        var btn    = canvas.Find("Export_Button");
        var status = canvas.Find("Status_Text");
        Transform label = btn != null ? btn.Find("Export_Button label") : null;
        if (label == null && btn != null) { var l = btn.GetComponentInChildren<TextMeshProUGUI>(true); if (l) label = l.transform; }

        if (bg != null) { var im = bg.GetComponent<Image>(); if (im) { im.sprite = spPanel; im.type = Image.Type.Sliced; im.color = Panel; } }

        if (title != null)
        {
            var tmp = title.GetComponent<TextMeshProUGUI>();
            if (tmp) { if (spaceGrotesk) tmp.font = spaceGrotesk; tmp.fontSize = 40; tmp.color = TitleCol;
                       tmp.alignment = TextAlignmentOptions.TopLeft; tmp.text = "Export Assembly to 3D"; tmp.enableWordWrapping = true; }
            RT(title.gameObject, new Vector2(160, 70), new Vector2(430, 96));
        }

        if (btn != null)
        {
            var im = btn.GetComponent<Image>();
            if (im) { if (spBtn) { im.sprite = spBtn; im.type = Image.Type.Sliced; } im.color = Accent; }
            var b = btn.GetComponent<Button>();
            if (b) b.transition = Selectable.Transition.None; // script drives the colours
            RT(btn.gameObject, new Vector2(94, -36), new Vector2(300, 96));
        }

        if (label != null)
        {
            var tmp = label.GetComponent<TextMeshProUGUI>();
            if (tmp) { if (plexSans) tmp.font = plexSans; tmp.fontSize = 40; tmp.color = OnAccent;
                       tmp.alignment = TextAlignmentOptions.Center; tmp.text = "Export 3D"; }
            // Shift the centred label right so it clears the left-hand download icon (gap before text).
            var lrt = label.GetComponent<RectTransform>();
            lrt.offsetMin = new Vector2(64, lrt.offsetMin.y);
            lrt.offsetMax = new Vector2(0, lrt.offsetMax.y);
        }

        if (status != null)
        {
            var tmp = status.GetComponent<TextMeshProUGUI>();
            if (tmp) { if (plexMono) tmp.font = plexMono; tmp.fontSize = 24; tmp.color = Muted;
                       tmp.alignment = TextAlignmentOptions.TopLeft; tmp.text = "Press the button to export current pipes"; }
            RT(status.gameObject, new Vector2(160, -150), new Vector2(430, 80));
        }

        // ---- New children ----
        // Left visual column
        var divider = Img(canvas, "Visual_Divider", null, Divider, false);
        RT(divider, new Vector2(-88, 0), new Vector2(2, 360));

        var glow = Img(canvas, "Cube_Glow", spGlow, new Color(0.176f, 0.831f, 0.749f, 0.22f), false);
        RT(glow, new Vector2(-244, 12), new Vector2(300, 300));
        var cube = Img(canvas, "IsoCube", spCube, Accent, false);
        RT(cube, new Vector2(-244, 12), new Vector2(180, 180));
        glow.transform.SetSiblingIndex(cube.transform.GetSiblingIndex()); // keep glow behind the cube

        var caption = Txt(canvas, "Visual_Caption", plexMono, 18, Muted, TextAlignmentOptions.Center, "ASSEMBLY · 3D");
        caption.GetComponent<TextMeshProUGUI>().characterSpacing = 8;
        RT(caption, new Vector2(-244, -118), new Vector2(260, 30));

        // Right eyebrow
        var eyebrow = Txt(canvas, "Eyebrow_Text", plexMono, 20, Muted, TextAlignmentOptions.TopLeft, "WATERVISION · EXPORT");
        eyebrow.GetComponent<TextMeshProUGUI>().characterSpacing = 12;
        RT(eyebrow, new Vector2(160, 132), new Vector2(430, 32));

        // Thin rounded teal rim around the whole panel (stretched, on top, non-blocking).
        var border = Img(canvas, "Panel_Border", spBorder, new Color(0.176f, 0.831f, 0.749f, 0.33f), false);
        border.GetComponent<Image>().type = Image.Type.Sliced;
        var brt = border.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.pivot = new Vector2(0.5f, 0.5f);
        brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero; brt.localScale = Vector3.one;

        // Corner brackets (sprite L = top+left arms; rotate per corner)
        MakeBracket(canvas, "Corner_TL", spBracket, new Vector2(-368, 168), 0);
        MakeBracket(canvas, "Corner_TR", spBracket, new Vector2(368, 168), 270);
        MakeBracket(canvas, "Corner_BL", spBracket, new Vector2(-368, -168), 90);
        MakeBracket(canvas, "Corner_BR", spBracket, new Vector2(368, -168), 180);

        // Top progress bar (hidden until exporting)
        var prog = Img(canvas, "Progress_Bar", null, Accent, false);
        var prt = prog.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0, 1); prt.anchorMax = new Vector2(1, 1); prt.pivot = new Vector2(0.5f, 1);
        prt.sizeDelta = new Vector2(0, 6); prt.anchoredPosition = new Vector2(0, -3); prt.localScale = Vector3.one;
        var progImg = prog.GetComponent<Image>();
        progImg.type = Image.Type.Filled; progImg.fillMethod = Image.FillMethod.Horizontal; progImg.fillAmount = 0.3f;
        prog.SetActive(false);

        // Button icon (left of label)
        Image iconImg = null;
        if (btn != null)
        {
            var icon = Img(btn, "Button_Icon", spDown, OnAccent, false);
            RT(icon, new Vector2(-104, 0), new Vector2(40, 40));
            iconImg = icon.GetComponent<Image>();

            // (B) top sheen + (C) hover affordance
            var sheen = Img(btn, "Button_Sheen", spSheen, new Color(1, 1, 1, 0.22f), false);
            var srt = sheen.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1); srt.pivot = new Vector2(0.5f, 1);
            srt.sizeDelta = new Vector2(-16, 46); srt.anchoredPosition = new Vector2(0, -6); srt.localScale = Vector3.one;
            sheen.GetComponent<Image>().type = Image.Type.Simple;
            sheen.transform.SetAsFirstSibling(); // behind label + icon

            var hover = btn.GetComponent<ExportButtonHover>();
            if (hover == null) hover = btn.gameObject.AddComponent<ExportButtonHover>();
            hover.sheen = sheen.GetComponent<Image>();
            hover.scaleTarget = btn.GetComponent<RectTransform>();
            hover.button = btn.GetComponent<Selectable>();
        }

        // (D) gentle glow pulse on the cube halo
        var pulse = glow.GetComponent<GlowPulse>();
        if (pulse == null) pulse = glow.AddComponent<GlowPulse>();
        pulse.target = glow.GetComponent<Image>();

        // ---- Wire the optional PipeAssemblyExporter refs (via SerializedObject; no asmdef dependency) ----
        var exporter = canvasGO.GetComponent("PipeAssemblyExporter");
        if (exporter != null)
        {
            var so = new SerializedObject(exporter);
            SetRef(so, "buttonImage", btn != null ? btn.GetComponent<Image>() : null);
            SetRef(so, "buttonLabel", label != null ? label.GetComponent<TextMeshProUGUI>() : null);
            SetRef(so, "buttonIcon", iconImg);
            SetRef(so, "progressBar", progImg);
            SetRef(so, "icoDownload", spDown);
            SetRef(so, "icoSpinner", spSpin);
            SetRef(so, "icoCheck", spCheck);
            SetRef(so, "icoRetry", spRetry);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[ThemeB] Built Direction-B export panel. Scene left UNSAVED — review, then save manually.");
    }

    // ---------------------------------------------------------------- helpers
    static Sprite Spr(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UI}/{name}.png");

    static void SetRef(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
    }

    static GameObject Img(Transform parent, string name, Sprite sprite, Color col, bool raycast)
    {
        var t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name, typeof(Image));
        if (t == null) go.transform.SetParent(parent, false);
        var im = go.GetComponent<Image>();
        im.sprite = sprite; im.color = col; im.raycastTarget = raycast; im.type = Image.Type.Simple;
        return go;
    }

    static GameObject Txt(Transform parent, string name, TMP_FontAsset font, float size, Color col, TextAlignmentOptions align, string text)
    {
        var t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name, typeof(TextMeshProUGUI));
        if (t == null) go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font) tmp.font = font; tmp.fontSize = size; tmp.color = col; tmp.alignment = align; tmp.text = text;
        tmp.raycastTarget = false;
        return go;
    }

    static void MakeBracket(Transform parent, string name, Sprite sprite, Vector2 pos, float zRot)
    {
        var go = Img(parent, name, sprite, Bracket, false);
        RT(go, pos, new Vector2(28, 28));
        go.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, zRot);
    }

    static void RT(GameObject go, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos; rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }
}
