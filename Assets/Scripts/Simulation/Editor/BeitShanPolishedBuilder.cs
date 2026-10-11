using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using XRMultiplayer;

/// <summary>
/// One-shot builder for the polished Beit Shan simulation layout:
/// - Roads / walking paths between stations
/// - Viewing platform with teleport pad at entrance
/// - Chlorine Contact Pool + Sludge Treatment Building primitives
/// - 6 Highway Signs (1 per station + 1 entrance) — large, readable from far
/// - 5 Detail Panels (1 per station) near tour entry — interactive Activate + Stop
/// - 1 Master Intro Panel at the entrance
/// - Updates WastewaterPlantManager.stations with English content
///
/// Idempotent — running it twice produces the same result (delete + recreate).
/// </summary>
public static class BeitShanPolishedBuilder
{
    // ============ Layout (world coordinates) ============
    // Plant root sits at world (300, 0, 0). Ground plate spans roughly (-160..1840, _, -970..1030).

    static readonly Vector3 ENTRANCE_LANDING = new(230, 1.7f, -10);
    static readonly Vector3 PLATFORM_TOP     = new(220, 12,   0);
    static readonly Vector3 CHLORINE_POOL    = new(1700, -1, -300);
    static readonly Vector3 SLUDGE_BUILDING  = new(50,   4,   200);

    // Highway signs sit along the road, oriented toward the road's direction.
    // Position is the sign's world position; rotation is its world Y-rotation in degrees.
    static readonly (Vector3 pos, float yaw)[] HIGHWAY_STATIONS = new (Vector3, float)[]
    {
        (new Vector3( 280,   6,    -10),  90),   // Stage A — facing east toward intake
        (new Vector3( 830,   6,    -10),  90),   // Stage B (Biological Treatment branch)
        (new Vector3(1230,   6,     50),  90),   // Stage C — Secondary Clarification
        (new Vector3(1590,   6,   -400), 180),   // Stage D — Disinfection
        (new Vector3(  60,   6,    140),   0),   // Stage E — Sludge Treatment (entrance side)
    };

    // Detail panels: positioned near the first tour waypoint of each station, eye height.
    static readonly Vector3[] DETAIL_PANELS = new[]
    {
        new Vector3( 280, 2.2f, -10),
        new Vector3( 830, 2.2f, -340),
        new Vector3(1230, 2.2f,  90),
        new Vector3(1590, 2.2f, -600),
        new Vector3(  60, 2.2f, 200),
    };

    // Master intro sits in front of the entrance, slightly to the side of the platform.
    static readonly Vector3 MASTER_INTRO_POS = new(232, 2.2f, -25);
    static readonly Vector3 MASTER_HIGHWAY_POS = new(220, 7, -20);

    // Road segments — list of (start, end, width) in world coords.
    // Each segment becomes a flattened Cube parented to the Roads group.
    static readonly (Vector3 a, Vector3 b, float width)[] ROAD_SEGMENTS = new (Vector3, Vector3, float)[]
    {
        (new Vector3( 230, 0.02f, -10), new Vector3( 300, 0.02f, -10), 6),  // Entrance → intake area
        (new Vector3( 300, 0.02f, -10), new Vector3( 800, 0.02f, -10), 6),  // East-west main
        (new Vector3( 800, 0.02f, -10), new Vector3( 800, 0.02f, -350), 6), // South to Stage B
        (new Vector3( 800, 0.02f, -10), new Vector3(1230, 0.02f, -10), 6),  // East main
        (new Vector3(1230, 0.02f, -10), new Vector3(1230, 0.02f, 100), 6),  // North to Stage C
        (new Vector3(1230, 0.02f, -10), new Vector3(1590, 0.02f, -10), 6),  // East main
        (new Vector3(1590, 0.02f, -10), new Vector3(1590, 0.02f, -610), 6), // South to Stage D
        (new Vector3(1590, 0.02f, -300),new Vector3(1700, 0.02f, -300), 6), // East spur to Chlorine pool
        (new Vector3( 230, 0.02f, -10), new Vector3(  60, 0.02f, -10), 6),  // West to Stage E
        (new Vector3(  60, 0.02f, -10), new Vector3(  60, 0.02f, 200), 6),  // North to Stage E
        (new Vector3( 300, 0.02f, -10), new Vector3( 300, 0.02f, 360), 4),  // Branch to POOL
    };

