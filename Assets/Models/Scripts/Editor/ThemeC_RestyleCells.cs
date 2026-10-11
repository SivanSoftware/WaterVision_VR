using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot tool: batch-restyles every model cell in ModelSelectionPanel.prefab to the
/// Theme-C (Blueprint/Water) look — dark-teal cell background, dark thumbnail backing, and
/// the IBM Plex Sans label font/colour. Operates on the prefab contents in one open/save so
/// all ~50 cells update together. Idempotent (re-runnable).
///
/// Run: Tools/Theme C/Restyle Model Cells
/// </summary>
public static class ThemeC_RestyleCells
{
    const string PREFAB = "Assets/Models/Asset/prefab/ModelSelectionPanel.prefab";
    const string FONT   = "Assets/Fonts/IBMPlexSans SDF.asset";

    static readonly Color CellBg   = new Color(0.043f, 0.204f, 0.231f, 1f);   // #0b343b — cell frame/bg
    static readonly Color ThumbBg  = new Color(0.039f, 0.157f, 0.180f, 1f);   // darker teal behind the model preview
    static readonly Color LabelCol = new Color(0.761f, 0.953f, 0.918f, 1f);   // #c2f3ea — label text

    [MenuItem("Tools/Theme C/Restyle Model Cells")]
    public static void Restyle()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT);
        var root = PrefabUtility.LoadPrefabContents(PREFAB);
        try
        {
            var handlers = root.GetComponentsInChildren<ModelButtonHandler>(true);
            int n = 0;
            foreach (var h in handlers)
            {
                var cell = h.transform;

                // The cell's own Image is its frame/background.
                var bg = cell.GetComponent<Image>();
                if (bg != null) bg.color = CellBg;

                // IconMask Image = the backing behind the (transparent) thumbnail preview.
                var iconMask = cell.Find("IconMask");
                if (iconMask != null)
                {
                    var im = iconMask.GetComponent<Image>();
                    if (im != null) im.color = ThumbBg;
                }

                // Label TMP → IBM Plex Sans + teal text.
                var label = cell.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    if (font != null) label.font = font;
                    label.color = LabelCol;
                }
                n++;
            }
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB);
            Debug.Log($"[ThemeC] Restyled {n} model cells (dark-teal bg + IBM Plex Sans labels).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
