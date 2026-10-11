#if UNITY_EDITOR
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using WaterVision.Recording;

namespace WaterVision.Recording.EditorTools
{
    /// <summary>
    /// One-click builder for the in-world Recording Dashboard. Reproducible: deletes the previous
    /// instance, rebuilds from scratch in the active scene, wires every Inspector reference into
    /// the runtime <c>RecordingDashboard</c> controller, and saves. Run again any time to reset.
    ///
    /// Follows the project's VR UI recipe (WaterVision_Custom_UI_System):
    ///  - World-space Canvas with BOTH GraphicRaycaster and TrackedDeviceGraphicRaycaster.
    ///  - 1000 px = 1 m (sizeDelta × 0.001 localScale).
    ///  - Dark "dashboard" palette (navy bg, blue frame, green Record, brick Stop, blue Refresh,
    ///    red close, red pulsing live dot).
    ///  - raycastTarget=false on every decorative graphic so buttons receive clicks.
    /// </summary>
    public static class RecordingDashboardBuilder
    {
        // ── Panel geometry (in metres) ───────────────────────────────────────────
        const float PANEL_W_M = 2.4f;
        const float PANEL_H_M = 1.6f;
        const float PIXEL_PER_METER = 1000f;

        // ── Palette (section 3.1 of the UI spec) ─────────────────────────────────
        static readonly Color BgNavy        = new Color(0.08f, 0.10f, 0.14f, 0.92f);
        static readonly Color FrameBlue     = new Color(0.23f, 0.44f, 0.83f, 1.00f);   // #3B6FD4
        static readonly Color TitleBlueWhite= new Color(0.85f, 0.95f, 1.00f, 1.00f);
        static readonly Color StatusReady   = Color.white;
        static readonly Color DotRed        = Color.red;

        static readonly Color RecordGreen   = new Color(0.18f, 0.66f, 0.29f, 1f); // #2EA84A
        static readonly Color RecordHover   = new Color(0.24f, 0.78f, 0.36f, 1f);
        static readonly Color RecordPressed = new Color(0.12f, 0.50f, 0.22f, 1f);

        static readonly Color StopBrick     = new Color(0.55f, 0.22f, 0.18f, 1f); // #8C3830
        static readonly Color StopHover     = new Color(0.65f, 0.28f, 0.22f, 1f);
        static readonly Color StopPressed   = new Color(0.40f, 0.14f, 0.12f, 1f);

        static readonly Color RefreshBlue   = new Color(0.20f, 0.60f, 0.90f, 1f); // #3399E6
        static readonly Color RefreshHover  = new Color(0.30f, 0.70f, 1.00f, 1f);
        static readonly Color RefreshPressed= new Color(0.10f, 0.45f, 0.75f, 1f);

        static readonly Color CloseRed      = new Color(0.75f, 0.22f, 0.17f, 1f); // #C0392B
        static readonly Color CloseHover    = new Color(0.88f, 0.30f, 0.25f, 1f);
        static readonly Color ClosePressed  = new Color(0.55f, 0.15f, 0.10f, 1f);

        static readonly Color FileRowBg     = new Color(0.13f, 0.16f, 0.22f, 1f);
        static readonly Color FileRowHover  = new Color(0.18f, 0.22f, 0.30f, 1f);
        static readonly Color FileRowPressed= new Color(0.10f, 0.13f, 0.18f, 1f);

        // ── Font sizes (canvas-pixel space; ÷ 1000 = metres) ─────────────────────
        const float FONT_TITLE   = 130;
        const float FONT_TIMER   = 220;
        const float FONT_STATUS  = 70;
        const float FONT_BUTTON  = 80;
        const float FONT_HEADER  = 80;
        const float FONT_FILE    = 60;
        const float FONT_CLOSE   = 90;

