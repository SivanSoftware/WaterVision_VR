using UnityEngine;

/// <summary>
/// Gates a biomass particle cloud (and all its child particle systems) so it only EMITS while the
/// cutscene camera is BELOW the water surface. Used by the WWTP SIM 4 (Biological Reactor) cutscene:
/// the cloud is parented directly to the cutscene camera (so it always rides the lens), and this
/// component switches its emission on the moment the camera dives under the surface and off again
/// when it surfaces — so the player is wrapped in dense activated-sludge flocs + oxygen bubbles
/// during the dive, but the cloud doesn't float in front of the lens during the above-water passes.
/// </summary>
[DisallowMultipleComponent]
public class UnderwaterBiologyFollower : MonoBehaviour
{
    [Tooltip("The cutscene camera transform whose depth decides when the cloud emits.")]
    public Transform target;

    [Tooltip("World Y of the water surface. The cloud only emits when the camera is below this.")]
    public float surfaceY = 0f;

    [Tooltip("Legacy: also snap this object onto the target each frame. Leave false when the cloud is parented to the camera.")]
    public bool followPosition = false;

    ParticleSystem[] _systems;

    void Awake()  { Cache(); }
    void OnEnable(){ if (_systems == null || _systems.Length == 0) Cache(); }
    void Cache()  { _systems = GetComponentsInChildren<ParticleSystem>(true); }

    void LateUpdate()
    {
        if (target == null) return;
        if (followPosition) transform.position = target.position;

        bool underwater = target.position.y < surfaceY;
        if (_systems == null) return;
        foreach (var ps in _systems)
        {
            if (ps == null) continue;
            var em = ps.emission;
            if (em.enabled != underwater) em.enabled = underwater;   // emit only while submerged
        }
    }
}