    // ============ Asset paths ============
    const string MAT_FOLDER       = "Assets/Models/Asset/Pipes/Materials_Extracted";
    const string ROAD_MAT_PATH    = MAT_FOLDER + "/Road.mat";
    const string BUILDING_MAT_PATH= MAT_FOLDER + "/Building.mat";
    const string CHLORINE_MAT_PATH= MAT_FOLDER + "/ChlorineWater.mat";
    const string SIGN_BG_MAT_PATH = MAT_FOLDER + "/SignBackground.mat";

    [MenuItem("MCP/Beit Shan/Build Polished Layout")]
    public static void BuildPolishedLayout()
    {
        var area = GameObject.Find("BeitShanSimulationArea");
        if (area == null) { Debug.LogError("[BeitShanPolishedBuilder] BeitShanSimulationArea not found."); return; }

        var manager = Object.FindFirstObjectByType<WastewaterPlantManager>();
        if (manager == null) { Debug.LogError("[BeitShanPolishedBuilder] WastewaterPlantManager not found in scene."); return; }

        Debug.Log("[BeitShanPolishedBuilder] === Building polished layout ===");

        // 0. Ensure shared materials exist
        var roadMat     = EnsureUrpLitMaterial(ROAD_MAT_PATH,    new Color(0.32f, 0.32f, 0.32f), smoothness: 0.10f);
        var buildingMat = EnsureUrpLitMaterial(BUILDING_MAT_PATH,new Color(0.78f, 0.72f, 0.58f), smoothness: 0.15f);
        var chlorineMat = EnsureUrpLitMaterial(CHLORINE_MAT_PATH,new Color(0.15f, 0.45f, 0.55f), smoothness: 0.85f);
        var signBgMat   = EnsureUrpLitMaterial(SIGN_BG_MAT_PATH, BeitShanUITheme.HeaderBlueDeep, smoothness: 0.20f);

        // 1. Delete legacy panels created by the earlier (Phase 1) builder
        DeleteChildrenByPrefix(area.transform, "StationPanel_");
        DeleteChildByName(area.transform, "MasterIntroPanel");
        DeleteChildByName(area.transform, "Roads");
        DeleteChildByName(area.transform, "ViewingPlatform");
        DeleteChildByName(area.transform, "Chlorine_Contact_Pool");
        DeleteChildByName(area.transform, "Sludge_Building");
        DeleteChildrenByPrefix(area.transform, "HighwaySign_");
        DeleteChildrenByPrefix(area.transform, "DetailPanel_");

        // 2. Update WastewaterPlantManager.stations with English content
        UpdateManagerStationsEnglish(manager);

        // 3. Roads group
        var roads = new GameObject("Roads", typeof(Transform));
        roads.transform.SetParent(area.transform, false);
        for (int i = 0; i < ROAD_SEGMENTS.Length; i++)
        {
            var seg = ROAD_SEGMENTS[i];
            BuildRoadSegment(roads.transform, $"Road_{i:00}", seg.a, seg.b, seg.width, roadMat);
        }

        // 4. Viewing platform
        BuildViewingPlatform(area.transform, buildingMat);

        // 5. Chlorine pool primitive
        BuildPrimitive(area.transform, "Chlorine_Contact_Pool", PrimitiveType.Cube,
                       CHLORINE_POOL, Quaternion.identity, new Vector3(50, 3, 30), chlorineMat);

        // 6. Sludge building primitive
        var sludgeBld = BuildPrimitive(area.transform, "Sludge_Building", PrimitiveType.Cube,
                                       SLUDGE_BUILDING, Quaternion.identity, new Vector3(30, 8, 20), buildingMat);
        // Add a "Sludge Treatment" billboard on top of the building
        BuildHighwaySign(area.transform, "HighwaySign_Building_Label",
                         SLUDGE_BUILDING + new Vector3(0, 9, 0), 0,
                         "Sludge Treatment Building", "Centrifuge dewatering · composting", signBgMat);

        // 7. Highway signs — 1 per station, oriented along the path
        for (int i = 0; i < HIGHWAY_STATIONS.Length && i < manager.stations.Count; i++)
        {
            var info = HIGHWAY_STATIONS[i];
            var s = manager.stations[i];
            string subtitle = StageSubtitle(i);
            BuildHighwaySign(area.transform, $"HighwaySign_{i}",
                             info.pos, info.yaw,
                             !string.IsNullOrEmpty(s.nameEnglish) ? s.nameEnglish : s.nameHebrew,
                             subtitle, signBgMat);
        }

        // 8. Master intro highway sign at the entrance (large welcome)
        BuildHighwaySign(area.transform, "HighwaySign_Entrance",
                         MASTER_HIGHWAY_POS, 90,
                         "Beit Shean WWTP",
                         "Wastewater treatment · 18,000 m³/day · 5 stages", signBgMat);

        // 9. Detail panels — 1 per station, near the tour entry
        for (int i = 0; i < DETAIL_PANELS.Length && i < manager.stations.Count; i++)
        {
            BuildDetailPanel(area.transform, $"DetailPanel_{i}", DETAIL_PANELS[i], 0,
                             stationIndex: i, manager: manager, isMaster: false);
        }

        // 10. Master intro panel (Activate All button) near the entrance
        BuildDetailPanel(area.transform, "MasterIntroPanel", MASTER_INTRO_POS, 90,
                         stationIndex: -1, manager: manager, isMaster: true);

        // 11. Save
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[BeitShanPolishedBuilder] === Done. Save the scene to persist. ===");
    }

