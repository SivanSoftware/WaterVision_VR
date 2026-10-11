using UnityEngine;

/// <summary>
/// Marker for the location where the player teleports when entering Simulation mode.
/// Place this component on a GameObject in your scene at the target landing position.
/// The RadialMenu will find it automatically via the Instance singleton.
/// </summary>
public class SimulationLandingPoint : MonoBehaviour
{
    public static SimulationLandingPoint Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SimulationLandingPoint] Multiple instances found! Only one should exist per scene.");
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Optional: draw a visual indicator in the Scene view
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
    }
}