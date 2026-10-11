#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using WaterVision.AR;

namespace WaterVision.AR.EditorTools
{
    /// <summary>
    /// Builds the floating AR utility toolbar (Save / Exit AR / Delete) — a world-space Canvas using the same
    /// clickability recipe + styling as ARUIButtonSetup, with the runtime ARUtilityToolbar (head-follow + show-in-AR)
    /// and its button refs wired. Idempotent (rebuilds on re-run). The toolbar appears only in AR (runtime-handled).
    /// </summary>
    public static class ARUtilityToolbarSetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string ToolbarName = "AR Utility Toolbar";
        const string TmpFontGuid = "8f586378b4e144a9851e7b34d9b748ee";

        const float CanvasScale = 0.0016f;
        static readonly Vector2 PanelSize = new Vector2(1240f, 180f); // 4 buttons: Save / Exit AR / Delete / Recenter
        static readonly Vector2 ButtonSize = new Vector2(280f, 150f);
        const float ButtonGap = 20f;

        static readonly Color Accent        = new Color(0.09f, 0.55f, 0.62f, 1f);
        static readonly Color AccentHover    = new Color(0.14f, 0.67f, 0.74f, 1f);
        static readonly Color AccentPressed  = new Color(0.06f, 0.42f, 0.47f, 1f);
        static readonly Color DeleteAccent   = new Color(0.66f, 0.22f, 0.20f, 1f); // red-ish for Delete
        static readonly Color DeleteHover     = new Color(0.78f, 0.30f, 0.27f, 1f);
        static readonly Color DeletePressed   = new Color(0.50f, 0.16f, 0.15f, 1f);

        [MenuItem("WaterVision/AR/Setup AR Utility Toolbar")]
        public static void Setup()
        {
            if (!OpenScene()) return;
            var scene = EditorSceneManager.GetActiveScene();

            var existing = GameObject.Find(ToolbarName);
            if (existing != null) Object.DestroyImmediate(existing);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(TmpFontGuid));
            var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var go = new GameObject(ToolbarName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                                    typeof(TrackedDeviceGraphicRaycaster), typeof(ARUtilityToolbar));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = PanelSize;
            rt.localScale = Vector3.one * CanvasScale;
            rt.position = new Vector3(0f, 1.4f, 0.8f); // initial; the runtime follow repositions it on AR enter

            float dx = ButtonSize.x + ButtonGap;
            var saveBtn     = BuildButton("Save",     "Save",     new Vector2(-1.5f * dx, 0f), rt, font, rounded, Accent, AccentHover, AccentPressed);
            var exitBtn     = BuildButton("ExitAR",   "Exit AR",  new Vector2(-0.5f * dx, 0f), rt, font, rounded, Accent, AccentHover, AccentPressed);
            var deleteBtn   = BuildButton("Delete",   "Delete",   new Vector2( 0.5f * dx, 0f), rt, font, rounded, DeleteAccent, DeleteHover, DeletePressed);
            var recenterBtn = BuildButton("Recenter", "Recenter", new Vector2( 1.5f * dx, 0f), rt, font, rounded, Accent, AccentHover, AccentPressed);

            var toolbar = go.GetComponent<ARUtilityToolbar>();
            var so = new SerializedObject(toolbar);
            var ctrl = Object.FindFirstObjectByType<ARModeController>(FindObjectsInactive.Include);
            if (ctrl != null) so.FindProperty("m_Controller").objectReferenceValue = ctrl;
            so.FindProperty("m_SaveButton").objectReferenceValue = saveBtn;
            so.FindProperty("m_ExitButton").objectReferenceValue = exitBtn;
            so.FindProperty("m_DeleteButton").objectReferenceValue = deleteBtn;
            so.FindProperty("m_RecenterButton").objectReferenceValue = recenterBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ARUtilityToolbarSetup] Built '" + ToolbarName + "' (Save/Exit AR/Delete/Recenter), wired to ARModeController + ARUtilityToolbar. " +
                      (ctrl == null ? "WARNING: no ARModeController found — assign it." : ""));
        }

        static bool OpenScene()
        {
            if (EditorSceneManager.GetActiveScene().path == ScenePath) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath);
            return EditorSceneManager.GetActiveScene().path == ScenePath;
        }

        static Button BuildButton(string name, string label, Vector2 anchoredPos, RectTransform parent,
                                  TMP_FontAsset font, Sprite rounded, Color normal, Color hover, Color pressed)
        {
            var cardGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            cardGo.transform.SetParent(parent, false);
            var rt = cardGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = ButtonSize;
            rt.anchoredPosition = anchoredPos;

            var img = cardGo.GetComponent<Image>();
            img.sprite = rounded; img.type = Image.Type.Sliced; img.color = Color.white;
            var shadow = cardGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f); shadow.effectDistance = new Vector2(4f, -4f);

            var btn = cardGo.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = normal; colors.highlightedColor = hover; colors.pressedColor = pressed;
            colors.selectedColor = hover; colors.fadeDuration = 0.1f;
            btn.colors = colors;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(rt, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            var trt = tmp.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            tmp.text = label;
            if (font != null) tmp.font = font;
            tmp.fontSize = 56f; tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white; tmp.raycastTarget = false;

            return btn;
        }
    }
}
#endif