        [MenuItem("WaterVision/UI/Build Recording Dashboard")]
        public static void Build()
        {
            // First: make sure the runtime infrastructure exists in the scene. Without these,
            // the dashboard would compile and render but Record/Stop and replay would do
            // nothing (SpatialRecorder.Instance / ReplayManager.Instance would be null).
            EnsureRecordingInfrastructure();

            // Add (or refresh) a "Record" button on the RadialMenu prefab that opens the dashboard.
            // Idempotent: re-running replaces the existing instance instead of stacking duplicates.
            AddRecordingRadialButton();

            // Remove the previous build (idempotent).
            var prev = GameObject.Find("RecordingDashboard_Panel");
            if (prev != null) Object.DestroyImmediate(prev);

            // ── 1) World-space Canvas (VR-clickable) ─────────────────────────────
            var go = new GameObject("RecordingDashboard_Panel",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(TrackedDeviceGraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta  = new Vector2(PANEL_W_M * PIXEL_PER_METER, PANEL_H_M * PIXEL_PER_METER);
            rt.localScale = Vector3.one * (1f / PIXEL_PER_METER);

            // Park the panel at a neutral spot in the scene. We do NOT use Camera.main here:
            // the editor camera position is meaningless once the user enters play mode (the VR
            // rig camera is somewhere else). RecordingPanelLauncher.OpenRecordingPanel will
            // reposition this in front of the live player every time the user clicks the radial
            // Record button — that's the runtime source of truth.
            rt.position = new Vector3(0f, 1.5f, 1.5f);
            rt.rotation = Quaternion.identity;

            // ── 2) Background + Frame ────────────────────────────────────────────
            var bg = NewImage("BG", rt);
            Stretch(bg.rectTransform);
            bg.sprite = Builtin("UI/Skin/UISprite.psd");
            bg.type   = Image.Type.Sliced;
            bg.color  = BgNavy;
            bg.raycastTarget = false;

            var frame = NewImage("Frame", rt);
            Stretch(frame.rectTransform);
            frame.sprite = Builtin("UI/Skin/UISprite.psd");
            frame.type   = Image.Type.Sliced;
            frame.color  = new Color(FrameBlue.r, FrameBlue.g, FrameBlue.b, 0.0f);
            frame.raycastTarget = false;
            // outline via a child Outline graphic
            var frameOutline = frame.gameObject.AddComponent<Outline>();
            frameOutline.effectColor = FrameBlue;
            frameOutline.effectDistance = new Vector2(6, -6);

            // ── 3) Title ─────────────────────────────────────────────────────────
            var title = NewText("Recording System", rt, FONT_TITLE, FontStyles.Bold,
                                TextAlignmentOptions.Top);
            title.color = TitleBlueWhite;
            title.raycastTarget = false;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1); trt.sizeDelta = new Vector2(-200, 200);
            trt.anchoredPosition = new Vector2(0, -50);

            // ── 4) Close Button (top-right red X) ────────────────────────────────
            var closeImg = MakeButton(rt, "BtnClose", CloseRed, CloseHover, ClosePressed,
                                     out Button btnClose);
            var crt = closeImg.rectTransform;
            crt.anchorMin = new Vector2(1, 1); crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(1, 1); crt.sizeDelta = new Vector2(140, 140);
            crt.anchoredPosition = new Vector2(-40, -40);
            var closeLbl = NewText("X", closeImg.rectTransform, FONT_CLOSE, FontStyles.Bold,
                                   TextAlignmentOptions.Center);
            Stretch(closeLbl.rectTransform);
            closeLbl.raycastTarget = false;

            // ── 5) Layout: 2-column split (left = recorder, right = library) ─────
            // Both are anchored under the title with a margin from frame.
            const float MARGIN = 80f;
            const float TITLE_H = 220f;
            const float SPLIT_W = 800f;       // left column width

            var leftCol = new GameObject("LeftCol_Recorder", typeof(RectTransform));
            leftCol.transform.SetParent(rt, false);
            var lrt = (RectTransform)leftCol.transform;
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0, 1);
            lrt.pivot = new Vector2(0, 0.5f);
            lrt.sizeDelta = new Vector2(SPLIT_W, -(TITLE_H + MARGIN));
            lrt.anchoredPosition = new Vector2(MARGIN, -(TITLE_H + MARGIN) * 0.5f + 0); // visual centering
            lrt.offsetMin = new Vector2(MARGIN, MARGIN);
            lrt.offsetMax = new Vector2(MARGIN + SPLIT_W, -TITLE_H);

            var rightCol = new GameObject("RightCol_Library", typeof(RectTransform));
            rightCol.transform.SetParent(rt, false);
            var rrt = (RectTransform)rightCol.transform;
            rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(1, 1);
            rrt.pivot = new Vector2(0, 0.5f);
            rrt.offsetMin = new Vector2(MARGIN + SPLIT_W + MARGIN, MARGIN);
            rrt.offsetMax = new Vector2(-MARGIN, -TITLE_H);

            // ── 6) LEFT: Timer (top), Indicator+Status, Record/Stop ──────────────
            // Timer text
            var timer = NewText("00:00", lrt, FONT_TIMER, FontStyles.Bold, TextAlignmentOptions.Center);
            timer.color = Color.white; timer.raycastTarget = false;
            var tirt = timer.rectTransform;
            tirt.anchorMin = new Vector2(0, 1); tirt.anchorMax = new Vector2(1, 1);
            tirt.pivot = new Vector2(0.5f, 1); tirt.sizeDelta = new Vector2(0, 280);
            tirt.anchoredPosition = new Vector2(0, -20);

            // Recording indicator (red circle, pulses via runtime alpha)
            var indicator = NewImage("RecordingIndicator", lrt);
            indicator.sprite = Builtin("UI/Skin/Knob.psd");
            indicator.color  = new Color(1, 0, 0, 1);
            indicator.raycastTarget = false;
            var irt = indicator.rectTransform;
            irt.anchorMin = new Vector2(0.5f, 1); irt.anchorMax = new Vector2(0.5f, 1);
            irt.pivot = new Vector2(0.5f, 1); irt.sizeDelta = new Vector2(120, 120);
            irt.anchoredPosition = new Vector2(0, -320);

            // Status text
            var status = NewText("READY TO RECORD", lrt, FONT_STATUS, FontStyles.Bold,
                                 TextAlignmentOptions.Center);
            status.color = StatusReady; status.raycastTarget = false;
            var srt = status.rectTransform;
            srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
            srt.pivot = new Vector2(0.5f, 1); srt.sizeDelta = new Vector2(0, 100);
            srt.anchoredPosition = new Vector2(0, -460);

            // Record button (green)
            var recImg = MakeButton(lrt, "BtnRecord", RecordGreen, RecordHover, RecordPressed,
                                    out Button btnRecord);
            var rrt2 = recImg.rectTransform;
            rrt2.anchorMin = new Vector2(0, 0); rrt2.anchorMax = new Vector2(1, 0);
            rrt2.pivot = new Vector2(0.5f, 0); rrt2.sizeDelta = new Vector2(0, 180);
            rrt2.anchoredPosition = new Vector2(0, 240);
            var recLbl = NewText("RECORD", recImg.rectTransform, FONT_BUTTON, FontStyles.Bold,
                                 TextAlignmentOptions.Center);
            Stretch(recLbl.rectTransform);
            recLbl.raycastTarget = false;

            // Stop button (brick)
            var stopImg = MakeButton(lrt, "BtnStop", StopBrick, StopHover, StopPressed,
                                     out Button btnStop);
            var strt = stopImg.rectTransform;
            strt.anchorMin = new Vector2(0, 0); strt.anchorMax = new Vector2(1, 0);
            strt.pivot = new Vector2(0.5f, 0); strt.sizeDelta = new Vector2(0, 180);
            strt.anchoredPosition = new Vector2(0, 40);
            var stopLbl = NewText("STOP", stopImg.rectTransform, FONT_BUTTON, FontStyles.Bold,
                                  TextAlignmentOptions.Center);
            Stretch(stopLbl.rectTransform);
            stopLbl.raycastTarget = false;

            // ── 7) RIGHT: Header + Refresh + ScrollView ──────────────────────────
            var libHeader = NewText("SAVED RECORDINGS", rrt, FONT_HEADER, FontStyles.Bold,
                                    TextAlignmentOptions.TopLeft);
            libHeader.color = TitleBlueWhite; libHeader.raycastTarget = false;
            var lhrt = libHeader.rectTransform;
            lhrt.anchorMin = new Vector2(0, 1); lhrt.anchorMax = new Vector2(1, 1);
            lhrt.pivot = new Vector2(0, 1); lhrt.sizeDelta = new Vector2(0, 110);
            lhrt.anchoredPosition = new Vector2(0, 0);

            // Refresh button (top-right of right column)
            var refImg = MakeButton(rrt, "BtnRefresh", RefreshBlue, RefreshHover, RefreshPressed,
                                    out Button btnRefresh);
            var refrt = refImg.rectTransform;
            refrt.anchorMin = new Vector2(1, 1); refrt.anchorMax = new Vector2(1, 1);
            refrt.pivot = new Vector2(1, 1); refrt.sizeDelta = new Vector2(260, 110);
            refrt.anchoredPosition = new Vector2(0, 0);
            var refLbl = NewText("REFRESH", refImg.rectTransform, 60, FontStyles.Bold,
                                 TextAlignmentOptions.Center);
            Stretch(refLbl.rectTransform);
            refLbl.raycastTarget = false;

            // ScrollView (manual build — no editor utility)
            var scrollGo = new GameObject("LibraryScroll",
                typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(Mask));
            scrollGo.transform.SetParent(rrt, false);
            var srrt = (RectTransform)scrollGo.transform;
            srrt.anchorMin = new Vector2(0, 0); srrt.anchorMax = new Vector2(1, 1);
            srrt.offsetMin = new Vector2(0, 0); srrt.offsetMax = new Vector2(0, -150);
            var scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.sprite = Builtin("UI/Skin/UISprite.psd");
            scrollBg.type = Image.Type.Sliced;
            scrollBg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);
            var mask = scrollGo.GetComponent<Mask>();
            mask.showMaskGraphic = true;
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            // Viewport == the scroll root itself (we already have Mask + Image).
            scroll.viewport = srrt;

