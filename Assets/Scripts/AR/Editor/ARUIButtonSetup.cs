#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI; // TrackedDeviceGraphicRaycaster
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort; // TunnelingVignetteController
using WaterVision.AR;

namespace WaterVision.AR.EditorTools
{
    /// <summary>
    /// Builds a polished, world-space UI "Enter AR Mode" button (a floating panel) in the model room of
    /// SampleScene, replacing the old physical poke kiosk. It uses the SAME clickability recipe the working
    /// model menu uses — a world-space Canvas + TrackedDeviceGraphicRaycaster + GraphicRaycaster routed through
    /// the scene's existing XR EventSystem — so it's clickable via finger-poke and ray regardless of the
    /// hand-ray bug. Button.onClick is wired to the existing ARModeController.ToggleARMode().
    ///
    /// Idempotent: removes any prior "AR Room Button" kiosk + prior "AR UI Button", then rebuilds. Anchors the
    /// panel ~0.8 m in front of Teleport_Target_Models at chest height, facing the player. Draggable to tune.
    /// </summary>
    public static class ARUIButtonSetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string AnchorName = "Teleport_Target_Models";
        const string OldKioskName = "AR Room Button";
        const string PanelName = "AR UI Button";
        const string TmpFontGuid = "8f586378b4e144a9851e7b34d9b748ee"; // LiberationSans SDF

        const float FrontOffset = 0.8f;   // metres in front of the teleport landing
        const float PanelHeight = 1.1f;   // metres above the room floor (lowered per user — chest height)
        const float CanvasScale = 0.0016f;
        static readonly Vector2 PanelSize = new Vector2(480f, 170f);

        // Accent palette (teal-cyan) — distinct from the plain blue-gray menu.
        static readonly Color Accent       = new Color(0.09f, 0.55f, 0.62f, 1f);
        static readonly Color AccentHover   = new Color(0.14f, 0.67f, 0.74f, 1f);
        static readonly Color AccentPressed = new Color(0.06f, 0.42f, 0.47f, 1f);
        static readonly Color BadgeColor    = new Color(0.05f, 0.30f, 0.35f, 1f);

        [MenuItem("WaterVision/AR/Setup AR UI Button")]
        public static void Setup()
        {
            if (!OpenTargetScene()) return;
            var scene = EditorSceneManager.GetActiveScene();

            var controller = EnsureController();
            ConfigureController(controller); // (re)assign hide-list + comfort vignette, even on an existing controller

            // Remove the old physical kiosk + any prior UI button (idempotent rebuild).
            var oldKiosk = GameObject.Find(OldKioskName);
            if (oldKiosk != null) { Object.DestroyImmediate(oldKiosk); Debug.Log("[ARUIButtonSetup] Removed old physical kiosk '" + OldKioskName + "'."); }
            var oldPanel = GameObject.Find(PanelName);
            if (oldPanel != null) Object.DestroyImmediate(oldPanel);

            BuildPanel(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ARUIButtonSetup] Done. Floating 'Enter AR Mode' UI button built in the model room + wired to ARModeController.ToggleARMode.");
        }