    // ==================== English content ====================

    static void UpdateManagerStationsEnglish(WastewaterPlantManager manager)
    {
        var so = new SerializedObject(manager);
        var prop = so.FindProperty("stations");
        if (prop == null || !prop.isArray) { Debug.LogWarning("Could not find stations array"); return; }

        // Make sure we have at least 5 entries
        while (prop.arraySize < 5) prop.InsertArrayElementAtIndex(prop.arraySize);

        var content = new (string name, string desc, string subtitle)[]
        {
            ("Stage A — Pre-Treatment & Primary Clarification",
             "Wastewater enters the plant and undergoes physical screening. pH and conductivity sensors guard the biology against industrial shocks. Mechanical screens remove debris up to 1.5 m long. Grit and sand removal protects downstream pumps. The primary clarifier physically separates settleable solids from raw influent.",
             "First stop · solids removal & primary settling"),
            ("Stage B — Biological Treatment (Activated Sludge)",
             "The heart of the process. Returned bacteria (RAS) and surplus biomass (WAS) cycle through three oxygen regimes. The anaerobic zone biologically removes phosphorus. The anoxic zone denitrifies nitrates into N₂. The aerobic zone breaks down organics (BOD) and oxidizes ammonia.",
             "Bacteria do the heavy lifting"),
            ("Stage C — Secondary Clarification & Online QC",
             "Mixed liquor settles in secondary clarifiers; clarified water flows on to tertiary treatment. An online turbidity sensor acts as gatekeeper — any exceedance alerts operators and halts effluent flow to prevent off-spec water from leaving the plant.",
             "Settle solids · turbidity gatekeeper"),
            ("Stage D — Tertiary Treatment & Disinfection",
             "Four gravity sand filters polish remaining suspended solids. Liquid chlorine is dosed into contact tanks for a 30–40 minute residence time, killing pathogens. The output meets Israeli (Inbar) regulations for unrestricted agricultural irrigation.",
             "Polish · chlorinate · irrigation-grade output"),
            ("Stage E — Sludge Treatment & Composting",
             "Combined primary and waste activated sludge is dewatered by a high-speed centrifuge using cationic polymer. Filtrate returns to the plant head. The cake (20–25% dry solids) is composted with green waste into agricultural-grade fertilizer.",
             "Dewater · compost · fertilizer"),
        };

        for (int i = 0; i < 5; i++)
        {
            var elem = prop.GetArrayElementAtIndex(i);
            var nameEn = elem.FindPropertyRelative("nameEnglish");
            var desc   = elem.FindPropertyRelative("description");
            if (nameEn != null) nameEn.stringValue = content[i].name;
            if (desc != null)   desc.stringValue   = content[i].desc;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[BeitShanPolishedBuilder] Updated 5 stations with English content.");
    }

    static string StageSubtitle(int idx)
    {
        return idx switch
        {
            0 => "First stop · solids removal & primary settling",
            1 => "Bacteria do the heavy lifting",
            2 => "Settle solids · turbidity gatekeeper",
            3 => "Polish · chlorinate · irrigation-grade output",
            4 => "Dewater · compost · fertilizer",
            _ => "",
        };
    }

    // ==================== Roads / primitives ====================

    static void BuildRoadSegment(Transform parent, string name, Vector3 a, Vector3 b, float width, Material mat)
    {
        Vector3 mid = (a + b) * 0.5f;
        float length = Vector3.Distance(a, b);
        if (length < 0.1f) return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<BoxCollider>()); // walkable, no collision
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.position = mid;
        Vector3 dir = (b - a).normalized;
        if (dir.sqrMagnitude > 0.001f)
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        // After LookRotation, local Z = forward = segment direction. Scale so Z=length, X=width, Y=thin slab.
        go.transform.localScale = new Vector3(width, 0.05f, length);
        ApplyMaterial(go, mat);
    }

