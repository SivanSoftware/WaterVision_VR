using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot tool: adds an XRI TeleportationArea to every floor tile of the Models Room
/// ("Room" in the hierarchy) so the player can teleport anywhere on its floor — like the grass.
///
/// Why per-tile: each floor tile already has its own BoxCollider; a separate teleport Cube laid
/// over the floor gets blocked (the ray hits the tile collider first). Putting the
/// TeleportationArea directly ON each tile's collider is exactly how the grass works.
///
/// The interaction layer mask is set to bit 31 ("Teleport") — the SAME value the working grass /
/// Ground tiles use — via the public interactionLayers property (reflection + implicit int
/// conversion), which is more reliable than poking the serialized m_Bits field.
///
/// Run: Tools/WaterVision/Add Teleport to Models Room Floor   (idempotent; also repairs the layer
/// mask on tiles that already have a TeleportationArea).
/// </summary>
public static class AddRoomFloorTeleport
{
    const string TELEPORT_AREA_TYPE =
        "UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea, Unity.XR.Interaction.Toolkit";

    // bit 31 — the "Teleport" interaction layer, same value the grass Cube (3) / Ground tiles use.
    const int TELEPORT_INTERACTION_LAYER_BITS = -2147483648;

    [MenuItem("Tools/WaterVision/Add Teleport to Models Room Floor")]
    public static void AddTeleport()
    {
        var room = GameObject.Find("Room");
        if (room == null) { Debug.LogError("[AddRoomFloorTeleport] 'Room' GameObject not found in the open scene."); return; }

        var areaType = System.Type.GetType(TELEPORT_AREA_TYPE);
        if (areaType == null)
        {
            Debug.LogError("[AddRoomFloorTeleport] Could not resolve TeleportationArea type. Is the XR Interaction Toolkit installed?");
            return;
        }

        // Resolve the interactionLayers property + the implicit (int -> InteractionLayerMask) operator once.
        var layersProp = areaType.GetProperty("interactionLayers", BindingFlags.Public | BindingFlags.Instance);
        object maskValue = null;
        if (layersProp != null)
        {
            var op = layersProp.PropertyType.GetMethod("op_Implicit", new[] { typeof(int) });
            if (op != null) maskValue = op.Invoke(null, new object[] { TELEPORT_INTERACTION_LAYER_BITS });
        }

        int added = 0, repaired = 0, skipped = 0;
        foreach (var t in room.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Floor")) continue;

            // Only the mesh tiles carry a Collider (the parent "Floor_01" wrappers don't).
            var col = t.GetComponent<Collider>();
            if (col == null) { skipped++; continue; }

            var area = t.GetComponent(areaType);
            if (area == null) { area = t.gameObject.AddComponent(areaType); added++; }
            else repaired++;

            // Set the interaction layer mask via the public property (reliable) + a SerializedObject
            // fallback so it sticks regardless of XRI version.
            bool set = false;
            if (layersProp != null && maskValue != null)
            {
                try { layersProp.SetValue(area, maskValue); set = true; } catch { /* fall through */ }
            }
            if (!set)
            {
                var so = new SerializedObject(area);
                var p = so.FindProperty("m_InteractionLayers.m_Bits") ?? so.FindProperty("m_InteractionLayers");
                if (p != null) { p.intValue = TELEPORT_INTERACTION_LAYER_BITS; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            EditorUtility.SetDirty(area);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"[AddRoomFloorTeleport] Done. Added {added} new, repaired layer on {repaired} existing, " +
                  $"skipped {skipped} non-collider wrappers. All floor tiles now teleportable (layer bit 31).");
    }
}