        static bool OpenTargetScene()
        {
            if (EditorSceneManager.GetActiveScene().path == ScenePath) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath);
            if (EditorSceneManager.GetActiveScene().path == ScenePath) return true;
            Debug.LogError("[ARUIButtonSetup] Could not open " + ScenePath + ".");
            return false;
        }

        // Find the existing ARModeController (already in the scene). If absent, create + point it at the model room.
        static ARModeController EnsureController()
        {
            var ctrl = Object.FindFirstObjectByType<ARModeController>(FindObjectsInactive.Include);
            if (ctrl != null) return ctrl;

            var go = new GameObject("AR Mode Controller");
            ctrl = go.AddComponent<ARModeController>();
            Debug.Log("[ARUIButtonSetup] Created ARModeController (none existed).");
            return ctrl;
        }

        // Always (re)configures the controller so re-running this menu fixes an EXISTING instance, not just a new one.
        // Hide-list = the model room's backdrop children (walls/doors/posters) + TrashBin — NEVER the whole Room
        // (that would hide the menus too). Also wires the comfort vignette.
        static void ConfigureController(ARModeController ctrl)
        {
            var anchor = GameObject.Find(AnchorName);
            var room = anchor != null ? anchor.transform.root.gameObject : GameObject.Find("Room");
            var so = new SerializedObject(ctrl);

            var list = so.FindProperty("m_EnvironmentRoots");
            list.arraySize = 0;
            if (room != null)
            {
                foreach (var n in new[] { "walls", "doors", "posters", "TrashBin" })
                {
                    var child = room.transform.Find(n);
                    if (child == null) { Debug.LogWarning("[ARUIButtonSetup] Room child '" + n + "' not found — skipped."); continue; }
                    int i = list.arraySize;
                    list.arraySize = i + 1;
                    list.GetArrayElementAtIndex(i).objectReferenceValue = child.gameObject;
                }
                so.FindProperty("m_EnvironmentRootName").stringValue = room.name;
            }
            else Debug.LogWarning("[ARUIButtonSetup] Model room not found — hide-list left empty (runtime name-fallback applies).");

            // Also hide OTHER rooms/environments that would render in AR passthrough (separate scene roots).
            foreach (var n in new[] { "Big Room", "WWTP_Tour", "Stage2_Process", "Stage3_Process", "Stage4_Process" })
            {
                var other = GameObject.Find(n);
                if (other == null) { Debug.LogWarning("[ARUIButtonSetup] Other room '" + n + "' not found — skipped."); continue; }
                int i = list.arraySize;
                list.arraySize = i + 1;
                list.GetArrayElementAtIndex(i).objectReferenceValue = other;
            }

            var vig = Object.FindFirstObjectByType<TunnelingVignetteController>(FindObjectsInactive.Include);
            var vigProp = so.FindProperty("m_ComfortVignette");
            if (vigProp != null && vig != null) vigProp.objectReferenceValue = vig;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ctrl);
            Debug.Log("[ARUIButtonSetup] Configured ARModeController: " + list.arraySize + " objects hidden in AR (backdrop + other rooms), vignette=" + (vig != null));
        }

        static void BuildPanel(ARModeController controller)
        {
            // ── World-space Canvas (the clickability recipe) ────────────────────────────────────────────────
            var canvasGo = new GameObject(PanelName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = PanelSize;
            canvasRt.localScale = Vector3.one * CanvasScale;

            PlaceAtModelRoom(canvasRt);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(TmpFontGuid));
            var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); // sliced rounded rect
            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");       // filled circle

            // ── Card (the Button) — fills the canvas ────────────────────────────────────────────────────────
            var card = NewUI("Card", canvasRt);
            Stretch(card.rectTransform);
            card.sprite = rounded;
            card.type = Image.Type.Sliced;
            card.color = Color.white; // tinted by the Button ColorBlock below
            var shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(4f, -4f);

            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            var colors = button.colors;
            colors.normalColor = Accent;
            colors.highlightedColor = AccentHover;
            colors.pressedColor = AccentPressed;
            colors.selectedColor = AccentHover;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            // ── Icon badge (circle + "AR") on the left ──────────────────────────────────────────────────────
            var badge = NewUI("IconBadge", card.rectTransform);
            badge.sprite = circle;
            badge.type = Image.Type.Simple;
            badge.color = BadgeColor;
            badge.raycastTarget = false; // decorative — let clicks pass to the Card/Button beneath
            var badgeRt = badge.rectTransform;
            badgeRt.anchorMin = new Vector2(0f, 0.5f);
            badgeRt.anchorMax = new Vector2(0f, 0.5f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.sizeDelta = new Vector2(120f, 120f);
            badgeRt.anchoredPosition = new Vector2(95f, 0f);
            var badgeText = NewText("AR", badgeRt, font, 64f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(badgeText.rectTransform);

            // ── Label ───────────────────────────────────────────────────────────────────────────────────────
            var label = NewText("Enter AR Mode", card.rectTransform, font, 52f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(180f, 10f);
            labelRt.offsetMax = new Vector2(-24f, -10f);

            // ── Wire onClick → ARModeController.ToggleARMode() (persistent, no runtime wirer needed) ──────────
            if (controller != null)
            {
                UnityEventTools.AddPersistentListener(button.onClick, controller.ToggleARMode);
                Debug.Log("[ARUIButtonSetup] Wired Button.onClick → ARModeController.ToggleARMode.");
            }
            else
            {
                Debug.LogWarning("[ARUIButtonSetup] No ARModeController — button built but not wired.");
            }

            EditorUtility.SetDirty(canvasGo);
        }

        static void PlaceAtModelRoom(RectTransform rt)
        {
            var anchor = GameObject.Find(AnchorName);
            if (anchor != null)
            {
                Vector3 fwd = anchor.transform.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
                fwd.Normalize();
                Vector3 pos = anchor.transform.position + fwd * FrontOffset;
                pos.y = anchor.transform.position.y + PanelHeight;
                rt.position = pos;
                rt.rotation = Quaternion.LookRotation(fwd, Vector3.up); // +Z away from player → readable -Z face toward player, non-mirrored (matches MenuController)
            }
            else
            {
                rt.position = new Vector3(-5f, 2.09f + PanelHeight, 63f);
                rt.rotation = Quaternion.Euler(0f, 90f, 0f);
                Debug.LogWarning("[ARUIButtonSetup] Anchor '" + AnchorName + "' not found — used a fallback position; drag the panel into place.");
            }
        }

        // ── helpers ───────────────────────────────────────────────────────────────────────────────────────
        static Image NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        static TextMeshProUGUI NewText(string text, Transform parent, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.raycastTarget = false; // labels never intercept clicks — they pass to the Button beneath
            return tmp;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
#endif