    static GameObject BuildPrimitive(Transform parent, string name, PrimitiveType type,
                                     Vector3 worldPos, Quaternion worldRot, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.position = worldPos;
        go.transform.rotation = worldRot;
        go.transform.localScale = scale;
        ApplyMaterial(go, mat);
        return go;
    }

    static void ApplyMaterial(GameObject go, Material mat)
    {
        if (mat == null) return;
        var r = go.GetComponent<MeshRenderer>();
        if (r != null) r.sharedMaterial = mat;
    }

    // ==================== Viewing Platform ====================

    static void BuildViewingPlatform(Transform parent, Material buildingMat)
    {
        var root = new GameObject("ViewingPlatform", typeof(Transform));
        root.transform.SetParent(parent, false);
        root.transform.position = Vector3.zero;

        // Support column from ground to platform
        BuildPrimitive(root.transform, "Column", PrimitiveType.Cube,
                       new Vector3(PLATFORM_TOP.x, PLATFORM_TOP.y * 0.5f, PLATFORM_TOP.z),
                       Quaternion.identity, new Vector3(2.5f, PLATFORM_TOP.y, 2.5f), buildingMat);

        // Platform top (8m × 8m square)
        BuildPrimitive(root.transform, "PlatformTop", PrimitiveType.Cube,
                       PLATFORM_TOP, Quaternion.identity, new Vector3(8, 0.4f, 8), buildingMat);

        // Railings (thin cubes) around the perimeter
        float railY = PLATFORM_TOP.y + 0.7f;
        float halfX = 4f, halfZ = 4f;
        BuildPrimitive(root.transform, "Rail_N", PrimitiveType.Cube,
                       new Vector3(PLATFORM_TOP.x,         railY, PLATFORM_TOP.z + halfZ),
                       Quaternion.identity, new Vector3(8, 1.4f, 0.15f), buildingMat);
        BuildPrimitive(root.transform, "Rail_S", PrimitiveType.Cube,
                       new Vector3(PLATFORM_TOP.x,         railY, PLATFORM_TOP.z - halfZ),
                       Quaternion.identity, new Vector3(8, 1.4f, 0.15f), buildingMat);
        BuildPrimitive(root.transform, "Rail_E", PrimitiveType.Cube,
                       new Vector3(PLATFORM_TOP.x + halfX, railY, PLATFORM_TOP.z),
                       Quaternion.identity, new Vector3(0.15f, 1.4f, 8), buildingMat);
        BuildPrimitive(root.transform, "Rail_W", PrimitiveType.Cube,
                       new Vector3(PLATFORM_TOP.x - halfX, railY, PLATFORM_TOP.z),
                       Quaternion.identity, new Vector3(0.15f, 1.4f, 8), buildingMat);

        // Teleport pad on the GROUND (to go UP)
        var upPad = BuildTeleportPad(root.transform, "TeleportPad_Up",
                                     new Vector3(PLATFORM_TOP.x, 0.05f, PLATFORM_TOP.z + halfZ + 2f),
                                     Quaternion.identity, color: BeitShanUITheme.ButtonPrimary);
        WireTeleporter(upPad, PLATFORM_TOP + new Vector3(0, 0.21f, 0), Quaternion.identity, WorldArea.Simulation);

        // Teleport pad on the PLATFORM (to go DOWN to entrance landing)
        var downPad = BuildTeleportPad(root.transform, "TeleportPad_Down",
                                       new Vector3(PLATFORM_TOP.x, PLATFORM_TOP.y + 0.22f, PLATFORM_TOP.z),
                                       Quaternion.identity, color: BeitShanUITheme.ButtonDanger);
        WireTeleporter(downPad, ENTRANCE_LANDING, Quaternion.Euler(0, 90, 0), WorldArea.Simulation);
    }

