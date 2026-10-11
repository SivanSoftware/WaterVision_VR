using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds the WWTP Tour: places the cutscenes package, creates an Entrance station + 6 Stage stations
/// with PLAY buttons that call CutsceneManager.PlayCutscene(int) / PlayAllCutscenes(), and a single
/// world-space ExplanationPlate that all 6 CameraMoves write their per-waypoint text to.
///
/// All work is in the active scene. The script is idempotent -every menu can be re-run safely.
///
/// Menus (under MCP/WWTP Tour/):
///   • Delete Old Simulation Area       -destroys the template's leftover `Simulation Area` GameObject
///   • Build Full WWTP Tour             -instantiates the cutscene prefab at TOUR_WORLD_POS, builds
///                                        Entrance + 6 stage stations, the explanation plate, and
///                                        wires every PLAY button + uiTextBox
///   • Repoint Teleport Target          -moves Teleport_Target_Sim to a sensible landing in front of the entrance
///
/// To reposition the whole installation: drag WWTP_Cutscenes in the scene, then update TOUR_WORLD_POS
/// below to the new position so future rebuilds land at the same spot.
/// </summary>
public static class WWTPTourBuild
{
    // ===================== Tunable constants =====================

    // World position of the WWTP installation. Both WWTP_Cutscenes and WWTP_Tour anchor here.
    // To reposition: drag WWTP_Cutscenes in the scene, then update this constant. Entrance/stage
    // signs and the teleport target derive their world placement from the cutscene prefab's
    // ACTUAL renderer bounds (computed at build time), not from this anchor -so even if the
    // model is rescaled or repositioned, the signs follow.
    static readonly Vector3 TOUR_WORLD_POS = new(-89.4f, 2.09f, -56.0f);

    // Uniform scale on the cutscene prefab. The package was authored at ~20m footprint; 0.3
    // brings the model + the cameras + the waypoints down together so the cutscenes still play
    // correctly through proportionally smaller geometry.
    const float CUTSCENE_PACKAGE_SCALE = 0.3f;

    // Height (in world meters) above the tour floor at which stage signs hover, regardless of
    // each SIM camera's startPoint y. (The cutscene package author intentionally parks startPoints
    // at y≈-100 to hide them -we override to eye level so the signs are reachable.)
    const float STAGE_SIGN_EYE_HEIGHT = 1.6f;

    // Distance OUT from the bounding-box face for the entrance station and teleport landing.
    // Entrance sits 1m past the plant's +Z face so the player sees it first on teleport.
    // Teleport lands a further 1.5m past the entrance.
    const float ENTRANCE_FRONT_OFFSET = 1.0f;
    const float TELEPORT_FRONT_OFFSET = 2.5f;

    // Stage signs are arranged in a row along the +Z face of the plant model (the face the
    // player sees on teleport). They sit STAGE_FRONT_OFFSET past the bounds.max.z edge and span
    // STAGE_ROW_X_FRACTION of the plant width -so as the player walks along the front of the
    // digital twin they pass each stage sign next to the appropriate region of the model.
    // The SIM camera `startPoint` is NOT used for placement (the cutscene package author placed
    // startPoints in a much larger virtual world -they're 10× the plant's scale, so they fall
    // far outside the visible model). The row layout keeps signs adjacent to the equipment the
    // player can actually see.
    const float STAGE_FRONT_OFFSET   = 0.5f;
    const float STAGE_ROW_X_FRACTION = 0.85f;

    // Sign visual sizes (world units). User's poster PNGs are ~16:9 (2339×1300, ratio ~1.8).
    // Sized large because the digital twin is at real scale (~60m wide) and each station sits
    // at the front edge -signs need to be readable from a few meters away.
    static readonly Vector2 ENTRANCE_SIGN_SIZE = new(2.4f, 1.35f);
    static readonly Vector2 STAGE_SIGN_SIZE    = new(1.8f, 1.0f);
    static readonly Vector2 PLAY_BUTTON_SIZE   = new(0.78f, 0.26f);   // 3:1, enlarged for visibility

    // White support poles -like the legs of an outdoor exhibition sign. Two poles per sign,
    // placed at the left + right edges, running from the floor up to just under the poster.
    const float POLE_RADIUS       = 0.04f;    // diameter ~8cm
    const float POLE_EDGE_INSET   = 0.06f;    // distance from sign edge to pole centre
    static readonly Color COLOR_POLE_WHITE = new(0.96f, 0.96f, 0.96f);

    // Industrial palette -borders + buttons + ExplanationPlate. The sign FACE is now the PNG,
    // these only colour the surrounding chrome.
    static readonly Color COLOR_ACCENT_CYAN    = new(0.16f, 0.74f, 1f);
    static readonly Color COLOR_WARNING_AMBER  = new(0.95f, 0.65f, 0.10f);
    static readonly Color COLOR_TEXT_WHITE     = new(0.96f, 0.96f, 0.96f);
    static readonly Color COLOR_PLATE_DARK     = new(0.04f, 0.08f, 0.15f);
    static readonly Color COLOR_BUTTON_GREEN   = new(0.12f, 0.55f, 0.20f);

    // Source asset paths.
    const string CUTSCENE_PREFAB_PATH = "Assets/Models/Asset/SIM/sim1.1.0.prefab";
    const string TMP_FONT_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    // Designed Vienna-Method poster PNGs -one per sign. Generated by
    // Assets/SignImages/generate_posters.py (PIL). All title + body text is baked into the PNG.
    const string SIGN_IMG_FOLDER = "Assets/SignImages";
    const string SIGN_IMG_ENTRANCE = SIGN_IMG_FOLDER + "/Sign_Entrance.png";

    // Designed PLAY buttons (generated by Assets/SignImages/generate_buttons.py).
    const string BTN_PLAY_PATH    = SIGN_IMG_FOLDER + "/btn_play.png";
    const string BTN_PLAYALL_PATH = SIGN_IMG_FOLDER + "/btn_playall.png";

    // SIM 4 process-flow diagram overlay (generated by generate_process_overlay.py).
    const string PROCESS_OVERLAY_PATH = SIGN_IMG_FOLDER + "/process_overlay.png";
    static readonly string[] SIGN_IMG_STAGES =
    {
        SIGN_IMG_FOLDER + "/Sign_Stage1_Intake.png",
        SIGN_IMG_FOLDER + "/Sign_Stage2_Grit.png",
        SIGN_IMG_FOLDER + "/Sign_Stage3_Primary.png",
        SIGN_IMG_FOLDER + "/Sign_Stage4_Biological.png",
        SIGN_IMG_FOLDER + "/Sign_Stage5_Secondary.png",
        SIGN_IMG_FOLDER + "/Sign_Stage6_Tertiary.png",
    };

    // The six stages -title + body. Body text is rendered on each StageStation_N's Sign.
    // Content matches the simulation plan that the SIM 1–6 cutscene cameras are demonstrating
    // ("תוכנית סימולציה: תהליך טיהור מי שפכים להשקיה חקלאית").
    static readonly StageInfo[] STAGES =
    {
        new(1, "Stage 1 · Pumping & Coarse Screening",
            "Pre-treatment -intake from multiple sources, removes large debris before pumps.\n\n" +
            "• 3 inflows: domestic sewage, industrial waste, urban runoff\n" +
            "• Coarse bar screens trap wipes, plastics, towels, large debris\n" +
            "• Fine screens catch wood splinters, sawdust, smaller floating bits\n" +
            "• Output: water free of large solids -sand, oils & dissolved matter still inside\n\n" +
            "Protects every downstream pump and pipe."),

        new(2, "Stage 2 · Grit & Oil Separation",
            "Slow-flow chamber that drops sand to the floor and skims grease off the top.\n\n" +
            "• Grit Chamber -controlled, slow flow rate\n" +
            "• Heavy sand & grit settle by gravity to the bottom\n" +
            "• Oils and grease float and are scraped off the surface\n" +
            "• Stops abrasive grit from chewing up downstream machinery\n\n" +
            "Last grit-removal step before primary settling."),

        new(3, "Stage 3 · Primary Clarification",
            "Final physical step -large tanks where suspended organics settle out.\n\n" +
            "• Water sits in primary clarifiers for several hours\n" +
            "• Most suspended organic solids settle as primary sludge\n" +
            "• Lighter floating material is skimmed and removed\n" +
            "• Drops organic load (BOD) significantly before biology\n\n" +
            "Prepares the water for the biological reactor."),

        new(4, "Stage 4 · Biological Reactor",
            "Heart of the plant -bacteria break down dissolved pollutants in 3 controlled tanks.\n\n" +
            "• Three biological tanks hosting pollutant-eating microorganisms\n" +
            "• Strict O₂ control creates aerobic + anaerobic/anoxic zones\n" +
            "• Removes nitrogen & phosphorus to Inbar regulation levels\n" +
            "• Quality feedback loop -failing water is returned to Stage 3 for re-treatment\n\n" +
            "Where bacteria do the real cleaning work."),

        new(5, "Stage 5 · Secondary Clarification",
            "Separates the bacterial 'sludge flocs' from the now-clean water.\n\n" +
            "• Bacterial colonies (biological sludge) settle to the floor\n" +
            "• Most settled sludge returns to the reactor (RAS) to keep bacteria active\n" +
            "• Excess sludge (WAS) is sent to sludge treatment\n" +
            "• Upper-layer water is nearly clear -needs final disinfection\n\n" +
            "Quality gate before tertiary polishing."),

        new(6, "Stage 6 · Filtration & Disinfection",
            "Tertiary polishing brings the water to unrestricted-irrigation quality.\n\n" +
            "• Depth/sand filtration removes the last suspended particles\n" +
            "• Chlorine contact tank (or UV) destroys remaining bacteria and pathogens\n" +
            "• Effluent classified for unrestricted agricultural use\n" +
            "• Pumped out to irrigation reservoirs → farmers' fields\n\n" +
            "Final product: clean irrigation water."),
    };

    struct StageInfo
    {
        public readonly int Number;
        public readonly string Title;
        public readonly string Body;
        public StageInfo(int n, string title, string body) { Number = n; Title = title; Body = body; }
    }

    // (Entrance + per-stage sign text is now baked into the PNG posters under Assets/SignImages/.
    //  Edit that text via Assets/SignImages/generate_posters.py and re-run the script.)

    // ===================== Narration =====================

    // Per-SIM narration: each beat is a short English sentence that appears on the
    // ExplanationPlate (Screen Space Overlay) for NARRATION_WAIT_SECS while the camera pauses
    // at the chosen waypoint. Beats are positioned by FRACTION of waypoint count so they remain
    // evenly distributed regardless of how many waypoints the SIM happens to have. New entries
    // are APPENDED to each CameraMove's existing customTriggers -pre-authored object-toggle
    // triggers are preserved untouched.
    const float NARRATION_WAIT_SECS = 4.0f;

    static readonly float[] NARRATION_FRACTIONS_3 = { 0.20f, 0.50f, 0.80f };
    static readonly float[] NARRATION_FRACTIONS_4 = { 0.15f, 0.40f, 0.65f, 0.88f };

