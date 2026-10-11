using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds a real 2-story building for Stage 1, with:
/// - Concrete walls, floors, roof
/// - West / east wall openings for pipe entry/exit
/// - South wall with window strip + a wall opening for the waste pipe to the outside yellow skip
/// - Approach pipe corridor (3 brown industrial pipes + blue valves) running into the west wall
/// - Internal equipment moved to correct floors: LD screens + sand sedimentation pool on upper floor,
///   sand collection skip + waste chute on lower floor
/// - Yellow skip OUTSIDE the south wall, connected by a black waste pipe through the wall
/// </summary>
public static class BeitShanStage1Building
{
    const string MAT_FOLDER = "Assets/Models/Asset/Pipes/Materials_Extracted";

    // Building footprint
    static readonly Vector3 BLD_CENTER = new(260, 6, -10); // center of building volume
    const float BLD_W = 50;    // X extent
    const float BLD_H = 12;    // Y extent (total, 2 floors of 6m)
    const float BLD_D = 30;    // Z extent
    const float MID_Y = 6;     // mid floor height
    const float WALL_TH = 0.4f;

    [MenuItem("MCP/Beit Shan/Build Stage 1 Building")]
    public static void BuildBuilding()
    {
        var area = GameObject.Find("BeitShanSimulationArea");
        if (area == null) { Debug.LogError("BeitShanSimulationArea not found"); return; }

        // Clean previous run
        DeleteByName(area.transform, "Building_Stage1");
        DeleteByName(area.transform, "ApproachPipes");

        // Materials
        var concrete = EnsureMat($"{MAT_FOLDER}/Concrete.mat",       new Color(0.78f, 0.76f, 0.72f), smoothness: 0.2f);
        var concreteDark = EnsureMat($"{MAT_FOLDER}/ConcreteDark.mat", new Color(0.55f, 0.54f, 0.52f), smoothness: 0.2f);
        var metalRoof = EnsureMat($"{MAT_FOLDER}/MetalRoof.mat",     new Color(0.45f, 0.46f, 0.48f), smoothness: 0.7f, metallic: 0.6f);
        var brownPipe = EnsureMat($"{MAT_FOLDER}/BrownPipe.mat",     new Color(0.45f, 0.22f, 0.18f), smoothness: 0.4f, metallic: 0.3f);
        var blueValve = EnsureMat($"{MAT_FOLDER}/BlueValve.mat",     new Color(0.15f, 0.40f, 0.75f), smoothness: 0.55f, metallic: 0.4f);
        var blackPipe = EnsureMat($"{MAT_FOLDER}/BlackPipe.mat",     new Color(0.10f, 0.10f, 0.12f), smoothness: 0.5f, metallic: 0.4f);
        var yellow   = EnsureMat($"{MAT_FOLDER}/YellowSkip.mat",     new Color(0.95f, 0.78f, 0.18f), smoothness: 0.55f);
        var debris   = EnsureMat($"{MAT_FOLDER}/Debris.mat",         new Color(0.30f, 0.25f, 0.18f), smoothness: 0.1f);
        var brownWater = EnsureMat($"{MAT_FOLDER}/BrownWater.mat",   new Color(0.42f, 0.32f, 0.18f, 0.88f), smoothness: 0.85f);

        // ===== Building skeleton =====
        var bld = new GameObject("Building_Stage1");
        bld.transform.SetParent(area.transform, false);

        float minX = BLD_CENTER.x - BLD_W * 0.5f;
        float maxX = BLD_CENTER.x + BLD_W * 0.5f;
        float minZ = BLD_CENTER.z - BLD_D * 0.5f;
        float maxZ = BLD_CENTER.z + BLD_D * 0.5f;

        // Slab (lower floor)
        Cube(bld.transform, "Slab",      new Vector3(BLD_CENTER.x, 0,    BLD_CENTER.z), new Vector3(BLD_W, WALL_TH, BLD_D), concreteDark);
        // Mid-floor (with a hole over the sand collection skip)
        // Split into 4 strips so there's a 6×6 opening near center at (BLD_CENTER.x, MID_Y, BLD_CENTER.z)
        float midHoleX = 6, midHoleZ = 6;
        // North strip (z > hole)
        Cube(bld.transform, "MidFloor_N", new Vector3(BLD_CENTER.x, MID_Y, BLD_CENTER.z + midHoleZ*0.5f + (BLD_D - midHoleZ)*0.25f),
             new Vector3(BLD_W, WALL_TH, (BLD_D - midHoleZ)*0.5f), concrete);
        // South strip (z < hole)
        Cube(bld.transform, "MidFloor_S", new Vector3(BLD_CENTER.x, MID_Y, BLD_CENTER.z - midHoleZ*0.5f - (BLD_D - midHoleZ)*0.25f),
             new Vector3(BLD_W, WALL_TH, (BLD_D - midHoleZ)*0.5f), concrete);
        // West strip (x < hole)
        Cube(bld.transform, "MidFloor_W", new Vector3(BLD_CENTER.x - midHoleX*0.5f - (BLD_W - midHoleX)*0.25f, MID_Y, BLD_CENTER.z),
             new Vector3((BLD_W - midHoleX)*0.5f, WALL_TH, midHoleZ), concrete);
        // East strip
        Cube(bld.transform, "MidFloor_E", new Vector3(BLD_CENTER.x + midHoleX*0.5f + (BLD_W - midHoleX)*0.25f, MID_Y, BLD_CENTER.z),
             new Vector3((BLD_W - midHoleX)*0.5f, WALL_TH, midHoleZ), concrete);

        // Roof
        Cube(bld.transform, "Roof",       new Vector3(BLD_CENTER.x, BLD_H, BLD_CENTER.z), new Vector3(BLD_W, WALL_TH, BLD_D), metalRoof);

        // West wall — solid except for pipe-entry opening at Y=7, width 4, height 2
        BuildWallWithOpening(bld.transform, "WestWall",
            wallPosX: minX, wallPosZ: BLD_CENTER.z,
            wallSpanZ: BLD_D, wallH: BLD_H,
            openingCenterY: 7.5f, openingW: 5, openingH: 2.5f, isXFacing: true,
            mat: concrete);

        // East wall — pipe-exit opening at Y=7, width 4, height 2
        BuildWallWithOpening(bld.transform, "EastWall",
            wallPosX: maxX, wallPosZ: BLD_CENTER.z,
            wallSpanZ: BLD_D, wallH: BLD_H,
            openingCenterY: 7.5f, openingW: 5, openingH: 2.5f, isXFacing: true,
            mat: concrete);

        // North wall — solid
        Cube(bld.transform, "NorthWall", new Vector3(BLD_CENTER.x, BLD_H * 0.5f, maxZ),
             new Vector3(BLD_W, BLD_H, WALL_TH), concrete);

        // South wall — has a window strip at Y=9 and a circular waste pipe opening at Y=4
        BuildSouthWallSplit(bld.transform, BLD_CENTER, BLD_W, BLD_H, WALL_TH, minZ, concrete);

        // Operator catwalk on upper floor
        Cube(bld.transform, "Catwalk", new Vector3(BLD_CENTER.x, 10, BLD_CENTER.z + 8),
             new Vector3(40, 0.2f, 1.5f), concreteDark);

        // ===== Approach pipe corridor (outside, west of building) =====
        var pipes = new GameObject("ApproachPipes");
        pipes.transform.SetParent(area.transform, false);

        // 3 brown industrial pipes running east into the west-wall opening
        for (int i = 0; i < 3; i++)
        {
            float zOff = (i - 1) * 1.4f; // -1.4, 0, +1.4
            BuildHorizontalPipe(pipes.transform, $"Pipe_{i}",
                                start: new Vector3(180, 7.5f + (i % 2 == 0 ? 0 : 0.6f), -10 + zOff),
                                end:   new Vector3(minX - 1, 7.5f + (i % 2 == 0 ? 0 : 0.6f), -10 + zOff),
                                radius: 0.5f, mat: brownPipe);

            // Blue valve cubes at roughly 1/3 and 2/3 along each pipe
            for (int v = 1; v <= 2; v++)
            {
                float t = v / 3f;
                Vector3 valvePos = Vector3.Lerp(
                    new Vector3(180, 7.5f + (i % 2 == 0 ? 0 : 0.6f), -10 + zOff),
                    new Vector3(minX - 1, 7.5f + (i % 2 == 0 ? 0 : 0.6f), -10 + zOff), t);
                Cube(pipes.transform, $"Valve_{i}_{v}", valvePos, new Vector3(1.4f, 1.6f, 1.4f), blueValve);
            }
        }

        // Concrete pad under the pipe corridor
        Cube(pipes.transform, "PipePad",
             new Vector3((180 + minX) * 0.5f, 0.05f, -10),
             new Vector3(minX - 180 + 8, 0.1f, 8), concreteDark);

        // ===== Internal equipment =====

        // Sand collection skip (inside, lower floor, below the screens)
        Cube(bld.transform, "SandCollection_Skip",
             new Vector3(BLD_CENTER.x, 1.2f, BLD_CENTER.z),
             new Vector3(4, 1.5f, 3), yellow);
        // 5 debris lumps inside the sand collection
        for (int i = 0; i < 5; i++)
        {
            float xo = Random.Range(-1.3f, 1.3f);
            float zo = Random.Range(-0.9f, 0.9f);
            float s  = Random.Range(0.3f, 0.7f);
            Cube(bld.transform, $"SandCollection_Sand_{i}",
                 new Vector3(BLD_CENTER.x + xo, 1.9f, BLD_CENTER.z + zo),
                 new Vector3(s, s*0.4f, s), debris);
        }

        // Waste chute from upper floor down to wall pipe (slanted cube)
        var chute = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chute.name = "WasteChute";
        chute.transform.SetParent(bld.transform, false);
        chute.transform.position = new Vector3(BLD_CENTER.x, 5, BLD_CENTER.z - 8);
        chute.transform.rotation = Quaternion.Euler(-35, 0, 0);
        chute.transform.localScale = new Vector3(2, 0.1f, 7);
        ApplyMat(chute, concreteDark);

        // Black wall pipe — horizontal, going through south wall at Y=4
        BuildHorizontalPipe(bld.transform, "WallWastePipe",
                            start: new Vector3(BLD_CENTER.x, 4, BLD_CENTER.z - 14),
                            end:   new Vector3(BLD_CENTER.x, 4, BLD_CENTER.z - 18),
                            radius: 0.7f, mat: blackPipe, isZAligned: true);

        // ===== Outside yellow skip =====
        DeleteByName(area.transform, "Stage1_Details/YellowSkip"); // remove old position
        var skip = new GameObject("OutsideYellowSkip");
        skip.transform.SetParent(area.transform, false);
        skip.transform.position = new Vector3(BLD_CENTER.x, 1, minZ - 5);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(skip.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(6, 2, 3);
        ApplyMat(body, yellow);

        var hollow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hollow.name = "Interior";
        hollow.transform.SetParent(skip.transform, false);
        hollow.transform.localPosition = new Vector3(0, 0.3f, 0);
        hollow.transform.localScale = new Vector3(5.5f, 1.6f, 2.4f);
        ApplyMat(hollow, EnsureMat($"{MAT_FOLDER}/SkipInterior.mat", new Color(0.10f, 0.09f, 0.07f), smoothness: 0.1f));

        // Debris pile inside outside skip
        for (int i = 0; i < 12; i++)
        {
            var lump = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lump.name = $"Debris_{i}";
            lump.transform.SetParent(skip.transform, false);
            float x = Random.Range(-2.3f, 2.3f);
            float z = Random.Range(-1f, 1f);
            float s = Random.Range(0.4f, 1.1f);
            lump.transform.localPosition = new Vector3(x, 1.3f, z);
            lump.transform.localRotation = Quaternion.Euler(Random.Range(0,360), Random.Range(0,360), Random.Range(0,360));
            lump.transform.localScale = new Vector3(s, s*0.35f, s);
            ApplyMat(lump, debris);
        }

        // ===== Reposition existing Stage1_Details equipment to be INSIDE the building =====
        RepositionExistingEquipment(area, brownWater);

        // ===== Move LandingPoint to face the entrance sign =====
        var landing = GameObject.Find("Teleport_Target_Sim");
        if (landing != null)
        {
            landing.transform.position = new Vector3(200, 1.7f, -20);
            landing.transform.rotation = Quaternion.Euler(0, 90, 0);
            Debug.Log("[BeitShanStage1Building] LandingPoint moved to (200, 1.7, -20) facing east");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[BeitShanStage1Building] Building, pipes, internal equipment and outside skip built.");
    }

    // ==================== Helpers ====================

    static void BuildSouthWallSplit(Transform parent, Vector3 center, float w, float h, float th, float minZ, Material mat)
    {
        // Window strip at Y=9 (upper part), pipe opening Ø1.5 at Y=4 (lower part)
        // We'll model it as four cube segments:
        //   - bottomLeft + bottomRight (skipping a 1.5-wide gap at center for the pipe at Y=3.25)
        //   - top above windows
        //   - between-windows verticals
        // Simpler: 6 vertical strips + 3 horizontal bands.

        float pipeOpeningHalfW = 1f, pipeOpeningCenterY = 4f, pipeOpeningH = 2f;

        // Strip below the pipe opening (full width)
        Cube(parent, "SWall_BelowPipe", new Vector3(center.x, (pipeOpeningCenterY - pipeOpeningH*0.5f)*0.5f, minZ),
             new Vector3(w, pipeOpeningCenterY - pipeOpeningH*0.5f, th), mat);

        // Strip above the pipe opening up to Y=8 (start of window strip)
        float midBandBottom = pipeOpeningCenterY + pipeOpeningH*0.5f;
        float midBandTop = 8;
        Cube(parent, "SWall_AbovePipe", new Vector3(center.x, (midBandBottom + midBandTop)*0.5f, minZ),
             new Vector3(w, midBandTop - midBandBottom, th), mat);

        // Bands on left/right of the pipe opening (so the pipe area has a clear gap of width 2)
        Cube(parent, "SWall_PipeBand_L", new Vector3(center.x - (pipeOpeningHalfW + (w*0.25f)), pipeOpeningCenterY, minZ),
             new Vector3((w*0.5f - pipeOpeningHalfW), pipeOpeningH, th), mat);
        Cube(parent, "SWall_PipeBand_R", new Vector3(center.x + (pipeOpeningHalfW + (w*0.25f)), pipeOpeningCenterY, minZ),
             new Vector3((w*0.5f - pipeOpeningHalfW), pipeOpeningH, th), mat);

        // Top band above the windows
        float winTop = 10, winBottom = 8;
        Cube(parent, "SWall_AboveWindows", new Vector3(center.x, (winTop + h)*0.5f, minZ),
             new Vector3(w, h - winTop, th), mat);

        // Between-window vertical pillars (3 windows -> 4 pillars across the wall)
        for (int i = 0; i <= 3; i++)
        {
            float t = i / 3f;
            float px = center.x - w*0.5f + t * w;
            float pillarW = 1.5f;
            Cube(parent, $"SWall_Pillar_{i}", new Vector3(px, (winTop + winBottom)*0.5f, minZ),
                 new Vector3(pillarW, winTop - winBottom, th), mat);
        }
    }

    static void BuildWallWithOpening(Transform parent, string name, float wallPosX, float wallPosZ,
                                     float wallSpanZ, float wallH, float openingCenterY,
                                     float openingW, float openingH, bool isXFacing, Material mat)
    {
        // Build 4 cubes around the opening: below, above, left of opening, right of opening
        // For X-facing wall, "left/right" of opening means -Z/+Z direction
        // Lower band
        Cube(parent, name + "_Lower", new Vector3(wallPosX, (openingCenterY - openingH*0.5f)*0.5f, wallPosZ),
             new Vector3(WALL_TH, openingCenterY - openingH*0.5f, wallSpanZ), mat);
        // Upper band
        float upperBottom = openingCenterY + openingH*0.5f;
        Cube(parent, name + "_Upper", new Vector3(wallPosX, (upperBottom + wallH)*0.5f, wallPosZ),
             new Vector3(WALL_TH, wallH - upperBottom, wallSpanZ), mat);
        // Left of opening (-Z side)
        float leftSpan = (wallSpanZ - openingW)*0.5f;
        Cube(parent, name + "_LeftOfOpening", new Vector3(wallPosX, openingCenterY, wallPosZ - (openingW*0.5f + leftSpan*0.5f)),
             new Vector3(WALL_TH, openingH, leftSpan), mat);
        // Right of opening (+Z side)
        Cube(parent, name + "_RightOfOpening", new Vector3(wallPosX, openingCenterY, wallPosZ + (openingW*0.5f + leftSpan*0.5f)),
             new Vector3(WALL_TH, openingH, leftSpan), mat);
    }

    static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        ApplyMat(go, mat);
        return go;
    }

    static GameObject BuildHorizontalPipe(Transform parent, string name, Vector3 start, Vector3 end,
                                          float radius, Material mat, bool isZAligned = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
        go.transform.SetParent(parent, false);
        Vector3 mid = (start + end) * 0.5f;
        float length = Vector3.Distance(start, end);
        go.transform.position = mid;
        // Cylinder default orientation: Y is its long axis
        Vector3 dir = (end - start).normalized;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        // Unity Cylinder has height=2 by default, so localScale.y = length / 2
        go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        ApplyMat(go, mat);
        return go;
    }

    static void RepositionExistingEquipment(GameObject area, Material brownWater)
    {
        // Move LD screens inside the building, upper floor
        // Phase 2 already created Stage1_Details/LDScreen_Left and LDScreen_Right
        var details = area.transform.Find("Stage1_Details");
        if (details == null) { Debug.LogWarning("Stage1_Details not found — skipping repositioning"); return; }

        var ldLeft  = details.Find("LDScreen_Left");
        var ldRight = details.Find("LDScreen_Right");
        if (ldLeft != null)
        {
            ldLeft.position = new Vector3(255, 6.5f, -8);
            ldLeft.rotation = Quaternion.Euler(0, 90, 0);
        }
        if (ldRight != null)
        {
            ldRight.position = new Vector3(255, 6.5f, -12);
            ldRight.rotation = Quaternion.Euler(0, 90, 0);
        }

        // Move (or rebuild) the sand-sed water surface inside the building upper floor
        var oldSandSed = details.Find("SandSedWaterSurface");
        if (oldSandSed != null) Object.DestroyImmediate(oldSandSed.gameObject);
        var newSandSed = GameObject.CreatePrimitive(PrimitiveType.Cube);
        newSandSed.name = "SandSedWaterSurface";
        Object.DestroyImmediate(newSandSed.GetComponent<BoxCollider>());
        newSandSed.transform.SetParent(details, false);
        newSandSed.transform.position = new Vector3(275, 8.7f, -10);
        newSandSed.transform.localScale = new Vector3(12, 0.2f, 15);
        ApplyMat(newSandSed, brownWater);

        // Remove the original Stage1_Details/YellowSkip (we built an OutsideYellowSkip instead)
        var oldSkip = details.Find("YellowSkip");
        if (oldSkip != null) Object.DestroyImmediate(oldSkip.gameObject);

        Debug.Log("[BeitShanStage1Building] LD screens, sand-sed surface repositioned; old YellowSkip removed.");
    }

    static Material EnsureMat(string path, Color baseColor, float smoothness = 0.3f, float metallic = 0f)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            if (existing.HasProperty("_BaseColor")) existing.SetColor("_BaseColor", baseColor);
            if (existing.HasProperty("_Smoothness")) existing.SetFloat("_Smoothness", smoothness);
            if (existing.HasProperty("_Metallic")) existing.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { Debug.LogError("URP/Lit shader missing"); return null; }
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (baseColor.a < 0.99f)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void ApplyMat(GameObject go, Material mat)
    {
        if (mat == null) return;
        var r = go.GetComponent<MeshRenderer>();
        if (r != null) r.sharedMaterial = mat;
    }

    static void DeleteByName(Transform parent, string name)
    {
        // Supports paths like "Stage1_Details/YellowSkip" — split by /
        var parts = name.Split('/');
        Transform cur = parent;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            cur = cur.Find(parts[i]);
            if (cur == null) return;
        }
        var last = cur.Find(parts[parts.Length - 1]);
        if (last != null) Object.DestroyImmediate(last.gameObject);
    }
}
