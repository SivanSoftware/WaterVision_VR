using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using XRMultiplayer;

/// <summary>
/// Builds the immersive water-flow tour for the Beit Shan plant.
/// - Creates Waypoint GameObjects that trace the path of water through each stage
/// - Wires them into WastewaterPlantManager.stations[i].tourWaypoints
/// - Updates Stage A description with the specific equipment from the real plant photos
///   (LD screens, yellow dumpster, sand sedimentation, primary clarifier)
/// </summary>
public static class BeitShanTourSetup
{
    [MenuItem("MCP/Beit Shan/Setup Immersive Water-Flow Tour")]
    public static void SetupTour()
    {
        var area = GameObject.Find("BeitShanSimulationArea");
        if (area == null) { Debug.LogError("BeitShanSimulationArea not found"); return; }
        var manager = Object.FindFirstObjectByType<WastewaterPlantManager>();
        if (manager == null) { Debug.LogError("WastewaterPlantManager not found"); return; }

        // Remove existing waypoint groups
        for (int i = area.transform.childCount - 1; i >= 0; i--)
        {
            var c = area.transform.GetChild(i);
            if (c.name.StartsWith("Waypoints_")) Object.DestroyImmediate(c.gameObject);
        }

        // Define waypoints for each station that follow the water's path.
        // Each row: (stationIndex, list of (pos, lookAt)).
        // lookAt is the world point the camera should turn to face at this waypoint.
        var stationWaypoints = new (int idx, (Vector3 pos, Vector3 lookAt)[] points)[]
        {
            // Stage A — Pre-Treatment & Primary Clarifier (12-point cinematic flow)
            // Path: facing entrance sign → fly to pipe corridor → POV inside pipe → into building
            //       → LD screens close-up → lower floor waste chute → outside yellow skip
            //       → sand sedimentation pool → glance at sand collection → exit east toward Primary Clarifier
            (0, new (Vector3, Vector3)[]
            {
                (new Vector3(200, 1.7f, -20),  new Vector3(220, 7,   -20)),   // 0: facing entrance sign
                (new Vector3(210, 5,    -10),  new Vector3(230, 7,   -10)),   // 1: rising toward pipe corridor
                (new Vector3(180, 7.5f, -10),  new Vector3(235, 7.5f,-10)),   // 2: behind approach pipes, looking east
                (new Vector3(200, 7.5f, -10),  new Vector3(260, 7.5f,-10)),   // 3: pipe POV — water flowing around
                (new Vector3(230, 7.5f, -10),  new Vector3(258, 7,   -10)),   // 4: at building west wall, pipe bend
                (new Vector3(250, 9,    -10),  new Vector3(258, 7,   -10)),   // 5: inside upper floor, facing LD screens
                (new Vector3(258, 8.5f, -10),  new Vector3(258, 7,   -10)),   // 6: close-up — rakes rotating
                (new Vector3(262, 6,    -22),  new Vector3(260, 4,   -25)),   // 7: lower floor view — waste chute + wall pipe
                (new Vector3(262, 3,    -28),  new Vector3(260, 2,   -30)),   // 8: outside — yellow skip receiving debris
                (new Vector3(270, 9.5f, -10),  new Vector3(275, 8.7f,-10)),   // 9: back upper floor — sand sed pool surface
                (new Vector3(270, 5,    -10),  new Vector3(260, 1,   -10)),   // 10: glance lower floor — sand collection
                (new Vector3(290, 7.5f,   0),  new Vector3(632, 5,   137)),   // 11: exiting east, heading to Primary
            }),
            // Stage B — Biological Treatment
            (1, new (Vector3, Vector3)[]
            {
                (new Vector3(700, 6, -200),   new Vector3(839, 4, -355)),     // approach
                (new Vector3(839, 6, -300),   new Vector3(839, 0, -355)),     // above biological reactor
                (new Vector3(839, 3, -355),   new Vector3(839, 3, -355 + 30)),// inside, looking through aerobic/anoxic/anaerobic zones
                (new Vector3(900, 5, -355),   new Vector3(1226, 4, 107)),     // exiting toward Stage C
            }),
            // Stage C — Secondary Clarification
            (2, new (Vector3, Vector3)[]
            {
                (new Vector3(1100, 5, 0),     new Vector3(1226, 4, 107)),     // approach
                (new Vector3(1226, 8, 50),    new Vector3(1226, 0, 107)),     // above secondary clarifier from above
                (new Vector3(1226, 3, 107),   new Vector3(1226, 0, 107)),     // edge of secondary
                (new Vector3(1400, 5, -200),  new Vector3(1588, 4, -400)),    // exiting toward Stage D
            }),
            // Stage D — Disinfection
            (3, new (Vector3, Vector3)[]
            {
                (new Vector3(1500, 5, -500),  new Vector3(1588, 4, -610)),    // approach
                (new Vector3(1588, 6, -550),  new Vector3(1588, 0, -610)),    // above disinfection building
                (new Vector3(1600, 3, -610),  new Vector3(1700, 0, -300)),    // toward Chlorine Contact Pool
                (new Vector3(1700, 5, -300),  new Vector3(1700, 0, -300)),    // above chlorine pool
            }),
            // Stage E — Sludge Treatment
            (4, new (Vector3, Vector3)[]
            {
                (new Vector3(200, 6, 100),   new Vector3(50, 4, 200)),       // approach
                (new Vector3(50, 6, 150),    new Vector3(50, 4, 200)),       // above sludge building
                (new Vector3(50, 3, 200),    new Vector3(50, 3, 250)),       // entering centrifuge area
            }),
        };

        var so = new SerializedObject(manager);
        var stations = so.FindProperty("stations");

        for (int s = 0; s < stationWaypoints.Length; s++)
        {
            int idx = stationWaypoints[s].idx;
            var pts = stationWaypoints[s].points;
            if (idx < 0 || idx >= stations.arraySize) continue;

            // Create a parent for this station's waypoints
            var parent = new GameObject($"Waypoints_Stage_{(char)('A'+idx)}");
            parent.transform.SetParent(area.transform, false);

            var elem = stations.GetArrayElementAtIndex(idx);
            var wpArray = elem.FindPropertyRelative("tourWaypoints");
            wpArray.arraySize = pts.Length;

            for (int p = 0; p < pts.Length; p++)
            {
                var wp = new GameObject($"WP_{p:00}");
                wp.transform.SetParent(parent.transform, false);
                wp.transform.position = pts[p].pos;
                Vector3 dir = (pts[p].lookAt - pts[p].pos);
                if (dir.sqrMagnitude > 0.001f)
                    wp.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

                wpArray.GetArrayElementAtIndex(p).objectReferenceValue = wp.transform;
            }

            // For Stage A (idx 0), shorten the dwell so the 12-point cinematic flows snappier,
            // AND turn on the microscale ride so the player feels like they're inside the pipe.
            if (idx == 0)
            {
                var dwell = elem.FindPropertyRelative("dwellPerWaypoint");
                var travel = elem.FindPropertyRelative("travelTimeBetweenWaypoints");
                if (dwell != null)  dwell.floatValue  = 2.5f;
                if (travel != null) travel.floatValue = 1.5f;

                var useMicro = elem.FindPropertyRelative("useMicroscale");
                var microScale = elem.FindPropertyRelative("microScale");
                var scaleDur = elem.FindPropertyRelative("scaleTransitionDuration");
                if (useMicro != null) useMicro.boolValue = true;
                if (microScale != null) microScale.floatValue = 0.03f;
                if (scaleDur != null) scaleDur.floatValue = 1.5f;
            }
        }

        // Update Stage A description with the specific equipment
        var stageA = stations.GetArrayElementAtIndex(0);
        var stageADesc = stageA.FindPropertyRelative("description");
        stageADesc.stringValue =
            "Wastewater flows in through the intake pipe. Two LD-system mechanical screens (the rake-style equipment) catch large solids — wipes, plastics, food waste — and dump them into a yellow skip outside the building. " +
            "The grey water then passes through a sand & grit chamber, where heavier particles settle to the bottom (the brown-water pool). " +
            "Finally the primary clarifier (the round pool) physically separates remaining settleable solids from the raw water before it heads to biological treatment.";

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[BeitShanTourSetup] Tour waypoints + Stage A description updated.");
    }
}