            // Content
            var contentGo = new GameObject("Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var crt2 = (RectTransform)contentGo.transform;
            crt2.anchorMin = new Vector2(0, 1); crt2.anchorMax = new Vector2(1, 1);
            crt2.pivot = new Vector2(0.5f, 1); crt2.anchoredPosition = Vector2.zero;
            crt2.sizeDelta = new Vector2(0, 0);
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.spacing = 12;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = crt2;

            // ── 8) FileButton template (kept OUTSIDE listContainer so RefreshLibrary won't
            //     destroy it on the first call). Parent is an inactive holder, so the template
            //     is invisible — but its own activeSelf stays true so Instantiate(prefab,
            //     listContainer) produces ACTIVE clones in the live list.
            var templatesHolder = new GameObject("_HiddenTemplates", typeof(RectTransform));
            templatesHolder.transform.SetParent(rt, false);
            templatesHolder.SetActive(false);

            var rowImg = MakeButton(templatesHolder.transform, "FileButtonTemplate",
                                    FileRowBg, FileRowHover, FileRowPressed, out Button rowBtn);
            var rowRT = rowImg.rectTransform;
            rowRT.sizeDelta = new Vector2(0, 120);
            var rowLbl = NewText("FileName", rowImg.rectTransform, FONT_FILE, FontStyles.Normal,
                                 TextAlignmentOptions.MidlineLeft);
            var lblRT = rowLbl.rectTransform;
            lblRT.anchorMin = new Vector2(0, 0); lblRT.anchorMax = new Vector2(1, 1);
            lblRT.offsetMin = new Vector2(30, 0); lblRT.offsetMax = new Vector2(-30, 0);
            rowLbl.raycastTarget = false;
            var rowLE = rowImg.gameObject.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 120; rowLE.minHeight = 120;

            // ── 9) Add the RecordingDashboard controller and wire references ─────
            var dash = go.AddComponent<RecordingDashboard>();
            dash.btnRecord = btnRecord;
            dash.btnStop = btnStop;
            dash.btnRefresh = btnRefresh;
            dash.txtTimer = timer;
            dash.txtStatus = status;
            dash.imgRecordingIndicator = indicator;
            dash.listContainer = crt2;
            dash.fileButtonPrefab = rowImg.gameObject;

            // ── 10) Close button hides the whole panel via the runtime helper ────
            var closer = go.AddComponent<HidePanelOnClick>();
            closer.target = go;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                btnClose.onClick, closer.Hide);

