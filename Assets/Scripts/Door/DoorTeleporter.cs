using UnityEngine;

public class DoorTeleporter : MonoBehaviour
{
    [Tooltip("Where to teleport.")]
    public Transform target;

    [Tooltip("Which area this teleport leads to. Updates the radial menu.")]
    public WorldArea destinationArea = WorldArea.Models;

    public void Teleport()
    {
        if (target == null) { Debug.LogWarning($"[DoorTeleporter] {name}: target NULL"); return; }
        if (Camera.main == null) { Debug.LogWarning("[DoorTeleporter] No Main Camera"); return; }

        GameObject xrOrigin = Camera.main.transform.root.gameObject;
        Transform cam = Camera.main.transform;

        CharacterController cc = xrOrigin.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        xrOrigin.transform.rotation = target.rotation;
        Physics.SyncTransforms();

        Vector3 camOffsetWorld = cam.position - xrOrigin.transform.position;
        Vector3 desiredOriginPos = new Vector3(
            target.position.x - camOffsetWorld.x,
            target.position.y,
            target.position.z - camOffsetWorld.z
        );
        xrOrigin.transform.position = desiredOriginPos;
        Physics.SyncTransforms();

        if (cc != null) cc.enabled = true;

        if (WorldAreaManager.Instance != null)
            WorldAreaManager.Instance.CurrentArea = destinationArea;

        // Record for replay — no-op when no session is active. Captures who
        // teleported, the door's GameObject name, and the destination area.
        ulong clientId = Unity.Netcode.NetworkManager.Singleton != null
            ? Unity.Netcode.NetworkManager.Singleton.LocalClientId
            : 0UL;
        WaterVision.Recording.WorldEventRecorder.Record(
            new WaterVision.Recording.Events.DoorEvent
            {
                DoorName = gameObject.name,
                DestinationArea = destinationArea,
                ClientId = clientId,
            });
    }
}