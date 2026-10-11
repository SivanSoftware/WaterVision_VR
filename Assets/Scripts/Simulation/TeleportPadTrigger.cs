using UnityEngine;

/// <summary>
/// Floor-mounted teleport pad. Fires the attached DoorTeleporter when the player
/// (any CharacterController, typically the XR rig) steps onto it.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(DoorTeleporter))]
public class TeleportPadTrigger : MonoBehaviour
{
    public DoorTeleporter teleporter;

    [Tooltip("Seconds to wait between teleports so the pad doesn't re-fire on landing.")]
    public float cooldown = 1.5f;

    float _lastFire;

    void Reset()
    {
        teleporter = GetComponent<DoorTeleporter>();
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time - _lastFire < cooldown) return;

        // Only react to the XR rig (CharacterController root).
        var rig = other.GetComponentInParent<CharacterController>();
        if (rig == null) return;

        _lastFire = Time.time;
        if (teleporter != null) teleporter.Teleport();
    }
}