    static readonly string[][] NARRATION_BEATS =
    {
        // SIM 1 -Intake & Coarse Screening (4 beats)
        new[]
        {
            "Stage 1 · Intake.\nRaw sewage arrives from three sources - domestic, industrial, urban runoff.",
            "Coarse bar screens trap large debris: wipes, plastics, towels, cloth.",
            "Fine screens catch wood splinters, sawdust, and smaller floating particles.",
            "Water is now free of large solids -ready for grit removal in Stage 2.",
        },
        // SIM 2 -Grit & Oil (3 beats)
        new[]
        {
            "Stage 2 · Grit Chamber.\nSlow controlled flow at 0.3 m/s lets gravity do the work.",
            "Heavy sand and grit settle to the floor of the chamber.",
            "Oils and grease float and are skimmed off the top.",
        },
        // SIM 3 -Primary Clarification (3 beats)
        new[]
        {
            "Stage 3 · Primary Clarifier.\nWater sits in large tanks for several hours.",
            "Heavy organic solids settle to the floor as primary sludge.",
            "BOD drops sharply - water is now ready for the biological stage.",
        },
        // SIM 4 -Biological Reactor (4 beats)
        new[]
        {
            "Stage 4 · Bioreactor.\nThree tanks host bacteria that eat dissolved pollutants.",
            "Anaerobic and anoxic zones break down nitrogen and phosphorus.",
            "Aerobic zone - oxygen pumped in to feed the bacteria.",
            "Quality check: failing water is returned to Stage 3 for re-treatment.",
        },
        // SIM 5 -Secondary Clarifier (3 beats)
        new[]
        {
            "Stage 5 · Secondary Clarifier.\nBacterial sludge flocs separate from the clean water.",
            "Most settled sludge returns to the reactor as RAS - keeps bacteria active.",
            "Excess sludge (WAS) is routed to sludge treatment.",
        },
        // SIM 6 -Filtration & Disinfection (4 beats)
        new[]
        {
            "Stage 6 · Tertiary.\nWater passes through sand filters for final particle removal.",
            "Chlorine contact tank -30-minute disinfection kills all remaining pathogens.",
            "Effluent meets the Israeli Inbar irrigation standard.",
            "Sent to farmer reservoirs and out to Jordan Valley fields.",
        },
    };

    // ===================== Menus =====================

    [MenuItem("MCP/WWTP Tour/Delete Old Simulation Area")]
    public static void DeleteOldSimulationArea()
    {
        var existing = GameObject.Find("Simulation Area");
        if (existing == null)
        {
            Debug.Log("[WWTPTourBuild] No 'Simulation Area' GameObject to delete.");
            return;
        }
        ClearSelectionIfWithin(existing.transform);
        Object.DestroyImmediate(existing);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[WWTPTourBuild] Deleted 'Simulation Area' (template leftover).");
    }

    [MenuItem("MCP/WWTP Tour/Build Full WWTP Tour")]
    public static void BuildFullTour()
    {
        // ---- 1. Instantiate (or refresh) the cutscenes package ----
        var cutscenePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CUTSCENE_PREFAB_PATH);
        if (cutscenePrefab == null)
        {
            Debug.LogError($"[WWTPTourBuild] Cutscene prefab not found at '{CUTSCENE_PREFAB_PATH}'. Did the sim1.1.0.unitypackage import succeed?");
            return;
        }

        // Preserve any station positions the user moved by hand: capture EntranceStation +
        // StageStation_N world transforms BEFORE wiping, then restore them after the rebuild.
        var savedStations = new Dictionary<string, (Vector3 pos, Quaternion rot)>();
        var existingTour = GameObject.Find("WWTP_Tour");
        if (existingTour != null)
            foreach (Transform child in existingTour.transform)
                if (child.name == "EntranceStation" || child.name.StartsWith("StageStation"))
                    savedStations[child.name] = (child.position, child.rotation);

        // Wipe previous cutscene instances + tour root for clean rebuild
        DestroyByName("WWTP_Tour");
        DestroyByName("sim1.1.0");
        DestroyByName("WWTP_Cutscenes");

        var cutsceneRoot = (GameObject)PrefabUtility.InstantiatePrefab(cutscenePrefab);
        cutsceneRoot.name = "WWTP_Cutscenes";
        cutsceneRoot.transform.position = TOUR_WORLD_POS;
        cutsceneRoot.transform.rotation = Quaternion.identity;
        cutsceneRoot.transform.localScale = Vector3.one * CUTSCENE_PACKAGE_SCALE;

        // Find CutsceneManager + the 6 CameraMoves inside
        var cutsceneManagerType = System.Type.GetType("CutsceneManager, Assembly-CSharp");
        var cameraMoveType      = System.Type.GetType("CameraMove, Assembly-CSharp");
        if (cutsceneManagerType == null || cameraMoveType == null)
        {
            Debug.LogError("[WWTPTourBuild] CutsceneManager or CameraMove type not found in Assembly-CSharp. Did the package compile?");
            return;
        }
        var cutsceneManager = cutsceneRoot.GetComponentInChildren(cutsceneManagerType);
        if (cutsceneManager == null)
        {
            Debug.LogError("[WWTPTourBuild] CutsceneManager component not found inside the instantiated cutscene prefab.");
            return;
        }
        var cameraMoves = cutsceneRoot.GetComponentsInChildren(cameraMoveType, includeInactive: true);
        Debug.Log($"[WWTPTourBuild] Found CutsceneManager + {cameraMoves.Length} CameraMove(s).");

        // ---- 2. Wire Main Camera (XR Origin's Main Camera) onto CutsceneManager ----
        var xrMainCam = FindXRMainCamera();
        if (xrMainCam == null)
            Debug.LogWarning("[WWTPTourBuild] Could not find XR Main Camera -assign 'mainCamera' on CutsceneManager manually in the Inspector.");
        var soManager = new SerializedObject(cutsceneManager);
        SetObj(soManager, "mainCamera", xrMainCam);
        soManager.ApplyModifiedPropertiesWithoutUndo();

        // ---- 3. Create tour root + ExplanationPlate ----
        var tourRoot = new GameObject("WWTP_Tour");
        tourRoot.transform.position = TOUR_WORLD_POS;

        var plate = BuildExplanationPlate(tourRoot.transform);

        // ---- 4. Wire every CameraMove's uiTextBox to the same ExplanationPlate ----
        // Plate's Panel is inactive at build time, so include-inactive when fetching the TMP.
        var plateText = plate.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
        foreach (var cm in cameraMoves)
        {
            var soMove = new SerializedObject(cm);
            SetObj(soMove, "uiTextBox", plateText);
            soMove.ApplyModifiedPropertiesWithoutUndo();
        }
        // Panel starts hidden (set in BuildExplanationPlate). CameraMove enables the SAME
        // GameObject (Image + TMP live on it together) on a narration trigger and disables it
        // again after waitTime -so the dark subtitle bar only appears while text is showing.

        // ---- 5. Compute the actual world-space bounds of the digital twin ----
        // ONLY the plant model child ("Beit Shan SIM 1.0.1"), NOT the whole prefab -the
        // `cutscenes` sibling holds the camera path GameObjects at y=-100 (hidden underground)
        // and including those would skew bounds wildly. The plant model is at WWTP_Cutscenes's
        // first non-cutscene child; we look for it by name fallback to first child.
        GameObject plantModel = FindPlantModel(cutsceneRoot);
        Bounds bounds = ComputeRendererBounds(plantModel);
        Debug.Log($"[WWTPTourBuild] Plant model = '{plantModel.name}'. Bounds: center={bounds.center}, size={bounds.size}");

        // ---- 6. Build the Entrance station (PLAY ALL) in front of the +Z face ----
        BuildEntranceStation(tourRoot.transform, cutsceneManager, bounds);

        // ---- 7. Build the 6 Stage stations, each at its SIM camera's startPoint ----
        for (int i = 0; i < STAGES.Length && i < cameraMoves.Length; i++)
            BuildStageStation(tourRoot.transform, STAGES[i], i, cameraMoves[i] as MonoBehaviour, cutsceneManager, bounds);

        // ---- 7b. Append narration triggers to each SIM camera (preserves existing ones) ----
        for (int i = 0; i < NARRATION_BEATS.Length && i < cameraMoves.Length; i++)
            AddNarrationTriggers(cameraMoves[i] as MonoBehaviour, NARRATION_BEATS[i]);

        if (cameraMoves.Length < STAGES.Length)
            Debug.LogWarning($"[WWTPTourBuild] Cutscene package only has {cameraMoves.Length} CameraMoves -built that many stage stations. Edit STAGES[] if more are added later.");

        // ---- 7c. Build the realistic activated-sludge sim, disable the package's crude particle
        //          effects in SIM 4, and choreograph the full cinematic camera flythrough.
        float bioSurfaceY = EnhanceBioreactorProcess(tourRoot.transform, cutsceneRoot);
        if (!float.IsNaN(bioSurfaceY))
        {
            DisablePackageSim4Particles(cutsceneRoot);
            ChoreographSim4Cutscene(cutsceneRoot, tourRoot.transform, bioSurfaceY);
        }

        // NOTE: Teleport_Target_Sim is INTENTIONALLY NOT moved here. The user fell through the
        // floor the last time it was auto-relocated; preserving the existing manually-placed
        // teleport landing is safer. The separate menu "MCP/WWTP Tour/Repoint Teleport Target"
        // is still available if she ever wants to re-derive it from the model bounds.

        // ---- 7d. Restore any hand-moved station positions captured before the wipe ----
        if (savedStations.Count > 0)
        {
            int restored = 0;
            foreach (Transform child in tourRoot.transform)
                if (savedStations.TryGetValue(child.name, out var x))
                { child.position = x.pos; child.rotation = x.rot; restored++; }
            Debug.Log($"[WWTPTourBuild] Restored {restored} hand-moved station position(s).");
        }

