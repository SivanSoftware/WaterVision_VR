using UnityEngine;

/// <summary>
/// A 3D trash bin: anything dropped (or thrown) into the trigger volume that carries a
/// <see cref="NetworkSpawnableModelVisual"/> gets despawned across all clients via the
/// component's networked <c>RequestDespawn()</c>.
///
/// Setup in the scene:
/// - Add a visual: cylinder / cube / model — whatever the bin looks like.
/// - Add a Collider with <c>isTrigger = true</c> sized to the bin's opening.
/// - Add this component. (Optional) Drag a "whoosh" AudioClip into <see cref="whooshClip"/>.
///
/// The script ignores non-pipe colliders, so it never deletes the player, the
/// controllers, or other objects passing through.
/// </summary>
[DisallowMultipleComponent]
public class PipeTrashBin : MonoBehaviour
{
    [SerializeField, Tooltip("Optional SFX played at the bin position when a pipe is trashed.")]
    AudioClip whooshClip;

    [SerializeField, Range(0f, 1f), Tooltip("Whoosh volume.")]
    float whooshVolume = 0.6f;

    [SerializeField, Tooltip("Enable to log each trashed pipe (helps verify multiplayer despawn).")]
    bool debugLog = false;

    void Reset()
    {
        // When the user adds the component in the Inspector, default the collider to trigger.
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other) { Handle(other, "TriggerEnter"); }

    // Backup path: even slow-moving / static-resting colliders that don't fire Enter
    // (kinematic + Discrete collision can miss fast Enter events) get caught here.
    void OnTriggerStay (Collider other) { Handle(other, "TriggerStay"); }

    void Handle(Collider other, string sourceTag)
    {
        // Diagnostic — log EVERY collider that touches the trigger so we can tell whether the
        // trigger is firing at all. Filter to per-collider once to avoid spamming.
        if (debugLog && other != null && _lastLoggedCollider != other)
        {
            _lastLoggedCollider = other;
            Debug.Log($"[PipeTrashBin] {sourceTag}: collider='{other.name}', root='{other.transform.root.name}'.", this);
        }

        if (other == null) return;
        // Walk up from whichever child collider entered (the pipe's grab collider may be on
        // a sub-object) to find the NetworkSpawnableModelVisual root.
        var pipe = other.GetComponentInParent<NetworkSpawnableModelVisual>();
        if (pipe == null) return;

        if (debugLog)
            Debug.Log($"[PipeTrashBin] Trashing pipe '{pipe.name}' (NetworkObjectId={pipe.NetworkObjectId}) via {sourceTag}.", this);

        pipe.RequestDespawn();

        if (whooshClip != null)
            AudioSource.PlayClipAtPoint(whooshClip, transform.position, whooshVolume);
    }

    Collider _lastLoggedCollider;
}