            // ── 11) Hide the panel by default — RecordingPanelLauncher.OpenRecordingPanel
            //     (called from the RadialMenu Record button) is the only path that should
            //     reveal it. Without this the panel would already be visible in the scene
            //     when the user enters play, masking the "click to open" UX.
            go.SetActive(false);

            // ── 12) Save and select ──────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[RecordingDashboardBuilder] Built 'RecordingDashboard_Panel' in scene " +
                      $"'{EditorSceneManager.GetActiveScene().name}' (starts hidden). " +
                      "Open it from the RadialMenu Record button in play mode.");
        }

        // ── menu wiring (RadialMenu) ─────────────────────────────────────────────

        const string RADIAL_MENU_PREFAB  = "Assets/UI/RadialMenu/Prefabs/RadialMenu.prefab";
        const string RADIAL_BUTTON_PREFAB = "Assets/UI/RadialMenu/Prefabs/RadialButton.prefab";
        const string RADIAL_BUTTON_NAME  = "RadialButton_Recording";

        /// <summary>
        /// Mirror of ASL_AddRadialButton, scoped to the recording feature. Loads the RadialMenu
        /// prefab, scrubs any dead "missing script" components (the prefab ships with a few that
        /// block saves), instantiates RadialButton.prefab as a child if not already present,
        /// attaches a <see cref="RecordingRadialButton"/> auto-wirer (which connects to
        /// <see cref="RecordingPanelLauncher.OpenRecordingPanel"/> at runtime), and saves.
        /// Idempotent: re-running finds the existing instance and refreshes its label/component.
        /// </summary>
        private static void AddRecordingRadialButton()
        {
            var btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RADIAL_BUTTON_PREFAB);
            if (btnPrefab == null)
            {
                Debug.LogWarning("[RecordingDashboardBuilder] RadialButton prefab missing at " +
                                 $"'{RADIAL_BUTTON_PREFAB}'. Skipping radial-menu wiring.");
                return;
            }

            // Repair the base button prefab first — its dead components propagate to every instance.
            RepairPrefab(RADIAL_BUTTON_PREFAB);

            var menuRoot = PrefabUtility.LoadPrefabContents(RADIAL_MENU_PREFAB);
            if (menuRoot == null)
            {
                Debug.LogWarning("[RecordingDashboardBuilder] RadialMenu prefab missing at " +
                                 $"'{RADIAL_MENU_PREFAB}'. Skipping radial-menu wiring.");
                return;
            }

            try
            {
                int removed = 0;
                foreach (var t in menuRoot.GetComponentsInChildren<Transform>(true))
                    removed += UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

                // Find a sensible parent for the button (some menus put buttons under a container
                // child). Match ASL: just parent under the menu root — RadialLayout will arrange it.
                var existing = menuRoot.transform.Find(RADIAL_BUTTON_NAME);
                GameObject inst = existing != null
                    ? existing.gameObject
                    : (GameObject)PrefabUtility.InstantiatePrefab(btnPrefab, menuRoot.transform);
                inst.name = RADIAL_BUTTON_NAME;

                if (inst.GetComponent<RecordingRadialButton>() == null)
                    inst.AddComponent<RecordingRadialButton>();

                // Also drop a RecordingPanelLauncher on the menu root so the runtime wirer has
                // something to find via GetComponentInParent — works even without a separate menu.
                if (menuRoot.GetComponent<RecordingPanelLauncher>() == null)
                    menuRoot.AddComponent<RecordingPanelLauncher>();

                // Update the visible label (RadialButton.prefab uses "Label " — trailing space —
                // for consistency with the ASL wire-up). Try both variants.
                var label = inst.transform.Find("Label ") ?? inst.transform.Find("Label");
                if (label != null)
                {
                    var tmp = label.GetComponent<TMPro.TextMeshProUGUI>();
                    if (tmp != null) tmp.text = "Record";
                }

                PrefabUtility.SaveAsPrefabAsset(menuRoot, RADIAL_MENU_PREFAB);
                Debug.Log($"[RecordingDashboardBuilder] RadialMenu: removed {removed} dead component(s), " +
                          $"added '{RADIAL_BUTTON_NAME}' with RecordingRadialButton + RecordingPanelLauncher. Saved.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(menuRoot);
            }
        }

        private static void RepairPrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return;
            try
            {
                int removed = 0;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    removed += UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                if (removed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[RecordingDashboardBuilder] Repaired '{path}': removed {removed} dead component(s).");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ── infrastructure (SpatialRecorder + ReplayManager) ─────────────────────

        /// <summary>
        /// Find an existing instance of <typeparamref name="T"/> in the scene that's "usable" —
        /// i.e. its whole transform chain is active, so Unity will actually run its Awake()
        /// at scene load. Returns null when the only matches are buried under inactive parents
        /// (e.g. inside a hidden menu prefab); callers should then create a fresh instance at
        /// scene root rather than trying to revive the buried one.
        /// </summary>
        private static T FindUsableSingleton<T>() where T : Component
        {
            var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in all)
                if (c != null && c.gameObject.activeInHierarchy) return c;
            return null;
        }

        /// <summary>
        /// Verify the runtime singletons the dashboard talks to actually exist in the scene.
        /// Create them with sensible defaults if missing. Idempotent: re-finding an existing
        /// instance is a no-op. The objects are NOT marked DontDestroyOnLoad — that's
        /// forbidden for SpatialRecorder (NetworkObject) and ReplayManager is scene-scoped.
        /// </summary>
        private static void EnsureRecordingInfrastructure()
        {
            // SpatialRecorder: NetworkBehaviour singleton. Needs NetworkObject so NGO can
            // replicate its recorderState NetworkVariable across clients. Offline play
            // still works (PublishRecorderState is a no-op when not network-spawned).
            // The recorder must end up with activeInHierarchy == true so Awake() runs and
            // SpatialRecorder.Instance gets assigned. A pre-existing recorder buried under an
            // inactive ancestor (e.g. inside the legacy 'Recording Feature V3' menu panel,
            // which is SetActive(false) by default) can't be rescued by toggling its own
            // activeSelf — its parent stays off. In that case we create a fresh standalone
            // recorder at scene root.
            var recorder = FindUsableSingleton<SpatialRecorder>();
            if (recorder == null)
            {
                var go = new GameObject("SpatialRecorder",
                    typeof(NetworkObject),
                    typeof(SpatialRecorder));
                Debug.Log("[RecordingDashboardBuilder] Created 'SpatialRecorder' at scene root " +
                          "(NetworkObject + SpatialRecorder). For full multiplayer support, " +
                          "register this prefab in the scene's NetworkManager prefab list. " +
                          "Offline editor testing works either way.", go);
            }
            else if (!recorder.enabled)
            {
                recorder.enabled = true;
                Debug.Log("[RecordingDashboardBuilder] Re-enabled existing 'SpatialRecorder' " +
                          "component so its Awake() runs.", recorder);
            }

            // Same rule for ReplayManager: needs activeInHierarchy so its Awake assigns Instance.
            var replay = FindUsableSingleton<ReplayManager>();
            if (replay == null)
            {
                var go = new GameObject("ReplayManager", typeof(ReplayManager));
                replay = go.GetComponent<ReplayManager>();
                Debug.Log("[RecordingDashboardBuilder] Created 'ReplayManager' at scene root.", go);
            }
            else if (!replay.enabled)
            {
                replay.enabled = true;
                Debug.Log("[RecordingDashboardBuilder] Re-enabled existing 'ReplayManager' component.", replay);
            }

            // Wire ghostPrefab if blank — auto-find the only Ghost.prefab in the project.
            if (replay.ghostPrefab == null)
            {
                var ghostGuids = AssetDatabase.FindAssets("t:Prefab Ghost");
                foreach (var guid in ghostGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.Contains("/New Recording/")) continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null && prefab.name == "Ghost")
                    {
                        replay.ghostPrefab = prefab;
                        Debug.Log($"[RecordingDashboardBuilder] Wired ReplayManager.ghostPrefab → '{path}'.", replay);
                        break;
                    }
                }
                if (replay.ghostPrefab == null)
                {
                    Debug.LogWarning("[RecordingDashboardBuilder] Could not auto-find Ghost.prefab; " +
                                     "please assign ReplayManager.ghostPrefab manually in the Inspector.", replay);
                }
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        static Image NewImage(string n, Transform parent)
        {
            var g = new GameObject(n, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(parent, false);
            return g.GetComponent<Image>();
        }

        static TextMeshProUGUI NewText(string t, Transform parent, float sz, FontStyles style,
                                       TextAlignmentOptions align)
        {
            var g = new GameObject("Text_" + t, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            var tmp = g.AddComponent<TextMeshProUGUI>();
            tmp.text = t;
            tmp.fontSize = sz;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = Color.white;
            return tmp;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        /// <summary>Create a 9-sliced rounded button with shadow + ColorBlock + VRButtonScale.</summary>
        static Image MakeButton(Transform parent, string name, Color normal, Color hover, Color pressed,
                                out Button btn)
        {
            var img = NewImage(name, parent);
            img.sprite = Builtin("UI/Skin/UISprite.psd");
            img.type   = Image.Type.Sliced;
            img.color  = Color.white;

            var shadow = img.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(3, -3);

            btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var c = btn.colors;
            c.normalColor = normal;
            c.highlightedColor = hover;
            c.pressedColor = pressed;
            c.selectedColor = hover;
            c.fadeDuration = 0.08f;
            btn.colors = c;

            // VRButtonScale (grows on pointer-enter); skip silently if the type isn't present.
            var vrType = System.Type.GetType("VRButtonScale, Assembly-CSharp")
                       ?? System.Type.GetType("VRButtonScale");
            if (vrType != null) img.gameObject.AddComponent(vrType);

            return img;
        }
    }

}
#endif