    static GameObject BuildTeleportPad(Transform parent, string name, Vector3 pos, Quaternion rot, Color color)
    {
        // Glowing flat disc for teleport — use a cylinder very thin
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = name;
        Object.DestroyImmediate(pad.GetComponent<CapsuleCollider>());
        pad.transform.SetParent(parent, false);
        pad.transform.position = pos;
        pad.transform.rotation = rot;
        pad.transform.localScale = new Vector3(2.5f, 0.05f, 2.5f);
        var matPath = $"{MAT_FOLDER}/TeleportPad_{name}.mat";
        var padMat = EnsureUrpLitMaterial(matPath, color, smoothness: 0.7f, emissive: color * 0.5f);
        ApplyMaterial(pad, padMat);
        // Add a box trigger so player stepping onto the pad triggers the teleport (XRSimpleInteractable feels less natural for a floor pad — use simple trigger Collider + script).
        var col = pad.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.2f, 4f, 1.2f);   // tall, narrow on top of pad
        col.center = new Vector3(0, 2, 0);
        return pad;
    }

    static void WireTeleporter(GameObject pad, Vector3 destinationWorldPos, Quaternion destinationRot, WorldArea area)
    {
        // Create a child target Transform and use the existing DoorTeleporter script + a trigger script.
        var target = new GameObject("Target", typeof(Transform));
        target.transform.SetParent(pad.transform, false);
        target.transform.position = destinationWorldPos;
        target.transform.rotation = destinationRot;

        var tp = pad.AddComponent<DoorTeleporter>();
        tp.target = target.transform;
        tp.destinationArea = area;

        // Trigger handler — fires Teleport() on enter
        var trigger = pad.AddComponent<TeleportPadTrigger>();
        trigger.teleporter = tp;
    }

    // ==================== Highway Sign ====================

    static GameObject BuildHighwaySign(Transform parent, string name, Vector3 worldPos, float yaw,
                                       string title, string subtitle, Material signBgMat)
    {
        // 3D physical sign — a thin Cube backing + child world-space Canvas for text
        var root = new GameObject(name, typeof(Transform));
        root.transform.SetParent(parent, false);
        root.transform.position = worldPos;
        root.transform.rotation = Quaternion.Euler(0, yaw, 0);

        // Solid background plate
        var backing = BuildPrimitive(root.transform, "Backing", PrimitiveType.Cube,
                                     worldPos, Quaternion.Euler(0, yaw, 0),
                                     new Vector3(BeitShanUITheme.HIGHWAY_WIDTH_M,
                                                 BeitShanUITheme.HIGHWAY_HEIGHT_M, 0.15f), signBgMat);
        backing.transform.SetParent(root.transform, true);

        // Yellow border frame — 4 thin cubes
        Material borderMat = EnsureUrpLitMaterial(MAT_FOLDER + "/SignBorder.mat", BeitShanUITheme.BorderYellow, smoothness: 0.4f);
        float w = BeitShanUITheme.HIGHWAY_WIDTH_M, h = BeitShanUITheme.HIGHWAY_HEIGHT_M;
        float th = 0.12f;
        // Top
        BuildPrimitive(root.transform, "Border_T", PrimitiveType.Cube,
                       worldPos + Quaternion.Euler(0, yaw, 0) * new Vector3(0,  h * 0.5f, -0.08f),
                       Quaternion.Euler(0, yaw, 0), new Vector3(w + th, th, 0.05f), borderMat);
        BuildPrimitive(root.transform, "Border_B", PrimitiveType.Cube,
                       worldPos + Quaternion.Euler(0, yaw, 0) * new Vector3(0, -h * 0.5f, -0.08f),
                       Quaternion.Euler(0, yaw, 0), new Vector3(w + th, th, 0.05f), borderMat);
        BuildPrimitive(root.transform, "Border_L", PrimitiveType.Cube,
                       worldPos + Quaternion.Euler(0, yaw, 0) * new Vector3(-w * 0.5f, 0, -0.08f),
                       Quaternion.Euler(0, yaw, 0), new Vector3(th, h + th, 0.05f), borderMat);
        BuildPrimitive(root.transform, "Border_R", PrimitiveType.Cube,
                       worldPos + Quaternion.Euler(0, yaw, 0) * new Vector3( w * 0.5f, 0, -0.08f),
                       Quaternion.Euler(0, yaw, 0), new Vector3(th, h + th, 0.05f), borderMat);

        // World-space text canvas mounted in front of the plate
        var canvasGO = new GameObject("TextCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(root.transform, false);
        canvasGO.transform.localPosition = new Vector3(0, 0, -0.1f);  // slightly in front (sign yaw forward)
        canvasGO.transform.localRotation = Quaternion.identity;
        var rt = (RectTransform)canvasGO.transform;
        rt.sizeDelta = new Vector2(w * BeitShanUITheme.PIXEL_PER_METER, h * BeitShanUITheme.PIXEL_PER_METER);
        rt.localScale = Vector3.one * (1f / BeitShanUITheme.PIXEL_PER_METER);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 2;

        // Title
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(canvasGO.transform, false);
        var titleRT = (RectTransform)titleGO.transform;
        titleRT.anchorMin = new Vector2(0, 0.45f);
        titleRT.anchorMax = new Vector2(1, 1f);
        titleRT.offsetMin = new Vector2(60, 0);
        titleRT.offsetMax = new Vector2(-60, -40);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = title;
        titleTMP.fontSize = BeitShanUITheme.FONT_TITLE_HIGHWAY;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = BeitShanUITheme.TextLight;
        titleTMP.enableAutoSizing = true;
        titleTMP.fontSizeMin = 80;
        titleTMP.fontSizeMax = BeitShanUITheme.FONT_TITLE_HIGHWAY;

        // Subtitle
        var subGO = new GameObject("Subtitle", typeof(RectTransform));
        subGO.transform.SetParent(canvasGO.transform, false);
        var subRT = (RectTransform)subGO.transform;
        subRT.anchorMin = new Vector2(0, 0);
        subRT.anchorMax = new Vector2(1, 0.45f);
        subRT.offsetMin = new Vector2(60, 40);
        subRT.offsetMax = new Vector2(-60, 0);
        var subTMP = subGO.AddComponent<TextMeshProUGUI>();
        subTMP.text = subtitle;
        subTMP.fontSize = BeitShanUITheme.FONT_SUBTITLE_HIGHWAY;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color = BeitShanUITheme.TextAccent;
        subTMP.enableAutoSizing = true;
        subTMP.fontSizeMin = 40;
        subTMP.fontSizeMax = BeitShanUITheme.FONT_SUBTITLE_HIGHWAY;

        return root;
    }

    // ==================== Detail Panel ====================

    static GameObject BuildDetailPanel(Transform parent, string name, Vector3 worldPos, float yaw,
                                       int stationIndex, WastewaterPlantManager manager, bool isMaster)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 1;

        float w = isMaster ? BeitShanUITheme.MASTER_WIDTH_M  : BeitShanUITheme.DETAIL_WIDTH_M;
        float h = isMaster ? BeitShanUITheme.MASTER_HEIGHT_M : BeitShanUITheme.DETAIL_HEIGHT_M;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(w * BeitShanUITheme.PIXEL_PER_METER, h * BeitShanUITheme.PIXEL_PER_METER);
        rt.localScale = Vector3.one * (1f / BeitShanUITheme.PIXEL_PER_METER);

        // ===== Claude Design — cream surface, coral accent, dark brown text =====

        // Background (warm cream)
        var bg = AddImage(go.transform, "Background", BeitShanUITheme.SurfaceCream, raycast: false);
        FillRect((RectTransform)bg.transform);

        // Header strip (top 24%) — slightly deeper cream for subtle layering
        var header = AddImage(go.transform, "Header", BeitShanUITheme.SurfaceDeepCream, raycast: false);
        var hrt = (RectTransform)header.transform;
        hrt.anchorMin = new Vector2(0, 0.76f);
        hrt.anchorMax = new Vector2(1, 1f);
        hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;

        // Coral hairline under the header (Anthropic-style accent stroke)
        var stroke = AddImage(go.transform, "HeaderStroke", BeitShanUITheme.AccentCoral, raycast: false);
        var strt = (RectTransform)stroke.transform;
        strt.anchorMin = new Vector2(0, 0.76f);
        strt.anchorMax = new Vector2(1, 0.76f);
        strt.offsetMin = new Vector2(BeitShanUITheme.PADDING_DETAIL, -3);
        strt.offsetMax = new Vector2(-BeitShanUITheme.PADDING_DETAIL, 0);

        // Title text (in header, dark ink on cream, generous padding)
        var titleTMP = AddTMP(header.transform, "Title",
                              isMaster ? "Beit Shean WWTP" :
                              (!string.IsNullOrEmpty(manager.stations[stationIndex].nameEnglish)
                                ? manager.stations[stationIndex].nameEnglish
                                : manager.stations[stationIndex].nameHebrew),
                              isMaster ? BeitShanUITheme.FONT_TITLE_MASTER : BeitShanUITheme.FONT_TITLE_DETAIL,
                              FontStyles.Bold, TextAlignmentOptions.MidlineLeft, BeitShanUITheme.TextInk, autosize: true);
        FillRectInset((RectTransform)titleTMP.transform,
                      leftRightInset: BeitShanUITheme.PADDING_DETAIL, topBottomInset: 30);

        // Description body — dark brown on cream, generous breathing room
        var descTMP = AddTMP(go.transform, "Description",
                             isMaster
                              ? "Design capacity 18,000 m³/day. A five-stage process turns raw sewage into irrigation-grade reclaimed water and compost-quality biosolids. Press Activate All for a full guided tour."
                              : manager.stations[stationIndex].description,
                             isMaster ? BeitShanUITheme.FONT_BODY_LARGE : BeitShanUITheme.FONT_BODY,
                             FontStyles.Normal, TextAlignmentOptions.TopLeft, BeitShanUITheme.TextInk, autosize: true);
        var drt = (RectTransform)descTMP.transform;
        drt.anchorMin = new Vector2(0, 0.20f);
        drt.anchorMax = new Vector2(1, 0.76f);
        drt.offsetMin = new Vector2(BeitShanUITheme.PADDING_DETAIL, 20);
        drt.offsetMax = new Vector2(-BeitShanUITheme.PADDING_DETAIL, -40);
        descTMP.textWrappingMode = TextWrappingModes.Normal;
        descTMP.lineSpacing = 6;

        // Primary action — coral pill button, near full width, prominent
        var activateBtn = BuildButton(go.transform, "ActivateButton",
                                      isMaster ? "Activate All" : "Activate",
                                      BeitShanUITheme.AccentCoral, BeitShanUITheme.TextOnInk);
        var abrt = (RectTransform)activateBtn.transform;
        abrt.anchorMin = new Vector2(0, 0);
        abrt.anchorMax = new Vector2(1, 0);
        abrt.pivot = new Vector2(0.5f, 0);
        abrt.anchoredPosition = new Vector2(0, 40);
        abrt.offsetMin = new Vector2(BeitShanUITheme.PADDING_DETAIL, 40);
        abrt.offsetMax = new Vector2(-BeitShanUITheme.PADDING_DETAIL, 40 + 160);
        abrt.sizeDelta = new Vector2(abrt.sizeDelta.x, 160);

        // Secondary action — "Stop tour" as a small text link in the top-right corner of header
        var stopBtn = BuildButton(header.transform, "StopButton", "Stop tour",
                                  new Color(0,0,0,0), BeitShanUITheme.TextInkMuted);
        var sbrt = (RectTransform)stopBtn.transform;
        sbrt.anchorMin = new Vector2(1, 1);
        sbrt.anchorMax = new Vector2(1, 1);
        sbrt.pivot = new Vector2(1, 1);
        sbrt.anchoredPosition = new Vector2(-30, -25);
        sbrt.sizeDelta = new Vector2(280, 80);
        // Override the stop button's label color to muted brown
        var stopLabel = stopBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (stopLabel != null)
        {
            stopLabel.color = BeitShanUITheme.TextInkMuted;
            stopLabel.fontSize = BeitShanUITheme.FONT_LINK;
            stopLabel.fontStyle = FontStyles.Normal;
        }

        // Attach StationInfoPanel and wire references
        var sip = go.AddComponent<StationInfoPanel>();
        var so = new SerializedObject(sip);
        so.FindProperty("stationIndex").intValue = isMaster ? -1 : stationIndex;
        so.FindProperty("isMasterPanel").boolValue = isMaster;
        so.FindProperty("titleText").objectReferenceValue       = titleTMP;
        so.FindProperty("descriptionText").objectReferenceValue = descTMP;
        so.FindProperty("activateButton").objectReferenceValue  = activateBtn.GetComponent<Button>();
        so.FindProperty("stopButton").objectReferenceValue      = stopBtn.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    // ==================== UI helpers ====================

    static GameObject AddImage(Transform parent, string name, Color color, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return go;
    }

    static TextMeshProUGUI AddTMP(Transform parent, string name, string text, int fontSize,
                                  FontStyles style, TextAlignmentOptions align, Color color, bool autosize = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        if (autosize)
        {
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = Mathf.Max(30, fontSize / 3);
            tmp.fontSizeMax = fontSize;
        }
        return tmp;
    }

    static GameObject BuildButton(Transform parent, string name, string label, Color bg, Color textColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = bg;

        var lbl = AddTMP(go.transform, "Label", label, BeitShanUITheme.FONT_BUTTON,
                         FontStyles.Bold, TextAlignmentOptions.Center, textColor);
        FillRect((RectTransform)lbl.transform);
        return go;
    }

    static void FillRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void FillRectInset(RectTransform rt, float leftRightInset, float topBottomInset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2( leftRightInset,  topBottomInset);
        rt.offsetMax = new Vector2(-leftRightInset, -topBottomInset);
    }

    // ==================== Material / scene utilities ====================

    static Material EnsureUrpLitMaterial(string path, Color baseColor, float smoothness, Color? emissive = null)
    {
        if (!AssetDatabase.IsValidFolder(MAT_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/Models/Asset/Pipes", "Materials_Extracted");
        }
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("URP/Lit shader not found.");
            return null;
        }
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0);
        if (emissive.HasValue && mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", emissive.Value);
            mat.EnableKeyword("_EMISSION");
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void DeleteChildByName(Transform parent, string name)
    {
        var c = parent.Find(name);
        if (c != null) Object.DestroyImmediate(c.gameObject);
    }

    static void DeleteChildrenByPrefix(Transform parent, string prefix)
    {
        var toDelete = new List<GameObject>();
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name.StartsWith(prefix)) toDelete.Add(c.gameObject);
        }
        foreach (var go in toDelete) Object.DestroyImmediate(go);
    }
}
