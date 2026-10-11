using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the visual detail of Stage 1 + visible water flow throughout the plant:
/// - 2 LD-style mechanical screens at the intake (with rotating rakes)
/// - Yellow waste skip dumpster + debris pile
/// - Brown water surface in the sand sedimentation pool
/// - Rotating bridge scrapers on Primary Sedimentation tanks
/// - Water flow particle systems along the process path
/// - Aeration bubbles in the biological reactor
/// </summary>
public static class BeitShanStage1Detail
{
    const string MAT_FOLDER = "Assets/Models/Asset/Pipes/Materials_Extracted";

    [MenuItem("MCP/Beit Shan/Build Stage 1 Visual Detail")]
    public static void BuildStage1Detail()
    {
        var area = GameObject.Find("BeitShanSimulationArea");
        if (area == null) { Debug.LogError("BeitShanSimulationArea not found"); return; }

        // Clean previous run
        DeleteByName(area.transform, "Stage1_Details");
        DeleteByName(area.transform, "WaterFlow");

        // Shared materials
        var metalMat   = EnsureMat($"{MAT_FOLDER}/MetalScreen.mat",  new Color(0.55f, 0.58f, 0.62f), smoothness: 0.65f, metallic: 0.7f);
        var rakeMat    = EnsureMat($"{MAT_FOLDER}/Rake.mat",         new Color(0.35f, 0.40f, 0.45f), smoothness: 0.5f, metallic: 0.5f);
        var yellowMat  = EnsureMat($"{MAT_FOLDER}/YellowSkip.mat",   new Color(0.95f, 0.78f, 0.18f), smoothness: 0.55f);
        var debrisMat  = EnsureMat($"{MAT_FOLDER}/Debris.mat",       new Color(0.30f, 0.25f, 0.18f), smoothness: 0.1f);
        var brownWater = EnsureMat($"{MAT_FOLDER}/BrownWater.mat",   new Color(0.42f, 0.32f, 0.18f, 0.88f), smoothness: 0.85f);
        var concreteMat= EnsureMat($"{MAT_FOLDER}/Concrete.mat",     new Color(0.78f, 0.76f, 0.72f), smoothness: 0.2f);

        // ===== Stage 1 Details =====
        var details = new GameObject("Stage1_Details");
        details.transform.SetParent(area.transform, false);

        // Two LD-style mechanical screens at the intake area
        BuildLDScreen(details.transform, "LDScreen_Left",
                      pos: new Vector3(258, 2, -7),
                      yaw: 90, metalMat, rakeMat, concreteMat);
        BuildLDScreen(details.transform, "LDScreen_Right",
                      pos: new Vector3(258, 2, -13),
                      yaw: 90, metalMat, rakeMat, concreteMat);

        // Yellow waste skip dumpster + debris pile (outside the building)
        BuildYellowSkip(details.transform, new Vector3(272, 1, -10), yellowMat, debrisMat);

        // Brown water surface on the sand sedimentation pool
        BuildPool(details.transform, "SandSedWaterSurface", new Vector3(320, 3.5f, 195),
                  scale: new Vector3(40, 0.1f, 60), brownWater);

        // Rotating bridge scrapers on Primary Sedimentation tanks
        BuildPrimaryScraper(details.transform, "PrimaryScraper_0", new Vector3(632, 1.5f, 248.6f));
        BuildPrimaryScraper(details.transform, "PrimaryScraper_1", new Vector3(803.98f, 1.5f, 136.7f));
        BuildPrimaryScraper(details.transform, "PrimaryScraper_2", new Vector3(632, 1.5f, 35.9f));
        // Secondary clarifier scrapers too
        BuildPrimaryScraper(details.transform, "SecondaryScraper_0", new Vector3(1226, 1.5f, 107),  scale: 1.5f);
        BuildPrimaryScraper(details.transform, "SecondaryScraper_1", new Vector3(1226, 1.5f, 544),  scale: 1.5f);
        BuildPrimaryScraper(details.transform, "SecondaryScraper_2", new Vector3(1633, 1.5f, 602),  scale: 1.5f);

        // ===== Water Flow particle systems =====
        var waterRoot = new GameObject("WaterFlow");
        waterRoot.transform.SetParent(area.transform, false);

        // ===== Stage 1 Immersive Sewage Stream (Phase 4) =====
        // 5-layer particle composition at the intake pipe corridor — visible from microscale POV.

        // Layer 1: Brown sewage water — primary visual mass
        BuildWaterParticles(waterRoot.transform, "Sewage_BrownWater_Intake",
                            pos: new Vector3(195, 7.5f, -10),
                            yaw: 90,
                            color: new Color(0.42f, 0.32f, 0.18f, 0.88f),
                            startSpeed: 12f, rate: 250, sizeRange: new Vector2(0.35f, 0.55f),
                            lifetime: 4f, coneAngle: 4f, radius: 0.8f);

        // Layer 2: Wipes (off-white) — visible chunks tumbling
        BuildWaterParticles(waterRoot.transform, "Sewage_Wipes_Intake",
                            pos: new Vector3(195, 7.5f, -10),
                            yaw: 90,
                            color: new Color(0.86f, 0.84f, 0.78f, 1f),
                            startSpeed: 11f, rate: 30, sizeRange: new Vector2(0.06f, 0.12f),
                            lifetime: 4f, coneAngle: 6f, radius: 0.6f);

        // Layer 3: Organic solids (darker brown specks)
        BuildWaterParticles(waterRoot.transform, "Sewage_Organics_Intake",
                            pos: new Vector3(195, 7.5f, -10),
                            yaw: 90,
                            color: new Color(0.30f, 0.20f, 0.10f, 1f),
                            startSpeed: 11f, rate: 60, sizeRange: new Vector2(0.04f, 0.08f),
                            lifetime: 4f, coneAngle: 6f, radius: 0.5f);

        // Layer 4: Sand grains (sandy yellow-brown, smallest)
        BuildWaterParticles(waterRoot.transform, "Sewage_Sand_Intake",
                            pos: new Vector3(195, 7.5f, -10),
                            yaw: 90,
                            color: new Color(0.65f, 0.50f, 0.30f, 1f),
                            startSpeed: 10f, rate: 120, sizeRange: new Vector2(0.02f, 0.04f),
                            lifetime: 4f, coneAngle: 8f, radius: 0.7f);

        // Layer 5: Splash froth — bright white mist for "churn" feel
        BuildWaterParticles(waterRoot.transform, "Sewage_Splash_Intake",
                            pos: new Vector3(195, 7.5f, -10),
                            yaw: 90,
                            color: new Color(0.95f, 0.95f, 0.92f, 0.45f),
                            startSpeed: 9f, rate: 50, sizeRange: new Vector2(0.10f, 0.18f),
                            lifetime: 3f, coneAngle: 12f, radius: 0.9f);

        // Falling water from pipe into the screen channel (now inside building upper floor)
        BuildWaterParticles(waterRoot.transform, "Flow_PipeFallToScreens",
                            pos: new Vector3(252, 7, -10),
                            pitch: 70,
                            color: new Color(0.40f, 0.34f, 0.22f, 0.85f),
                            startSpeed: 4f, rate: 60, sizeRange: new Vector2(0.3f, 0.6f),
                            lifetime: 2f, coneAngle: 15f, radius: 0.6f);

        // After screens — clearer grey water continuing east
        BuildWaterParticles(waterRoot.transform, "Flow_AfterScreens",
                            pos: new Vector3(268, 7.5f, -10),
                            yaw: 90,
                            color: new Color(0.50f, 0.46f, 0.36f, 0.78f),
                            startSpeed: 3.5f, rate: 80, sizeRange: new Vector2(0.25f, 0.5f),
                            lifetime: 3f, coneAngle: 8f, radius: 0.6f);

        // Wipes/debris drop through the wall pipe into the OutsideYellowSkip
        BuildWaterParticles(waterRoot.transform, "Sewage_DebrisDrop_OutsideSkip",
                            pos: new Vector3(260, 4, -25),
                            pitch: 75,   // pointing down-south
                            color: new Color(0.86f, 0.84f, 0.78f, 1f),
                            startSpeed: 3f, rate: 18, sizeRange: new Vector2(0.08f, 0.15f),
                            lifetime: 2f, coneAngle: 20f, radius: 0.3f);

        BuildWaterParticles(waterRoot.transform, "Sewage_DebrisDropOrganics_OutsideSkip",
                            pos: new Vector3(260, 4, -25),
                            pitch: 80,
                            color: new Color(0.30f, 0.20f, 0.10f, 1f),
                            startSpeed: 3.5f, rate: 35, sizeRange: new Vector2(0.05f, 0.10f),
                            lifetime: 2f, coneAngle: 20f, radius: 0.3f);

        // Wipes accumulating at the screens (stationary-ish, settling)
        BuildWaterParticles(waterRoot.transform, "Sewage_Caught_AtScreens",
                            pos: new Vector3(258, 7, -10),
                            yaw: 90,
                            color: new Color(0.78f, 0.72f, 0.62f, 1f),
                            startSpeed: 0.4f, rate: 12, sizeRange: new Vector2(0.10f, 0.18f),
                            lifetime: 6f, coneAngle: 40f, radius: 1.5f);

        // Sand settling in the Sand Sedimentation pool (moved into the new pool position)
        BuildWaterParticles(waterRoot.transform, "Sewage_SandSettle_SandSed",
                            pos: new Vector3(275, 9.5f, -10),
                            pitch: 90,   // straight down
                            color: new Color(0.65f, 0.50f, 0.30f, 1f),
                            startSpeed: 1.5f, rate: 80, sizeRange: new Vector2(0.02f, 0.04f),
                            lifetime: 5f, coneAngle: 30f, radius: 6f);

        // Brown water churning in the sand sed pool
        BuildWaterParticles(waterRoot.transform, "Flow_SandSedChurn",
                            pos: new Vector3(320, 4f, 195),
                            pitch: -90,  // upward (boiling effect)
                            color: new Color(0.45f, 0.32f, 0.18f, 0.6f),
                            startSpeed: 0.6f, rate: 40, sizeRange: new Vector2(0.2f, 0.4f),
                            lifetime: 3f, coneAngle: 90f, radius: 15f);

        // Flow toward primary clarifier
        BuildWaterParticles(waterRoot.transform, "Flow_ToPrimary",
                            pos: new Vector3(450, 2.5f, 140),
                            yaw: 90,
                            color: new Color(0.50f, 0.50f, 0.45f, 0.7f),
                            startSpeed: 3f, rate: 50, sizeRange: new Vector2(0.3f, 0.6f),
                            lifetime: 3f, coneAngle: 10f, radius: 0.8f);

        // Aeration bubbles in biological reactor (looking up)
        BuildWaterParticles(waterRoot.transform, "Flow_BioBubbles",
                            pos: new Vector3(839, 0.5f, -355),
                            pitch: -90,
                            color: new Color(0.85f, 0.92f, 0.98f, 0.7f),
                            startSpeed: 1.5f, rate: 120, sizeRange: new Vector2(0.15f, 0.35f),
                            lifetime: 4f, coneAngle: 90f, radius: 30f);

        // Clean water flowing from secondary to disinfection
        BuildWaterParticles(waterRoot.transform, "Flow_ToDisinfection",
                            pos: new Vector3(1400, 2, -200),
                            yaw: 135,  // southeast
                            color: new Color(0.55f, 0.70f, 0.80f, 0.75f),
                            startSpeed: 3f, rate: 50, sizeRange: new Vector2(0.3f, 0.5f),
                            lifetime: 3f, coneAngle: 10f, radius: 1.0f);

        // Final chlorinated effluent — bright clean
        BuildWaterParticles(waterRoot.transform, "Flow_ChlorineOut",
                            pos: new Vector3(1700, 1.5f, -290),
                            yaw: 0,
                            color: new Color(0.70f, 0.90f, 0.95f, 0.85f),
                            startSpeed: 2f, rate: 40, sizeRange: new Vector2(0.2f, 0.4f),
                            lifetime: 3f, coneAngle: 12f, radius: 1.0f);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[BeitShanStage1Detail] Stage 1 visual detail + water particles built. " +
                  "2 LD screens, yellow skip, sand sed pool surface, 6 rotating bridges, 8 particle systems.");
    }

    // ==================== LD Screen ====================

    static void BuildLDScreen(Transform parent, string name, Vector3 pos, float yaw,
                              Material metal, Material rake, Material concrete)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0, yaw, 0);

        // Concrete channel base under the screen
        var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basePlate.name = "ChannelBase";
        basePlate.transform.SetParent(root.transform, false);
        basePlate.transform.localPosition = new Vector3(0, 0, 0);
        basePlate.transform.localScale = new Vector3(5, 0.4f, 4);
        ApplyMat(basePlate, concrete);

        // Side walls (low)
        for (int side = -1; side <= 1; side += 2)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Wall_{(side<0?"L":"R")}";
            wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = new Vector3(0, 0.6f, side * 1.7f);
            wall.transform.localScale = new Vector3(5, 1.4f, 0.25f);
            ApplyMat(wall, concrete);
        }

        // Metal frame of the screen (slanted, like the LD machines in the photo)
        var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "Frame";
        frame.transform.SetParent(root.transform, false);
        frame.transform.localPosition = new Vector3(0.5f, 2, 0);
        frame.transform.localRotation = Quaternion.Euler(-25, 0, 0);
        frame.transform.localScale = new Vector3(2.5f, 4, 0.2f);
        ApplyMat(frame, metal);

        // Rake bars (vertical bars on the frame face)
        for (int i = 0; i < 9; i++)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = $"Bar_{i}";
            bar.transform.SetParent(frame.transform, false);
            float t = (i - 4) / 4f; // -1..1
            bar.transform.localPosition = new Vector3(0, 0, t * 0.46f);
            bar.transform.localScale = new Vector3(1.05f, 1.05f, 0.04f);
            ApplyMat(bar, rake);
        }

        // Rotating rake cleaner — disc + arms
        var rotator = new GameObject("Rotator");
        rotator.transform.SetParent(root.transform, false);
        rotator.transform.localPosition = new Vector3(-0.5f, 2.5f, 0);
        rotator.transform.localRotation = Quaternion.Euler(0, 0, 0);
        var spin = rotator.AddComponent<SpinAxis>();
        // Default axis is Y, but we want it rotating around its local X (looks like a paddle wheel)
        var spinSo = new SerializedObject(spin);
        spinSo.FindProperty("axis").enumValueIndex = 2; // Z
        spinSo.FindProperty("degreesPerSecond").floatValue = -25f;
        spinSo.ApplyModifiedPropertiesWithoutUndo();

        // 3 arms 120° apart
        for (int i = 0; i < 3; i++)
        {
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = $"Arm_{i}";
            arm.transform.SetParent(rotator.transform, false);
            arm.transform.localRotation = Quaternion.Euler(0, 0, i * 120);
            arm.transform.localPosition = arm.transform.localRotation * new Vector3(0, 1.1f, 0);
            arm.transform.localScale = new Vector3(0.2f, 2.2f, 0.5f);
            ApplyMat(arm, rake);
        }

        // Catch chute (slanted plate leading to dumpster)
        var chute = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chute.name = "Chute";
        chute.transform.SetParent(root.transform, false);
        chute.transform.localPosition = new Vector3(2.0f, 1.3f, 0);
        chute.transform.localRotation = Quaternion.Euler(0, 0, -22);
        chute.transform.localScale = new Vector3(2.5f, 0.1f, 1.5f);
        ApplyMat(chute, metal);
    }

    // ==================== Yellow Skip Dumpster ====================

    static void BuildYellowSkip(Transform parent, Vector3 pos, Material yellow, Material debris)
    {
        var skip = new GameObject("YellowSkip");
        skip.transform.SetParent(parent, false);
        skip.transform.position = pos;

        // Box (the dumpster body)
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(skip.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(5, 1.8f, 2.5f);
        ApplyMat(body, yellow);

        // Hollow it visually: black "interior" cube slightly inset
        var hollow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hollow.name = "Interior";
        hollow.transform.SetParent(skip.transform, false);
        hollow.transform.localPosition = new Vector3(0, 0.3f, 0);
        hollow.transform.localScale = new Vector3(4.5f, 1.4f, 2.0f);
        var darkMat = EnsureMat($"{MAT_FOLDER}/SkipInterior.mat", new Color(0.10f, 0.09f, 0.07f), smoothness: 0.1f);
        ApplyMat(hollow, darkMat);

        // Debris pile inside
        for (int i = 0; i < 6; i++)
        {
            var lump = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lump.name = $"Debris_{i}";
            lump.transform.SetParent(skip.transform, false);
            float x = Random.Range(-1.8f, 1.8f);
            float z = Random.Range(-0.8f, 0.8f);
            float scale = Random.Range(0.5f, 1.1f);
            lump.transform.localPosition = new Vector3(x, 1.1f, z);
            lump.transform.localRotation = Quaternion.Euler(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
            lump.transform.localScale = new Vector3(scale, scale * 0.4f, scale);
            ApplyMat(lump, debris);
        }

        // Label sign on the side
        var label = new GameObject("Label_Skip", typeof(UnityEngine.RectTransform), typeof(UnityEngine.Canvas), typeof(UnityEngine.UI.CanvasScaler));
        label.transform.SetParent(skip.transform, false);
        label.transform.localPosition = new Vector3(0, 0.7f, 1.27f);
        var rt = (RectTransform)label.transform;
        rt.sizeDelta = new Vector2(2000, 500);
        rt.localScale = Vector3.one * 0.001f;
        var c = label.GetComponent<UnityEngine.Canvas>();
        c.renderMode = UnityEngine.RenderMode.WorldSpace;
        var txt = new GameObject("Text", typeof(RectTransform));
        txt.transform.SetParent(label.transform, false);
        var trt = (RectTransform)txt.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var tmp = txt.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "WASTE / פסולת";
        tmp.fontSize = 240;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.color = Color.black;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
    }

    // ==================== Pool ====================

    static void BuildPool(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var pool = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pool.name = name;
        Object.DestroyImmediate(pool.GetComponent<BoxCollider>());
        pool.transform.SetParent(parent, false);
        pool.transform.position = pos;
        pool.transform.localScale = scale;
        ApplyMat(pool, mat);
    }

    // ==================== Rotating Bridge Scraper ====================

    static void BuildPrimaryScraper(Transform parent, string name, Vector3 pos, float scale = 1f)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        var spin = root.AddComponent<SpinAxis>();
        var spinSo = new SerializedObject(spin);
        spinSo.FindProperty("axis").enumValueIndex = 1; // Y (around vertical)
        spinSo.FindProperty("degreesPerSecond").floatValue = 4f;
        spinSo.ApplyModifiedPropertiesWithoutUndo();

        // Bridge beam across the tank
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "Bridge";
        beam.transform.SetParent(root.transform, false);
        beam.transform.localPosition = new Vector3(0, 0, 0);
        beam.transform.localScale = new Vector3(35 * scale, 0.6f, 1.5f);
        var bridgeMat = EnsureMat($"{MAT_FOLDER}/BridgeSteel.mat", new Color(0.45f, 0.45f, 0.50f), smoothness: 0.6f, metallic: 0.6f);
        ApplyMat(beam, bridgeMat);

        // Central column
        var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        col.name = "CenterColumn";
        Object.DestroyImmediate(col.GetComponent<CapsuleCollider>());
        col.transform.SetParent(root.transform, false);
        col.transform.localPosition = new Vector3(0, -1, 0);
        col.transform.localScale = new Vector3(2 * scale, 1, 2 * scale);
        ApplyMat(col, bridgeMat);
    }

    // ==================== Particle System Helper ====================

    static void BuildWaterParticles(Transform parent, string name, Vector3 pos,
                                    float yaw = 0, float pitch = 0,
                                    Color color = default,
                                    float startSpeed = 3f, float rate = 30,
                                    Vector2 sizeRange = default,
                                    float lifetime = 3f,
                                    float coneAngle = 10f,
                                    float radius = 0.5f)
    {
        if (sizeRange == default) sizeRange = new Vector2(0.2f, 0.5f);
        if (color == default) color = new Color(0.5f, 0.5f, 0.6f, 0.8f);

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(pitch, yaw, 0);

        var ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();

        // Use the default URP particle material with the URP/Particles/Unlit shader
        var psMat = EnsureParticleMat($"{MAT_FOLDER}/Particle_{name}.mat", color);
        renderer.sharedMaterial = psMat;

        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = lifetime;
        main.startSpeed = startSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
        main.startColor = color;
        main.maxParticles = 1000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = rate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = radius;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = grad;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0, 1f),
            new Keyframe(0.6f, 1.1f),
            new Keyframe(1, 0.6f));
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
    }

    // ==================== Materials / utilities ====================

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
        // Enable alpha blending for translucent water surfaces
        if (baseColor.a < 0.99f)
        {
            mat.SetFloat("_Surface", 1f);     // 0 = opaque, 1 = transparent
            mat.SetFloat("_Blend", 0f);       // alpha
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material EnsureParticleMat(string path, Color color)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) { existing.color = color; return existing; }
        // Try URP particles, fall back to URP/Lit
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
        // Try to set transparent
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 3000;
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
        var c = parent.Find(name);
        if (c != null) Object.DestroyImmediate(c.gameObject);
    }
}