        // ---- 7. Finalize ----
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = tourRoot;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("[WWTPTourBuild] Built full WWTP Tour: cutscene prefab placed, ExplanationPlate created, " +
                  $"Entrance + {Mathf.Min(STAGES.Length, cameraMoves.Length)} stage stations wired.");
    }

    /// <summary>
    /// Rebuilds ONLY the SIM 4 (Biological Reactor) activated-sludge FX + cinematic camera path on
    /// the cutscene package that is ALREADY in the scene — without re-instantiating the prefab.
    /// This is the safe way to iterate on the underwater biology after the user has hand-placed the
    /// plant (WWTP_Cutscenes) and the signs: nothing is moved, only the SIM 4 effects + waypoints
    /// are regenerated in place. (BuildFullTour, by contrast, re-instantiates the plant at
    /// TOUR_WORLD_POS and would undo a manual move.)
    /// </summary>
    [MenuItem("MCP/WWTP Tour/Rebuild SIM 4 Biology (in place)")]
    public static void RebuildSim4BiologyInPlace()
    {
        var cutsceneRoot = GameObject.Find("WWTP_Cutscenes");
        if (cutsceneRoot == null) { Debug.LogError("[WWTPTourBuild] 'WWTP_Cutscenes' not found in scene. Run Build Full WWTP Tour first."); return; }
        var tour = GameObject.Find("WWTP_Tour");
        if (tour == null) { Debug.LogError("[WWTPTourBuild] 'WWTP_Tour' not found in scene."); return; }

        // Drop the previous SIM 4 FX so we don't stack duplicates. Signs/stations are untouched.
        Selection.objects = new Object[0];   // avoid stale-inspector errors on DestroyImmediate
        foreach (var n in new[] { "BioreactorProcess_FX", "DiveBubbles", "DiveBiology_CameraCloud", "DiveBiology_Static" })
        {
            var t = tour.transform.Find(n);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        // The camera-locked biomass cloud is now parented to the SIM 4 camera (inside WWTP_Cutscenes),
        // not under WWTP_Tour — purge any leftover copies there too so rebuilds don't stack.
        foreach (var tr in cutsceneRoot.GetComponentsInChildren<Transform>(true))
            if (tr != null && tr.name == "DiveBiology_CameraCloud") { Object.DestroyImmediate(tr.gameObject); break; }

        float surfaceY = EnhanceBioreactorProcess(tour.transform, cutsceneRoot);
        if (float.IsNaN(surfaceY)) { Debug.LogError("[WWTPTourBuild] Bioreactor group not found — aborting SIM 4 rebuild."); return; }
        DisablePackageSim4Particles(cutsceneRoot);
        ChoreographSim4Cutscene(cutsceneRoot, tour.transform, surfaceY);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[WWTPTourBuild] Rebuilt SIM 4 biology + camera path IN PLACE (plant & signs not moved).");
    }

    /// <summary>
    /// Re-skins the existing PLAY / PLAY ALL buttons with the designed pill textures, IN PLACE —
    /// it does NOT rebuild the tour or move anything, so the sign positions the user set by hand
    /// are preserved. Run after moving signs around.
    /// </summary>
    [MenuItem("MCP/WWTP Tour/Restyle PLAY Buttons")]
    public static void RestylePlayButtons()
    {
        var playSprite    = EnsureButtonSprite(BTN_PLAY_PATH);
        var playAllSprite = EnsureButtonSprite(BTN_PLAYALL_PATH);
        if (playSprite == null || playAllSprite == null)
        {
            Debug.LogError("[WWTPTourBuild] Button sprites not found — run Assets/SignImages/generate_buttons.py first.");
            return;
        }

        var tour = GameObject.Find("WWTP_Tour");
        if (tour == null) { Debug.LogError("[WWTPTourBuild] WWTP_Tour not found."); return; }

        int restyled = 0;
        foreach (var btn in tour.GetComponentsInChildren<Button>(includeInactive: true))
        {
            // Walk up to the station-button root ("Button_PlayAll" / "Button_PlayStageN").
            Transform r = btn.transform;
            while (r != null && !r.name.StartsWith("Button_Play")) r = r.parent;
            bool isPlayAll = r != null && r.name.Contains("PlayAll");

            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = isPlayAll ? playAllSprite : playSprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;     // texture is 3:1, button is 3:1 → fills cleanly
                img.color = Color.white;        // show the sprite's true colours
            }
            // Resize the world-space Canvas to the current PLAY_BUTTON_SIZE (grows from centre,
            // so the button enlarges in place without changing where it sits on the sign).
            var canvasRT = btn.transform.parent != null ? btn.transform.parent.GetComponent<RectTransform>() : null;
            if (canvasRT != null) canvasRT.sizeDelta = PLAY_BUTTON_SIZE;
            // The label is baked into the sprite now — hide the runtime TMP label.
            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.gameObject.SetActive(false);
            restyled++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[WWTPTourBuild] Restyled {restyled} PLAY button(s) in place (positions untouched).");
    }

    /// <summary>Ensures a PNG imports as a Sprite (Single) and returns it.</summary>
    static Sprite EnsureButtonSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    [MenuItem("MCP/WWTP Tour/Repoint Teleport Target")]
    public static void RepointTeleportTarget()
    {
        // Re-derive bounds from the live WWTP_Cutscenes GO so this menu works without rebuilding
        // the whole tour. If the cutscenes haven't been instantiated yet, fall back to the static
        // anchor so we at least don't error.
        var cutsceneRoot = GameObject.Find("WWTP_Cutscenes");
        if (cutsceneRoot == null)
        {
            Debug.LogError("[WWTPTourBuild] WWTP_Cutscenes not in scene -run 'Build Full WWTP Tour' first.");
            return;
        }
        var plantModel = FindPlantModel(cutsceneRoot);
        Bounds bounds = ComputeRendererBounds(plantModel);
        PositionTeleportTargetFromBounds(bounds, TOUR_WORLD_POS.y);
    }

    /// <summary>
    /// Finds the plant model GameObject inside WWTP_Cutscenes. Tries by name first
    /// ("Beit Shan SIM 1.0.1"), falls back to the first child that isn't the "cutscenes" branch.
    /// </summary>
    static GameObject FindPlantModel(GameObject cutsceneRoot)
    {
        // Search children by name
        foreach (Transform child in cutsceneRoot.transform)
        {
            string n = child.name.ToLowerInvariant();
            if (n.Contains("beit shan") || n.Contains("sim 1.0")) return child.gameObject;
        }
        // Fallback: first child that doesn't have a CutsceneManager component
        var managerType = System.Type.GetType("CutsceneManager, Assembly-CSharp");
        foreach (Transform child in cutsceneRoot.transform)
        {
            if (managerType != null && child.GetComponent(managerType) != null) continue;
            return child.gameObject;
        }
        return cutsceneRoot; // last-ditch
    }

    static void PositionTeleportTargetFromBounds(Bounds bounds, float floorY)
    {
        var target = GameObject.Find("Teleport_Target_Sim");
        if (target == null) { Debug.LogError("[WWTPTourBuild] Teleport_Target_Sim not found."); return; }

        // Land OUTSIDE the bounding box at the +Z face, facing back toward bounds.center so the
        // first thing the player sees on teleport is the entrance sign + the plant behind it.
        Vector3 landing = new Vector3(bounds.center.x, floorY, bounds.max.z + TELEPORT_FRONT_OFFSET);
        Vector3 lookAt  = new Vector3(bounds.center.x, floorY, bounds.center.z);
        target.transform.position = landing;
        target.transform.rotation = Quaternion.LookRotation(lookAt - landing, Vector3.up);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[WWTPTourBuild] Teleport_Target_Sim → {landing} facing the WWTP digital twin.");
    }

    // ===================== Builders =====================

    /// <summary>
    /// The single TMPro element the cutscene cameras write their per-waypoint text to.
    /// Built as a Screen Space Overlay canvas -the cutscene swaps its camera onto the HMD via
    /// targetDisplay, so a screen-space overlay reliably appears regardless of where the
    /// underground cutscene cameras are physically located.
    ///
    /// IMPORTANT: the dark subtitle panel + text live on the SAME GameObject as the TMPro
    /// component, so CameraMove's `uiTextBox.gameObject.SetActive(true/false)` toggles the
    /// whole subtitle (background AND text) together. The panel is built INACTIVE so nothing
    /// appears on screen until a cutscene actually fires a narration trigger.
    /// </summary>
    static GameObject BuildExplanationPlate(Transform parent)
    {
        var plate = new GameObject("ExplanationPlate");
        plate.transform.SetParent(parent, false);
        plate.transform.localPosition = Vector3.zero;

        var canvasGO = new GameObject("Canvas");
        canvasGO.transform.SetParent(plate.transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // on top of any other UI
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Subtitle panel -Image (dark backing). Image and TextMeshProUGUI BOTH extend Graphic,
        // and Unity allows only one Graphic per GameObject -so the text lives on a sibling
        // GameObject, not a child of the panel. A small `MirrorActiveState` script on the text
        // mirrors its active state onto the Panel so the dark backing appears/disappears with
        // the subtitle text (CameraMove only toggles the text's GameObject).
        // Subtitle band anchored higher up -bottom edge sits ~12% from the screen bottom so
        // descenders aren't clipped on cinematic / wide aspect ratios where the actual visible
        // bottom edge ends up well above 0% Y.
        const float SUB_BOTTOM = 0.12f;
        const float SUB_TOP    = 0.32f;

        var panelGO = new GameObject("Panel", typeof(RectTransform));
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.1f, SUB_BOTTOM);
        panelRT.anchorMax = new Vector2(0.9f, SUB_TOP);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        var bg = panelGO.AddComponent<Image>();
        bg.color = new Color(COLOR_PLATE_DARK.r, COLOR_PLATE_DARK.g, COLOR_PLATE_DARK.b, 0.85f);

        // Subtitle text on a SIBLING GO with same anchors + generous insets so 2-3 line beats fit
        // without clipping.
        var textGO = new GameObject("ExplanationText", typeof(RectTransform));
        textGO.transform.SetParent(canvasGO.transform, false);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.1f, SUB_BOTTOM);
        textRT.anchorMax = new Vector2(0.9f, SUB_TOP);
        textRT.offsetMin = new Vector2(40, 24);
        textRT.offsetMax = new Vector2(-40, -24);
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.font = LoadTMPFont();
        tmp.fontSize = 32f;
        tmp.color = COLOR_TEXT_WHITE;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;

        // Mirror the text's active state onto the Panel so the dark backing only shows when text
        // is showing. Use reflection so this editor script doesn't need to compile against the
        // runtime script.
        var mirrorType = System.Type.GetType("MirrorActiveState, Assembly-CSharp");
        if (mirrorType != null)
        {
            var mirror = textGO.AddComponent(mirrorType);
            var soMirror = new SerializedObject(mirror);
            var prop = soMirror.FindProperty("target");
            if (prop != null) prop.objectReferenceValue = panelGO;
            soMirror.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[WWTPTourBuild] MirrorActiveState type not found -panel will stay visible. Make sure Assets/Scripts/Simulation/MirrorActiveState.cs has compiled.");
        }

        // Hidden until a cutscene narration trigger activates the text (Panel follows via mirror).
        textGO.SetActive(false);
        panelGO.SetActive(false);

        return plate;
    }

    static GameObject BuildEntranceStation(Transform parent, Object cutsceneManager, Bounds bounds)
    {
        // Entrance sits BETWEEN the teleport landing (+Z further out) and the row of stage signs
        // (+Z closer in), centred on the plant's X axis. Identity rotation -its Quad's +Z normal
        // already faces the player approaching from +Z.
        var station = new GameObject("EntranceStation");
        station.transform.SetParent(parent, false);
        Vector3 worldPos = new Vector3(bounds.center.x,
                                       parent.position.y + STAGE_SIGN_EYE_HEIGHT,
                                       bounds.max.z + ENTRANCE_FRONT_OFFSET + 0.6f);
        station.transform.position = worldPos;
        station.transform.rotation = Quaternion.identity;

        BuildSignPanel(station.transform, "Sign_Entrance",
                       localPos: Vector3.zero,
                       size: ENTRANCE_SIGN_SIZE,
                       pngPath: SIGN_IMG_ENTRANCE);

        BuildPlayButton(station.transform, "Button_PlayAll",
                        localPos: new Vector3(0, -ENTRANCE_SIGN_SIZE.y * 0.5f - 0.12f, -0.04f),
                        label: "▶ PLAY ALL",
                        cutsceneManager: cutsceneManager,
                        methodName: "PlayAllCutscenes",
                        intArg: -1, isPlayAll: true);

        return station;
    }

    static void BuildStageStation(Transform parent, StageInfo info, int index,
                                  MonoBehaviour cameraMove, Object cutsceneManager,
                                  Bounds bounds)
    {
        var station = new GameObject($"StageStation_{info.Number}");
        station.transform.SetParent(parent, false);

        // Arrange the 6 stations in a row along the +Z FRONT face of the plant model. Each
        // station occupies one slot of STAGE_ROW_X_FRACTION of the plant width. So as the player
        // walks left-to-right along the front of the digital twin they pass Stage 1 → 2 → … → 6
        // in order, each adjacent to a different region of the visible model.
        float spanHalf = bounds.size.x * STAGE_ROW_X_FRACTION * 0.5f;
        float t        = STAGES.Length == 1 ? 0.5f : (float)index / (STAGES.Length - 1);
        float worldX   = Mathf.Lerp(bounds.center.x - spanHalf, bounds.center.x + spanHalf, t);
        float worldY   = parent.position.y + STAGE_SIGN_EYE_HEIGHT;
        float worldZ   = bounds.max.z + STAGE_FRONT_OFFSET;
        Vector3 worldPos = new Vector3(worldX, worldY, worldZ);

        station.transform.position = worldPos;
        // Face +Z so the sign's front Quad normal points toward the player (who teleports in
        // from further +Z). Identity rotation works because Quad's mesh faces +Z by default.
        station.transform.rotation = Quaternion.identity;

        BuildSignPanel(station.transform, $"Sign_Stage{info.Number}",
                       localPos: Vector3.zero,
                       size: STAGE_SIGN_SIZE,
                       pngPath: SIGN_IMG_STAGES[index]);

        BuildPlayButton(station.transform, $"Button_PlayStage{info.Number}",
                        localPos: new Vector3(0, -STAGE_SIGN_SIZE.y * 0.5f - 0.12f, -0.04f),
                        label: $"▶ PLAY · Stage {info.Number}",
                        cutsceneManager: cutsceneManager,
                        methodName: "PlayCutscene",
                        intArg: index, isPlayAll: false);
    }

    static void BuildImageGalleryPlaceholder(Transform parent, int stageNumber)
    {
        var panel = new GameObject("ImageGallery_Placeholder");
        panel.transform.SetParent(parent, false);
        panel.transform.localPosition = new Vector3(0.8f, 1.5f, 0); // beside the sign at eye level

        Vector2 size = new(0.55f, 0.40f);   // sized for VR room scale

        // Dashed-look border (cyan accent)
        var border = NewQuad(panel.transform, "Border");
        border.localScale = new Vector3(size.x * 1.05f, size.y * 1.06f, 1f);
        border.localPosition = new Vector3(0, 0, 0.04f);
        AssignSolidColor(border.gameObject, COLOR_ACCENT_CYAN);

        // Light grey "empty" interior so the placeholder reads as "to be filled"
        var bg = NewQuad(panel.transform, "Background");
        bg.localScale = new Vector3(size.x, size.y, 1f);
        bg.localPosition = new Vector3(0, 0, 0.02f);
        AssignSolidColor(bg.gameObject, new Color(0.18f, 0.22f, 0.28f));

        // Text
        var canvasGO = new GameObject("Canvas");
        canvasGO.transform.SetParent(panel.transform, false);
        canvasGO.transform.localPosition = new Vector3(0, 0, -0.01f);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.6f); titleRT.anchorMax = new Vector2(1, 1f);
        titleRT.offsetMin = new Vector2(0.08f, 0.02f); titleRT.offsetMax = new Vector2(-0.08f, -0.02f);
        var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "Image Gallery";
        titleTmp.font = LoadTMPFont();
        titleTmp.fontSize = size.y * 0.18f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = COLOR_ACCENT_CYAN;
        titleTmp.alignment = TextAlignmentOptions.Center;

        var bodyGO = new GameObject("Body");
        bodyGO.transform.SetParent(canvasGO.transform, false);
        var bodyRT = bodyGO.AddComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0, 0.25f); bodyRT.anchorMax = new Vector2(1, 0.6f);
        bodyRT.offsetMin = new Vector2(0.02f, 0); bodyRT.offsetMax = new Vector2(-0.02f, 0);
        var bodyTmp = bodyGO.AddComponent<TextMeshProUGUI>();
        bodyTmp.text = $"Stage {stageNumber}\nDetail sheets\ncoming soon";
        bodyTmp.font = LoadTMPFont();
        bodyTmp.fontSize = size.y * 0.10f;
        bodyTmp.color = COLOR_TEXT_WHITE;
        bodyTmp.alignment = TextAlignmentOptions.Center;

        // Greyed-out prev/next labels so the layout reads as a real gallery panel
        var navGO = new GameObject("NavStub");
        navGO.transform.SetParent(canvasGO.transform, false);
        var navRT = navGO.AddComponent<RectTransform>();
        navRT.anchorMin = new Vector2(0, 0f); navRT.anchorMax = new Vector2(1, 0.25f);
        navRT.offsetMin = new Vector2(0.02f, 0.01f); navRT.offsetMax = new Vector2(-0.02f, -0.01f);
        var navTmp = navGO.AddComponent<TextMeshProUGUI>();
        navTmp.text = "◀ Prev    1 / -   Next ▶";
        navTmp.font = LoadTMPFont();
        navTmp.fontSize = size.y * 0.085f;
        navTmp.color = new Color(0.55f, 0.65f, 0.75f); // muted -clearly disabled
        navTmp.alignment = TextAlignmentOptions.Center;
    }

    /// <summary>
    /// Builds a sign as a textured Quad held up by two white support poles -like an outdoor
    /// exhibition info panel at a park or museum. The poster (with title + body baked in) is the
    /// front face; a thin cyan border + dark backing plate sit behind it for VR contrast; two
    /// white cylinders run from the floor up to the sign's bottom edge at the left/right corners.
    /// </summary>
    /// <param name="parent">The station's Transform (assumed at sign-eye-level above the floor).</param>
    /// <param name="size">Sign width × height in world meters.</param>
    static void BuildSignPanel(Transform parent, string name, Vector3 localPos, Vector2 size, string pngPath)
    {
        var sign = new GameObject(name);
        sign.transform.SetParent(parent, false);
        sign.transform.localPosition = localPos;

        // Cyan border -sits the furthest back so it shows as a thin frame around the poster.
        var border = NewQuad(sign.transform, "Border");
        border.localScale = new Vector3(size.x * 1.04f, size.y * 1.06f, 1f);
        border.localPosition = new Vector3(0, 0, 0.04f);
        AssignSolidColor(border.gameObject, COLOR_ACCENT_CYAN);

        // Dark backing plate -between border and poster, gives a soft black frame.
        var backing = NewQuad(sign.transform, "Backing");
        backing.localScale = new Vector3(size.x * 1.01f, size.y * 1.02f, 1f);
        backing.localPosition = new Vector3(0, 0, 0.025f);
        AssignSolidColor(backing.gameObject, COLOR_PLATE_DARK);

        // Poster -front Quad with the designed PNG as the unlit base map.
        var poster = NewQuad(sign.transform, "Poster");
        poster.localScale = new Vector3(size.x, size.y, 1f);
        poster.localPosition = new Vector3(0, 0, 0f);
        AssignTexture(poster.gameObject, pngPath);

        // Two white support poles. Station pivot is at eye level (STAGE_SIGN_EYE_HEIGHT above
        // the floor), so in the station's local space the floor is at y = -STAGE_SIGN_EYE_HEIGHT
        // and the sign's bottom edge is at y = localPos.y - size.y/2. Each pole spans from
        // floor up to just under the sign, offset slightly behind so the poster covers the top.
        float floorLocalY = -STAGE_SIGN_EYE_HEIGHT;
        float signBottomY = localPos.y - size.y * 0.5f;
        float poleLength  = Mathf.Max(0.2f, signBottomY - floorLocalY);
        float poleCenterY = (floorLocalY + signBottomY) * 0.5f;
        float poleXOffset = size.x * 0.5f - POLE_EDGE_INSET;
        BuildPole(parent, $"{name}_Pole_L", new Vector3(-poleXOffset, poleCenterY, localPos.z + 0.05f), poleLength);
        BuildPole(parent, $"{name}_Pole_R", new Vector3(+poleXOffset, poleCenterY, localPos.z + 0.05f), poleLength);
    }

    /// <summary>
    /// Appends per-waypoint narration text triggers to a CameraMove's customTriggers array.
    /// Existing entries are preserved (so any pre-authored object-toggle triggers keep working).
    /// Each new entry pauses the camera at its waypoint, writes its text to uiTextBox (the
    /// shared ExplanationPlate), waits NARRATION_WAIT_SECS, then hides the text and resumes.
    /// </summary>
    static void AddNarrationTriggers(MonoBehaviour cameraMove, string[] beats)
    {
        if (cameraMove == null || beats == null || beats.Length == 0) return;

        var so          = new SerializedObject(cameraMove);
        var triggersArr = so.FindProperty("customTriggers");
        var pointsArr   = so.FindProperty("points");
        if (triggersArr == null || !triggersArr.isArray || pointsArr == null || !pointsArr.isArray)
        {
            Debug.LogWarning($"[WWTPTourBuild] {cameraMove.name}: customTriggers / points property not found -skipping narration.");
            return;
        }

        int pointCount = pointsArr.arraySize;
        if (pointCount < 2) return;

        float[] fractions = (beats.Length >= 4) ? NARRATION_FRACTIONS_4 : NARRATION_FRACTIONS_3;
        int startIdx = triggersArr.arraySize;

        for (int i = 0; i < beats.Length; i++)
        {
            float frac = (i < fractions.Length) ? fractions[i] : (i + 1f) / (beats.Length + 1f);
            int   wp   = Mathf.Clamp(Mathf.RoundToInt(frac * (pointCount - 1)), 1, pointCount - 1);

            triggersArr.InsertArrayElementAtIndex(triggersArr.arraySize);
            var entry = triggersArr.GetArrayElementAtIndex(triggersArr.arraySize - 1);

            var pLabel   = entry.FindPropertyRelative("triggerLabel");
            var pIndex   = entry.FindPropertyRelative("triggerAtPointIndex");
            var pTarget  = entry.FindPropertyRelative("targetObject");
            var pAction  = entry.FindPropertyRelative("action");
            var pWait    = entry.FindPropertyRelative("waitTime");
            var pText    = entry.FindPropertyRelative("explanationText");

            if (pLabel  != null) pLabel.stringValue            = $"Narration {i + 1}";
            if (pIndex  != null) pIndex.intValue               = wp;
            if (pTarget != null) pTarget.objectReferenceValue  = null;
            if (pAction != null) pAction.enumValueIndex        = 0; // None -don't toggle objects
            if (pWait   != null) pWait.floatValue              = NARRATION_WAIT_SECS;
            if (pText   != null) pText.stringValue             = beats[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[WWTPTourBuild] {cameraMove.name}: added {beats.Length} narration trigger(s) (waypoints / {pointCount} total).");
    }

    // ===================== Bioreactor water + aeration =====================

    /// <summary>
    /// Builds a realistic Activated-Sludge simulation in the SIM 4 bioreactor: a murky-brown water
    /// surface, plus three oxygen zones (Anaerobic / Anoxic / Aerobic) mapped onto the model's
    /// three tank groups. Each zone gets floor diffuser pipes, rising aeration bubbles, drifting
    /// biological-sludge clouds, suspended micro-particles, gentle circular currents, and a soft
    /// underwater light — scaled by oxygen level so the gradient reads anaerobic→aerobic. All FX
    /// are parented under WWTP_Tour (world scale 1) and rebuilt cleanly each time.
    /// </summary>
    /// <returns>The world-space Y of the water surface, or float.NaN if it couldn't be built.</returns>
    static float EnhanceBioreactorProcess(Transform tourRoot, GameObject cutsceneRoot)
    {
        var bioGroup = FindDescendantByName(cutsceneRoot.transform, "Biological Treatment stage 4 cutscene");
        if (bioGroup == null)
        {
            Debug.LogWarning("[WWTPTourBuild] 'Biological Treatment stage 4 cutscene' not found — skipping bioreactor FX.");
            return float.NaN;
        }

        Bounds full = ComputeBioreactorTankBounds(bioGroup);
        if (full.size == Vector3.zero) { Debug.LogWarning("[WWTPTourBuild] Bioreactor bounds empty — skipping FX."); return float.NaN; }

        float surfaceY = full.max.y - full.size.y * 0.12f;   // just under the wall rim

        var fx = new GameObject("BioreactorProcess_FX");
        fx.transform.SetParent(tourRoot, true);              // world units, no inherited scale
        fx.transform.position = full.center;
        fx.transform.rotation = Quaternion.identity;
        fx.transform.localScale = Vector3.one;

        // ---- Murky-brown water surface across the whole footprint ----
        var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
        water.name = "WaterSurface";
        var wcol = water.GetComponent<Collider>(); if (wcol != null) Object.DestroyImmediate(wcol);
        water.transform.SetParent(fx.transform, false);
        water.transform.position = new Vector3(full.center.x, surfaceY, full.center.z);
        water.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        water.transform.localScale = new Vector3(full.size.x * 0.97f, full.size.z * 0.97f, 1f);
        water.GetComponent<MeshRenderer>().sharedMaterial = MakeWaterSurfaceMaterial();

        // ---- Map the three tank groups to the three oxygen zones, ordered by world X (flow dir).
        var groups = new List<(string name, Bounds b)>();
        foreach (var zn in new[] { "Biological Treatment", "Biological Treatment (1)", "Biological Treatment (2)" })
        {
            var g = FindDescendantByName(bioGroup, zn);
            if (g == null) continue;
            var gb = ComputeBioreactorTankBounds(g);
            if (gb.size != Vector3.zero) groups.Add((zn, gb));
        }
        groups.Sort((x, y) => x.b.center.x.CompareTo(y.b.center.x));

        // anaerobic (still / dark) → anoxic (gentle) → aerobic (vigorous, lots of air)
        // Deliberate gradient so the camera reads the transition Anaerobic → Anoxic → Aerobic:
        // bubbles, swirl, micro-life and light all ramp UP toward aerobic; warm/dark → cool/bright.
        var specs = new[]
        {
            new ZoneSpec("Anaerobic", bubbleRate:  5f,  swirl:  5f, sludgeRate: 55f, micro:  0f,
                         lightColor: new Color(0.55f, 0.40f, 0.24f), lightIntensity: 0.6f),    // darkest, calmest, almost no bubbles
            new ZoneSpec("Anoxic",    bubbleRate: 60f,  swirl: 24f, sludgeRate: 42f, micro: 28f,
                         lightColor: new Color(0.70f, 0.72f, 0.66f), lightIntensity: 1.1f),    // neutral, gentle mixing, small bubbles
            new ZoneSpec("Aerobic",   bubbleRate:620f,  swirl: 60f, sludgeRate: 26f, micro: 80f,
                         lightColor: new Color(0.80f, 0.90f, 1.00f), lightIntensity: 1.9f),    // brightest, most active, dense bubble streams
        };

        int zcount = Mathf.Min(groups.Count, specs.Length);
        for (int i = 0; i < zcount; i++)
            BuildBioZone(fx.transform, groups[i].name, groups[i].b, specs[i], surfaceY);

        Debug.Log($"[WWTPTourBuild] Activated-sludge sim built: surface y={surfaceY:F1}, {zcount} oxygen zones, footprint {full.size.x:F0}×{full.size.z:F0}.");
        return surfaceY;
    }

    struct ZoneSpec
    {
        public string label;
        public float bubbleRate;     // aeration intensity
        public float swirl;          // orbital current strength (deg/s-ish)
        public float sludgeRate;     // drifting biological-sludge cloud density
        public float micro;          // suspended micro-particle ("life") density
        public Color lightColor;
        public float lightIntensity;
        public ZoneSpec(string label, float bubbleRate, float swirl, float sludgeRate, float micro, Color lightColor, float lightIntensity)
        { this.label = label; this.bubbleRate = bubbleRate; this.swirl = swirl; this.sludgeRate = sludgeRate; this.micro = micro; this.lightColor = lightColor; this.lightIntensity = lightIntensity; }
    }

    static void BuildBioZone(Transform parent, string srcName, Bounds zb, ZoneSpec s, float surfaceY)
    {
        var zone = new GameObject($"Zone_{s.label}");
        zone.transform.SetParent(parent, true);
        zone.transform.position = new Vector3(zb.center.x, zb.center.y, zb.center.z);
        zone.transform.rotation = Quaternion.identity;
        zone.transform.localScale = Vector3.one;

        float floorY  = zb.min.y + 0.05f;
        float rise    = Mathf.Max(0.8f, surfaceY - floorY);
        float midY    = (floorY + surfaceY) * 0.5f;

        // Murk volume: a double-sided brown box filling the water body. When the camera dives in
        // it's enveloped in murky sludge tint (the reliable "underwater" look); from above it
        // deepens the surface colour. Bubbles/motes render brightly against it.
        var murk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        murk.name = "MurkVolume";
        var mcol = murk.GetComponent<Collider>(); if (mcol != null) Object.DestroyImmediate(mcol);
        murk.transform.SetParent(zone.transform, false);
        murk.transform.position = new Vector3(zb.center.x, (floorY + surfaceY) * 0.5f, zb.center.z);
        murk.transform.localScale = new Vector3(zb.size.x * 0.95f, Mathf.Max(0.4f, surfaceY - floorY - 0.1f), zb.size.z * 0.95f);
        murk.GetComponent<MeshRenderer>().sharedMaterial = MakeMurkMaterial();

        BuildDiffuserGrid(zone.transform, zb, floorY);

        if (s.bubbleRate > 0.5f)
            // Emit from a TIGHT band along the camera's dive path so the bubbles form a dense,
            // readable curtain (the whole 36×54 m floor would spread them invisibly thin).
            BuildBubbleEmitter(zone.transform, "Aeration", new Vector3(zb.center.x, floorY + 0.08f, zb.center.z),
                               zb.size.x * 0.18f, zb.size.z * 0.6f, rise, s.bubbleRate, s.swirl);

        if (s.sludgeRate > 0.5f)
            BuildSludgeDrift(zone.transform, "SludgeClouds", new Vector3(zb.center.x, midY, zb.center.z),
                             zb.size.x * 0.9f, rise * 0.95f, zb.size.z * 0.9f, s.sludgeRate, s.swirl);

        if (s.micro > 0.5f)
            BuildMicroParticles(zone.transform, "MicroLife", new Vector3(zb.center.x, midY, zb.center.z),
                                zb.size.x * 0.8f, rise * 0.8f, zb.size.z * 0.8f, s.micro);

        if (s.lightIntensity > 0.01f)
        {
            var lgo = new GameObject("ZoneLight");
            lgo.transform.SetParent(zone.transform, false);
            lgo.transform.position = new Vector3(zb.center.x, surfaceY - 0.3f, zb.center.z);
            var light = lgo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = s.lightColor;
            light.intensity = s.lightIntensity;
            light.range = Mathf.Max(zb.size.x, zb.size.z) * 0.55f;
            light.shadows = LightShadows.None;
        }
    }

    /// <summary>Industrial floor diffuser system: a header pipe + lateral aeration pipes.</summary>
    static void BuildDiffuserGrid(Transform parent, Bounds zb, float floorY)
    {
        var root = new GameObject("Diffusers");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(zb.center.x, floorY + 0.06f, zb.center.z);

        var mat = MakeDiffuserMaterial();
        float laneLen = zb.size.z * 0.82f;
        int lanes = 5;
        float span = zb.size.x * 0.74f;
        for (int i = 0; i < lanes; i++)
        {
            float fx = lanes == 1 ? 0f : Mathf.Lerp(-span * 0.5f, span * 0.5f, (float)i / (lanes - 1));
            // lateral pipe running along Z
            var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = $"DiffuserPipe_{i}";
            var c = pipe.GetComponent<Collider>(); if (c != null) Object.DestroyImmediate(c);
            pipe.transform.SetParent(root.transform, false);
            pipe.transform.localPosition = new Vector3(fx, 0f, 0f);
            pipe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);    // lay along Z
            pipe.transform.localScale = new Vector3(0.16f, laneLen * 0.5f, 0.16f);
            pipe.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        // header pipe across X joining the laterals
        var header = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        header.name = "DiffuserHeader";
        var hc = header.GetComponent<Collider>(); if (hc != null) Object.DestroyImmediate(hc);
        header.transform.SetParent(root.transform, false);
        header.transform.localPosition = new Vector3(0f, 0f, -laneLen * 0.5f);
        header.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);      // lay along X
        header.transform.localScale = new Vector3(0.22f, span * 0.6f, 0.22f);
        header.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void BuildBubbleEmitter(Transform parent, string name, Vector3 worldPos,
                                   float areaX, float areaZ, float rise, float rate, float swirl)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Confine bubbles to the water column: they rise from the floor and DIE at the surface
        // (lifetime × riseSpeed ≈ water depth). No gravity, no scattered start speed — so they
        // stop shooting metres up into the sky.
        const float riseSpeed = 2.3f;                                    // brisk rise → clear upward motion
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(rise / riseSpeed * 0.85f, rise / riseSpeed * 1.1f);
        main.startSpeed = 0f;                                            // velocity comes from velocityOverLifetime only
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.5f);    // small, DISCRETE bubbles (not overlapping blobs/haze)
        main.startColor = new Color(0.94f, 0.97f, 1f, 0.95f);            // bright, near-opaque bubbles
        main.maxParticles = 4000;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission; emission.rateOverTime = rate;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(areaX, 0.05f, areaZ);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        // All three linear-velocity curves must share the same MinMaxCurve mode (TwoConstants).
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(riseSpeed * 0.9f, riseSpeed * 1.15f);  // steady rise
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(swirl * 0.012f);    // swirl as they rise (zone-scaled)

        var noise = ps.noise;                                            // streams wander → natural, not perfect columns
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.3f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.4f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.9f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        // Alpha-blended bright white (NOT additive) so bubbles stay visible against bright water
        // AND dark murk alike — additive bubbles wash out against light backgrounds.
        psr.sharedMaterial = MakeParticleMaterial(new Color(0.96f, 0.98f, 1f, 0.85f), additive: false);
        psr.sortingOrder = 2;
    }

    static void BuildSludgeDrift(Transform parent, string name, Vector3 worldCenter,
                                 float areaX, float areaY, float areaZ, float rate, float swirl)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldCenter;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
        main.startSpeed = 0.05f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.0f, 2.6f);       // sized to the shallow basin; murk builds by overlap
        main.startColor = new Color(0.40f, 0.30f, 0.18f, 0.22f);          // low per-particle alpha; many overlap into murk
        main.maxParticles = 2000;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.283f);  // random spin so clouds don't look identical

        var emission = ps.emission; emission.rateOverTime = rate;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(areaX, areaY, areaZ);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.orbitalY = new ParticleSystem.MinMaxCurve(swirl * 0.02f);     // slow circular current
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.1f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = ps.noise;                                            // subtle "alive" biological churn
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.18f;                                          // slow, rhythmic — not jittery
        noise.scrollSpeed = 0.15f;
        noise.damping = true;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.40f, 0.30f, 0.18f), 0f), new GradientColorKey(new Color(0.34f, 0.25f, 0.14f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.25f), new GradientAlphaKey(0.3f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sharedMaterial = MakeParticleMaterial(new Color(0.40f, 0.30f, 0.18f, 0.4f), additive: false);
        psr.sortingOrder = 1;
    }

    static void BuildMicroParticles(Transform parent, string name, Vector3 worldCenter,
                                    float areaX, float areaY, float areaZ, float rate)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldCenter;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSpeed = 0.15f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);    // microscopic motes
        main.startColor = new Color(0.85f, 0.82f, 0.6f, 0.5f);
        main.maxParticles = 800;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = ps.emission; emission.rateOverTime = rate;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(areaX, areaY, areaZ);

        var noise = ps.noise;                                            // brownian-ish drifting "life"
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.2f;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sharedMaterial = MakeParticleMaterial(new Color(0.9f, 0.86f, 0.62f, 0.55f), additive: true);
        psr.sortingOrder = 3;
    }

    /// <summary>
    /// A DENSE, visible cloud of activated-sludge flocs (the brown bacterial colonies) suspended
    /// in a sphere — placed on the camera's underwater dive path so the viewer is surrounded by
    /// living biomass that churns among the oxygen bubbles. This is the biological process made
    /// readable: not cartoon bacteria, but the real brown floc matter a reactor is full of.
    /// </summary>
    static void BuildActivatedSludgeFlocs(Transform parent, string name, Vector3 worldCenter, float radius)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldCenter;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = 0.08f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.65f);     // visible brown clumps
        main.startColor = new Color(0.55f, 0.42f, 0.25f, 0.78f);         // lighter tan floc — pops against the dark murk
        main.maxParticles = 2200;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;        // stay put as the camera flies through
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.283f);

        var emission = ps.emission; emission.rateOverTime = 230f;          // dense biomass
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;

        var vel = ps.velocityOverLifetime;                                // gentle suspended drift
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.06f, 0.12f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = ps.noise;                                             // churning, alive
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.2f;
        noise.damping = true;

        var col = ps.colorOverLifetime;                                   // fade in/out (no popping)
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(   // white color keys → only alpha animates; the tan startColor defines the hue (no triple-darkening)
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.82f, 0.2f), new GradientAlphaKey(0.74f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sharedMaterial = MakeParticleMaterial(Color.white, additive: false);   // white tint → particle startColor is the true colour
        psr.sortingOrder = 2;
    }

    /// <summary>
    /// Builds a dense activated-sludge floc cloud and attaches an UnderwaterBiologyFollower so it
    /// rides the cutscene camera and only emits while submerged. Guarantees the camera is wrapped
    /// in visible biomass during the underwater leg regardless of the tank-wall geometry.
    /// </summary>
    static void BuildCameraBiologyCloud(GameObject go, Transform cameraTransform, float surfaceY)
    {
        // The cloud STAYS parented to WWTP_Tour (world scale 1). The follower moves it onto the
        // camera every frame (followPosition) and gates emission to underwater. Materials use
        // renderOnTop (ZTest Always) so the flocs + bubbles draw OVER the tank walls and can never be
        // occluded when the camera is inside a wall-bounded cell — which is what made the underwater
        // leg look empty. (Parenting to the camera was wrong: the camera's 0.03 prefab scale shrank
        // every particle ~33x into an invisible speck.)
        go.transform.localScale = Vector3.one;

        // ---- Brown activated-sludge flocs surrounding the lens ----
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startColor = new Color(0.62f, 0.48f, 0.30f, 0.85f);        // tan floc clumps
        main.maxParticles = 2500;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;     // world drift → parallax/motion as the camera moves
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.283f);

        var emission = ps.emission; emission.rateOverTime = 300f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 4f;   // wrap the lens

        var vel = ps.velocityOverLifetime;
        vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.12f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        var noise = ps.noise;
        noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.3f; noise.scrollSpeed = 0.3f; noise.damping = true;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.18f), new GradientAlphaKey(0.85f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sharedMaterial = MakeParticleMaterial(Color.white, additive: false, renderOnTop: true);
        psr.sortingOrder = 5;

        // ---- Bright oxygen bubbles rising (world up) through the view ----
        var bubGO = new GameObject("LensBubbles");
        bubGO.transform.SetParent(go.transform, false);
        var bps = bubGO.AddComponent<ParticleSystem>();
        bps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var bmain = bps.main;
        bmain.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.8f);
        bmain.startSpeed = 0f;
        bmain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.34f);
        bmain.startColor = new Color(0.96f, 0.99f, 1f, 0.95f);
        bmain.maxParticles = 2000;
        bmain.gravityModifier = 0f;
        bmain.simulationSpace = ParticleSystemSimulationSpace.World;
        var bem = bps.emission; bem.rateOverTime = 200f;
        var bshape = bps.shape; bshape.shapeType = ParticleSystemShapeType.Sphere; bshape.radius = 3.5f;
        var bvel = bps.velocityOverLifetime;
        bvel.enabled = true; bvel.space = ParticleSystemSimulationSpace.World;
        bvel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        bvel.y = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);           // rise in WORLD up
        bvel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var bnoise = bps.noise; bnoise.enabled = true; bnoise.strength = 0.4f; bnoise.frequency = 0.5f; bnoise.scrollSpeed = 0.3f;
        var bpsr = bubGO.GetComponent<ParticleSystemRenderer>();
        bpsr.renderMode = ParticleSystemRenderMode.Billboard;
        bpsr.sharedMaterial = MakeParticleMaterial(new Color(0.96f, 0.99f, 1f, 0.95f), additive: false, renderOnTop: true);
        bpsr.sortingOrder = 6;

        // Follower: rides the camera (followPosition) + gates BOTH systems' emission to underwater
        // only, so the cloud appears the instant the camera dives and clears when it surfaces.
        var followerType = System.Type.GetType("UnderwaterBiologyFollower, Assembly-CSharp");
        if (followerType != null)
        {
            var f = go.AddComponent(followerType);
            var soF = new SerializedObject(f);
            var pt = soF.FindProperty("target");         if (pt != null) pt.objectReferenceValue = cameraTransform;
            var py = soF.FindProperty("surfaceY");       if (py != null) py.floatValue = surfaceY;
            var pf = soF.FindProperty("followPosition"); if (pf != null) pf.boolValue = true;
            soF.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[WWTPTourBuild] UnderwaterBiologyFollower not found — camera biology won't ride the camera.");
        }
    }

    /// <summary>
    /// Disables every particle system that shipped INSIDE the SIM 4 cutscene group (the package's
    /// crude flat "solids/wet-wipe" sprite emitters, including the one parented to the camera that
    /// sprays squares into the sky). They're replaced by the realistic activated-sludge sim under
    /// WWTP_Tour, which is unaffected because it lives outside the cutscene group.
    /// </summary>
    static void DisablePackageSim4Particles(GameObject cutsceneRoot)
    {
        var bioGroup = FindDescendantByName(cutsceneRoot.transform, "Biological Treatment stage 4 cutscene");
        if (bioGroup == null) return;
        int disabled = 0;
        foreach (var ps in bioGroup.GetComponentsInChildren<ParticleSystem>(includeInactive: true))
        {
            if (ps.gameObject.activeSelf) { ps.gameObject.SetActive(false); disabled++; }
        }
        Debug.Log($"[WWTPTourBuild] Disabled {disabled} package particle system(s) in SIM 4 (replaced by realistic sim).");
    }

    /// <summary>
    /// Choreographs the full cinematic SIM 4 flythrough: an establishing shot above the basin,
    /// a low pass over each oxygen zone (Anaerobic → Anoxic → Aerobic), a dive into the aerobic
    /// bubble field, and a rise back out toward Stage 5 — with per-zone narration + paced pauses.
    /// Re-applied every build because the prefab is re-instantiated fresh.
    /// </summary>
    static void ChoreographSim4Cutscene(GameObject cutsceneRoot, Transform tourRoot, float surfaceY)
    {
        var camMoveType = System.Type.GetType("CameraMove, Assembly-CSharp");
        if (camMoveType == null) return;
        var bioGroup = FindDescendantByName(cutsceneRoot.transform, "Biological Treatment stage 4 cutscene");
        if (bioGroup == null) return;
        var cm = bioGroup.GetComponentInChildren(camMoveType, includeInactive: true);
        if (cm == null) { Debug.LogWarning("[WWTPTourBuild] SIM 4 CameraMove not found — can't choreograph."); return; }

        // Three tank groups, sorted by world X → flow order Anaerobic → Anoxic → Aerobic.
        var groups = new List<Bounds>();
        foreach (var zn in new[] { "Biological Treatment", "Biological Treatment (1)", "Biological Treatment (2)" })
        {
            var g = FindDescendantByName(bioGroup, zn);
            if (g == null) continue;
            var gb = ComputeBioreactorTankBounds(g);
            if (gb.size != Vector3.zero) groups.Add(gb);
        }
        if (groups.Count == 0) { Debug.LogWarning("[WWTPTourBuild] No bioreactor tank groups found."); return; }
        groups.Sort((a, b) => a.center.x.CompareTo(b.center.x));
        Bounds ana = groups[0];
        Bounds aer = groups[groups.Count - 1];
        Bounds ano = groups[Mathf.Clamp(groups.Count / 2, 0, groups.Count - 1)];

        float cz     = aer.center.z;
        float floorY = Mathf.Min(ana.min.y, aer.min.y);

        // 13 world positions: [0] = startPoint, [1..12] = points[0..11].
        // KEY: CameraMove always does transform.LookAt(NEXT waypoint). To make the player LOOK DOWN
        // AT the bubbling basins (instead of out at the sky, which is what happened before), each
        // narration-pause waypoint is placed LOW, just above the water of its zone, and is APPROACHED
        // FROM ABOVE — so on arrival the camera is aimed steeply down into the rising bubbles. The
        // submerged dip (p6–p8) stays just under the surface so the camera doesn't bury itself in a
        // wall cell; the camera-attached biomass cloud (renderOnTop) wraps the lens through the dip.
        // STRATEGY (user request): "porpoise" rhythm — for each zone the camera PLUNGES into the
        // pool, narrates the process from inside (biomass cloud fills the lens), then RISES OUT for a
        // brief aerial/outside view above the NEXT pool, then PLUNGES straight back down into it.
        // Each aerial waypoint sits directly ABOVE the next pool so the plunge looks straight DOWN
        // into the water (not at the horizon), and the underwater beats carry the actual process.
        var path = new List<Vector3>
        {
            new Vector3(ana.center.x, surfaceY + 6f,   cz + 2f),  //  start — aerial above Anaerobic, aimed DOWN [intro]
            new Vector3(ana.center.x, surfaceY - 1.2f, cz - 1f),  //  0  PLUNGE into Anaerobic                   [PAUSE]
            new Vector3(ano.center.x, surfaceY + 5f,   cz),       //  1  rise OUT → aerial above Anoxic
            new Vector3(ano.center.x, surfaceY - 1.2f, cz - 1f),  //  2  PLUNGE into Anoxic                      [PAUSE]
            new Vector3(aer.center.x, surfaceY + 5f,   cz),       //  3  rise OUT → aerial above Aerobic
            new Vector3(aer.center.x, surfaceY - 1.2f, cz - 1f),  //  4  PLUNGE into Aerobic                     [PAUSE]
            new Vector3(aer.center.x, surfaceY - 1.5f, cz - 4f),  //  5  deeper into the biomass                 [PAUSE]
            new Vector3(aer.center.x, surfaceY - 1.2f, cz - 7f),  //  6  submerged — oxygen bubbles              [PAUSE]
            new Vector3(aer.center.x, surfaceY + 5f,   cz - 9f),  //  7  rise OUT above the basin
            new Vector3(ano.center.x, surfaceY + 5f,   cz - 13f), //  8  aerial pull-back toward Stage 5         [PAUSE]
            new Vector3(ano.center.x, surfaceY + 4f,   cz - 17f), //  9  glide on
            new Vector3(ana.center.x, surfaceY + 4f,   cz - 20f), // 10  final
            new Vector3(ana.center.x, surfaceY + 5f,   cz - 24f), // 11  final wide
        };
        // Keep every waypoint inside the water body (never below the floor).
        for (int i = 0; i < path.Count; i++)
            path[i] = new Vector3(path[i].x, Mathf.Max(path[i].y, floorY + 0.4f), path[i].z);

        var so = new SerializedObject(cm);
        var spProp = so.FindProperty("startPoint");
        if (spProp != null && spProp.objectReferenceValue is Transform spT)
        {
            spT.position = path[0];
            // Aim the establishing shot DOWN at the basin (not at the horizon/sky). CameraMove holds
            // startPoint.rotation during the intro pause, so without this the opening frame shows sky.
            Vector3 lookTarget = new Vector3(ana.center.x, surfaceY, cz);
            spT.rotation = Quaternion.LookRotation((lookTarget - path[0]).normalized, Vector3.up);
        }

        var pointsArr = so.FindProperty("points");
        int nPts = Mathf.Min(path.Count - 1, pointsArr.arraySize);
        for (int i = 0; i < nPts; i++)
        {
            var t = pointsArr.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (t != null) t.position = path[i + 1];
        }

        var speedProp = so.FindProperty("speed");
        if (speedProp != null) speedProp.floatValue = 5f;   // slower → the player lingers in the biology instead of blowing past it

        // Rebuild the narration/pause triggers to match the new choreography (POINT indices, where
        // point N = path[N+1]). Pauses land on the low look-down waypoints + the two submerged ones.
        var triggersArr = so.FindProperty("customTriggers");
        triggersArr.ClearArray();
        AddCutsceneTrigger(triggersArr, 0, 3.5f, "Stage 4 - Biological Reactor.\nWastewater from primary clarification enters the activated-sludge basin - diving in...");
        AddCutsceneTrigger(triggersArr, 1, 4.5f, "Zone 1 - Anaerobic.\nNo oxygen. Microbes begin removing phosphorus. Calm, dark, dense sludge.");
        AddCutsceneTrigger(triggersArr, 3, 4.5f, "Zone 2 - Anoxic.\nGentle mixing. Microbes convert nitrates into nitrogen gas.");
        AddCutsceneTrigger(triggersArr, 5, 4.5f, "Zone 3 - Aerobic.\nOxygen is injected through floor diffusers - bubbles everywhere.");
        AddCutsceneTrigger(triggersArr, 6, 5f,   "Inside the reactor: billions of microbes (activated sludge) feed on the pollutants.");
        AddCutsceneTrigger(triggersArr, 7, 5f,   "Oxygen bubbles keep the biomass alive and mixing. Pollutants + O2 + microbes -> cleaner water.");
        AddCutsceneTrigger(triggersArr, 9, 4f,   "Treated water flows on to Stage 5 - Secondary Clarification.");
        so.ApplyModifiedPropertiesWithoutUndo();

        // World-space aeration columns rising from the floor under the underwater leg (path[2..8]).
        // These rise only up to the surface (they die there) so nothing sprays into the air, and the
        // dense aerobic column reads through the murk during the submerged aerobic beat.
        var diveFx = new GameObject("DiveBubbles");
        diveFx.transform.SetParent(tourRoot, true);
        diveFx.transform.position = path[4];
        float riseToSurface = Mathf.Max(1f, surfaceY - (floorY + 0.1f));
        foreach (int idx in new[] { 1, 3, 5, 6, 7 })
        {
            Vector3 p = path[idx];
            BuildBubbleEmitter(diveFx.transform, $"DiveAeration_{idx}",
                               new Vector3(p.x, floorY + 0.1f, p.z),
                               areaX: 6f, areaZ: 6f, rise: riseToSurface, rate: 240f, swirl: 55f);
        }

        // The biological process at the lens: a dense floc + bubble cloud PARENTED-IN-WORLD that the
        // follower keeps ON the cutscene camera (renderOnTop) and emits only while submerged — so the
        // entire underwater narration is wrapped in churning brown biomass + rising oxygen bubbles
        // that draw OVER the tank walls and can never be occluded. This is the "process in front of
        // your eyes". (No separate static floc field — it used to float above the surface as brown
        // specks in the sky; the camera cloud covers the underwater biomass on its own.)
        var camCloud = new GameObject("DiveBiology_CameraCloud");
        camCloud.transform.SetParent(tourRoot, true);
        camCloud.transform.position = path[6];
        BuildCameraBiologyCloud(camCloud, cm.transform, surfaceY);

        // ===== THE critical fix =====
        // The SIM 4 cutscene camera renders ONLY its own layer (cullingMask = 1<<layer). Every FX we
        // build defaults to layer 0 (Default), so the cutscene camera NEVER drew the water, murk,
        // bubbles, flocs or the biomass cloud — the underwater leg looked empty even though all the
        // particles were emitting. Move every FX object onto the camera's layer so they render in
        // the cutscene. (They simply won't appear in the player's free-roam view, which is fine —
        // they're cutscene dressing.)
        int camLayer = cm.gameObject.layer;
        foreach (var n in new[] { "BioreactorProcess_FX", "DiveBubbles", "DiveBiology_CameraCloud", "DiveBiology_Static" })
        {
            var t = tourRoot.Find(n);
            if (t != null) SetLayerRecursive(t.gameObject, camLayer);
        }

        Debug.Log($"[WWTPTourBuild] SIM 4 choreographed: look-down passes over Anaerobic({ana.center.x:F0})/Anoxic({ano.center.x:F0})/Aerobic({aer.center.x:F0}) + shallow dip; FX moved to camera layer {camLayer}; speed 5.");
    }

    /// <summary>Sets <paramref name="layer"/> on a GameObject and all of its descendants.</summary>
    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }

    static void AddCutsceneTrigger(SerializedProperty triggersArr, int pointIndex, float waitTime, string text)
    {
        triggersArr.InsertArrayElementAtIndex(triggersArr.arraySize);
        var e = triggersArr.GetArrayElementAtIndex(triggersArr.arraySize - 1);
        var pLabel  = e.FindPropertyRelative("triggerLabel");
        var pIndex  = e.FindPropertyRelative("triggerAtPointIndex");
        var pTarget = e.FindPropertyRelative("targetObject");
        var pAction = e.FindPropertyRelative("action");
        var pWait   = e.FindPropertyRelative("waitTime");
        var pText   = e.FindPropertyRelative("explanationText");
        if (pLabel  != null) pLabel.stringValue           = $"Sim4 @ {pointIndex}";
        if (pIndex  != null) pIndex.intValue              = pointIndex;
        if (pTarget != null) pTarget.objectReferenceValue = null;
        if (pAction != null) pAction.enumValueIndex       = 0;
        if (pWait   != null) pWait.floatValue             = waitTime;
        if (pText   != null) pText.stringValue            = text;
    }

    /// <summary>Union bounds of the bioreactor tank-wall meshes (skips WaterBlock + particles).</summary>
    static Bounds ComputeBioreactorTankBounds(Transform bioGroup)
    {
        Bounds? acc = null;
        foreach (var r in bioGroup.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (r.name.IndexOf("WaterBlock", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (acc == null) acc = r.bounds;
            else { var bb = acc.Value; bb.Encapsulate(r.bounds); acc = bb; }
        }
        return acc ?? new Bounds(bioGroup.position, Vector3.zero);
    }

    // Activated-sludge water is an opaque muddy brown (see the real Beit Shean photos). Rendered
    // double-sided + semi-transparent so the camera can dip BELOW the surface and still read it
    // as murky water rather than a hard wall.
    static readonly Color COLOR_SLUDGE_BROWN = new(0.42f, 0.32f, 0.19f, 0.88f);

    static Material MakeWaterSurfaceMaterial()
    {
        // URP Lit, transparent + double-sided so it reads as murky sludge water from above AND
        // from below (when the cutscene camera descends into the basin).
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);       // 0 opaque, 1 transparent
        if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", 0f);         // alpha blend
        if (mat.HasProperty("_ZWrite"))  mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_Cull"))    mat.SetFloat("_Cull", 0f);          // double-sided
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.55f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", COLOR_SLUDGE_BROWN);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
    }

    // Murky brown sludge-water volume. Double-sided + transparent so the camera sees a brown
    // tint whether it's outside looking in or submerged inside it.
    static Material MakeMurkMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_ZWrite"))  mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_Cull"))    mat.SetFloat("_Cull", 0f);   // double-sided
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
        // Deep murky brown (alpha 0.55). When the camera dives in it's enveloped in dark sludge
        // water — which is what gives the bright oxygen bubbles and tan flocs the CONTRAST they need
        // to read as discrete, moving particles. At alpha 0.15 the underwater was a washed-out pale
        // haze where white bubbles vanished against a white background.
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.16f, 0.115f, 0.065f, 0.72f));
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 1;  // behind bubbles/sludge
        return mat;
    }

    // Dark industrial diffuser-pipe material (matte black plastic / cast iron look).
    static Material MakeDiffuserMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.13f, 0.13f, 0.14f));
        else mat.color = new Color(0.13f, 0.13f, 0.14f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);   // wet, worn metal sheen
        if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", 0.6f);      // heavier cast-iron feel
        return mat;
    }

    const string SOFT_PARTICLE_TEX_PATH = "Assets/SignImages/soft_particle.png";

    /// <summary>
    /// Ensures a soft radial-gradient circle PNG exists in the project (so particle materials
    /// reference a persistent asset, not an in-memory texture that's lost on scene reload), and
    /// returns it.
    /// </summary>
    static Texture2D EnsureSoftParticleTexture()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(SOFT_PARTICLE_TEX_PATH);
        if (existing == null)
        {
            const int sz = 128;
            var t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            Vector2 c = new Vector2(sz * 0.5f, sz * 0.5f);
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / (sz * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);   // smoothstep for a soft edge
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            string full = System.IO.Path.Combine(Application.dataPath, SOFT_PARTICLE_TEX_PATH.Substring("Assets/".Length));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
            System.IO.File.WriteAllBytes(full, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(SOFT_PARTICLE_TEX_PATH);
        }

        // ALWAYS enforce import settings (even if the asset already existed) — the default DXT1
        // compression has NO alpha channel, which makes the soft circle render as a hard square.
        // Uncompressed RGBA + FromInput alpha keeps the radial transparency intact.
        var importer = AssetImporter.GetAtPath(SOFT_PARTICLE_TEX_PATH) as TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Default)                  { importer.textureType = TextureImporterType.Default; changed = true; }
            if (importer.alphaSource != TextureImporterAlphaSource.FromInput)          { importer.alphaSource = TextureImporterAlphaSource.FromInput; changed = true; }
            if (!importer.alphaIsTransparency)                                         { importer.alphaIsTransparency = true; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed){ importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (importer.mipmapEnabled)                                                { importer.mipmapEnabled = false; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp)                            { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (changed) importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(SOFT_PARTICLE_TEX_PATH);
    }

    /// <summary>Soft-billboard particle material. additive=true glows (bubbles/motes), false = alpha (sludge).</summary>
    static Material MakeParticleMaterial(Color tint, bool additive, bool renderOnTop = false)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        var tex = EnsureSoftParticleTexture();
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        else mat.mainTexture = tex;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        else mat.color = tint;
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);                 // transparent
        if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", additive ? 2f : 0f);   // 2=Additive, 0=Alpha
        if (mat.HasProperty("_ZWrite"))  mat.SetFloat("_ZWrite", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        // renderOnTop: ignore the depth buffer so these particles draw OVER the tank walls. Used by
        // the camera-attached biomass cloud so the dense flocs/bubbles in front of the lens are NEVER
        // occluded when the cutscene camera dives into a wall-bounded cell of the real plant model.
        if (renderOnTop)
        {
            if (mat.HasProperty("_ZTest")) mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;   // draw last, above scene geometry
        }
        return mat;
    }

    /// <summary>Depth-first search for the first descendant whose name matches exactly.</summary>
    static Transform FindDescendantByName(Transform root, string targetName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == targetName) return t;
        return null;
    }

    static void BuildPole(Transform parent, string name, Vector3 localPos, float length)
    {
        // Unity Cylinder primitive is 2m tall on Y axis by default -divide localScale.y by 2.
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = name;
        var col = pole.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        pole.transform.SetParent(parent, false);
        pole.transform.localPosition = localPos;
        pole.transform.localRotation = Quaternion.identity;
        pole.transform.localScale = new Vector3(POLE_RADIUS * 2f, length * 0.5f, POLE_RADIUS * 2f);
        AssignSolidColor(pole, COLOR_POLE_WHITE);
    }

    static void BuildPlayButton(Transform parent, string name, Vector3 localPos, string label,
                                Object cutsceneManager, string methodName, int intArg, bool isPlayAll)
    {
        // World-space Canvas hosting a UnityUI Button.
        var btnRoot = new GameObject(name);
        btnRoot.transform.SetParent(parent, false);
        btnRoot.transform.localPosition = localPos;

        var canvasGO = new GameObject("Canvas");
        canvasGO.transform.SetParent(btnRoot.transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = PLAY_BUTTON_SIZE;
        canvasGO.AddComponent<GraphicRaycaster>();
        // Add the XR variant too so hand interactors register the click in VR
        var trgrType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
        if (trgrType != null) canvasGO.AddComponent(trgrType);

        var btnGO = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(canvasGO.transform, false);
        var btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = Vector2.zero; btnRT.anchorMax = Vector2.one;
        btnRT.offsetMin = Vector2.zero; btnRT.offsetMax = Vector2.zero;
        var img = btnGO.GetComponent<Image>();
        var btn = btnGO.GetComponent<Button>();

        // Prefer the DESIGNED pill sprite (play glyph + label baked in). Falls back to a flat
        // colour + runtime text label if the sprite asset isn't present.
        var designed = EnsureButtonSprite(isPlayAll ? BTN_PLAYALL_PATH : BTN_PLAY_PATH);
        if (designed != null)
        {
            img.sprite = designed;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
        }
        else
        {
            img.color = isPlayAll ? COLOR_WARNING_AMBER : COLOR_BUTTON_GREEN;
            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(btnGO.transform, false);
            var lblRT = lblGO.AddComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
            var lblTmp = lblGO.AddComponent<TextMeshProUGUI>();
            lblTmp.text = label;
            lblTmp.font = LoadTMPFont();
            lblTmp.fontSize = PLAY_BUTTON_SIZE.y * 0.45f;
            lblTmp.fontStyle = FontStyles.Bold;
            lblTmp.color = Color.white;
            lblTmp.alignment = TextAlignmentOptions.Center;
        }

        // Wire persistent listener -survives serialization (unlike runtime AddListener).
        // Button.onClick is a parameterless UnityEvent, so we use the typed overloads
        // (AddPersistentListener for no-arg, AddIntPersistentListener for int-arg).
        if (cutsceneManager != null)
        {
            if (isPlayAll)
            {
                var act = System.Delegate.CreateDelegate(typeof(UnityAction), cutsceneManager, methodName, false, false) as UnityAction;
                if (act != null) UnityEventTools.AddPersistentListener(btn.onClick, act);
            }
            else
            {
                var act = System.Delegate.CreateDelegate(typeof(UnityAction<int>), cutsceneManager, methodName, false, false) as UnityAction<int>;
                if (act != null) UnityEventTools.AddIntPersistentListener(btn.onClick, act, intArg);
            }
        }
    }

    // ===================== Helpers =====================

    static Transform NewQuad(Transform parent, string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    static void AssignSolidColor(GameObject go, Color color)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
        r.sharedMaterial = mat;
    }

    /// <summary>
    /// Applies a designed PNG poster as the unlit base map of the GameObject's MeshRenderer.
    /// Texture is loaded from the AssetDatabase by path; falls back to a solid placeholder colour
    /// if the PNG is missing so the build doesn't crash mid-rebuild.
    /// </summary>
    static void AssignTexture(GameObject go, string pngPath)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
        var mat = new Material(shader);
        if (tex == null)
        {
            Debug.LogWarning($"[WWTPTourBuild] Sign texture not found: '{pngPath}'. Falling back to dark placeholder.");
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", COLOR_PLATE_DARK);
            else mat.color = COLOR_PLATE_DARK;
        }
        else
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            else mat.mainTexture = tex;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            else mat.color = Color.white;
        }
        r.sharedMaterial = mat;
    }

    /// <summary>
    /// Returns the union of all enabled MeshRenderer/SkinnedMeshRenderer bounds under a root,
    /// in world space. Used to find the digital twin's actual footprint so signs and the
    /// teleport target are placed relative to real geometry rather than the prefab's pivot.
    /// </summary>
    static Bounds ComputeRendererBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: false);
        if (renderers == null || renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        // Skip particle systems and trail renderers -they bias the bounds wildly.
        Bounds? acc = null;
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            // Also skip canvas / UI elements
            if (r.GetComponentInParent<Canvas>() != null) continue;
            if (!r.enabled) continue;
            if (acc == null) acc = r.bounds;
            else { var b = acc.Value; b.Encapsulate(r.bounds); acc = b; }
        }
        return acc ?? new Bounds(root.transform.position, Vector3.one);
    }

    static TMP_FontAsset LoadTMPFont()
    {
        var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TMP_FONT_PATH);
        if (f == null) f = TMP_Settings.defaultFontAsset;
        return f;
    }

    static Camera FindXRMainCamera()
    {
        var byName = GameObject.Find("Main Camera");
        if (byName != null && byName.TryGetComponent<Camera>(out var c)) return c;
        // Fallback: any active Camera tagged MainCamera
        var tagged = GameObject.FindGameObjectsWithTag("MainCamera");
        foreach (var t in tagged) if (t.TryGetComponent<Camera>(out var cc)) return cc;
        return Camera.main;
    }

    static void SetObj(SerializedObject so, string fieldName, Object value)
    {
        var prop = so.FindProperty(fieldName);
        if (prop != null) prop.objectReferenceValue = value;
        else Debug.LogWarning($"[WWTPTourBuild] SerializedProperty '{fieldName}' not found.");
    }

    static void DestroyByName(string name)
    {
        var existing = GameObject.Find(name);
        if (existing == null) return;
        ClearSelectionIfWithin(existing.transform);
        Object.DestroyImmediate(existing);
        Debug.Log($"[WWTPTourBuild] Removed existing '{name}' for clean rebuild.");
    }

    static void ClearSelectionIfWithin(Transform root)
    {
        var sel = Selection.activeGameObject;
        if (sel == null) return;
        var t = sel.transform;
        while (t != null)
        {
            if (t == root) { Selection.activeGameObject = null; return; }
            t = t.parent;
        }
    }
}
