using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

public static class BeitShanTeleportDebug
{
    [MenuItem("MCP/Beit Shan/Teleport To Sim Landing (Play or Edit)")]
    public static void TeleportToSimLanding()
    {
        var landing = GameObject.Find("Teleport_Target_Sim");
        if (landing == null) { Debug.LogError("[BeitShanTeleportDebug] Teleport_Target_Sim not found in scene"); return; }

        // Find the XR Origin (root with XROrigin component, OR the rig root).
        var origin = GameObject.Find("XR Interaction Setup (MP Variant)");
        if (origin == null) { Debug.LogError("[BeitShanTeleportDebug] XR Interaction Setup (MP Variant) not found"); return; }

        var cam = Camera.main;
        if (cam == null) { Debug.LogError("[BeitShanTeleportDebug] Camera.main is null"); return; }

        // Disable CharacterController + XRBodyTransformer (so transform changes stick).
        var cc = origin.GetComponent<CharacterController>();
        var body = origin.GetComponentInChildren<XRBodyTransformer>();
        if (body != null) body.enabled = false;
        if (cc != null) cc.enabled = false;

        // Rotate first so camera offset is in the new orientation.
        origin.transform.rotation = landing.transform.rotation;
        Physics.SyncTransforms();

        // Compute offset and place rig so camera ends up at landing position.
        Vector3 camOffset = cam.transform.position - origin.transform.position;
        Vector3 newRigPos = new Vector3(
            landing.transform.position.x - camOffset.x,
            landing.transform.position.y,
            landing.transform.position.z - camOffset.z
        );
        origin.transform.position = newRigPos;
        Physics.SyncTransforms();

        if (cc != null) cc.enabled = true;
        if (body != null) body.enabled = true;

        Debug.Log($"[BeitShanTeleportDebug] Teleported XR rig. Rig now at {origin.transform.position}, " +
                  $"Camera at {cam.transform.position}, Landing target {landing.transform.position}. " +
                  $"Plant should be ~30m east of camera.");
    }

    [MenuItem("MCP/Beit Shan/Diagnose: Where Am I?")]
    public static void Diagnose()
    {
        var origin = GameObject.Find("XR Interaction Setup (MP Variant)");
        var cam = Camera.main;
        var landing = GameObject.Find("Teleport_Target_Sim");
        var plant = GameObject.Find("Beit Shan SIM 1.0.0");

        Debug.Log($"[BeitShanTeleportDebug] === Diagnostic ===");
        Debug.Log($"  XR Origin world pos: {(origin != null ? origin.transform.position.ToString() : "NOT FOUND")}");
        Debug.Log($"  Main Camera world pos: {(cam != null ? cam.transform.position.ToString() : "NOT FOUND")}");
        Debug.Log($"  Teleport_Target_Sim world pos: {(landing != null ? landing.transform.position.ToString() : "NOT FOUND")}");
        Debug.Log($"  Beit Shan SIM 1.0.0 world pos: {(plant != null ? plant.transform.position.ToString() : "NOT FOUND")}");

        // Check renderer counts under the plant
        if (plant != null)
        {
            var rens = plant.GetComponentsInChildren<MeshRenderer>(true);
            int withMat = 0, withoutMat = 0;
            foreach (var r in rens)
            {
                if (r.sharedMaterial != null) withMat++;
                else withoutMat++;
            }
            Debug.Log($"  Plant MeshRenderers: total={rens.Length}, withMaterial={withMat}, withoutMaterial={withoutMat}");
        }

        // Check LandingPoint registry
        var lp = landing != null ? landing.GetComponent<LandingPoint>() : null;
        Debug.Log($"  LandingPoint registered: {(LandingPoint.Get(WorldArea.Simulation) != null ? "YES" : "NO")} " +
                  $"(landing has LandingPoint component: {lp != null})");
    }
}
