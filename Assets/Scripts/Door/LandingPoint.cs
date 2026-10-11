using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Auto-registering landing point. Attach to a GameObject with the right
/// WorldArea, and RadialMenuController will find it without inspector wiring.
/// </summary>
public class LandingPoint : MonoBehaviour
{
    private static readonly Dictionary<WorldArea, LandingPoint> registry = new();

    public static Transform Get(WorldArea area) =>
        registry.TryGetValue(area, out var p) && p != null ? p.transform : null;

    [SerializeField] private WorldArea area;

    void OnEnable()
    {
        if (registry.TryGetValue(area, out var existing) && existing != null && existing != this)
            Debug.LogWarning($"[LandingPoint] Duplicate for {area}: '{existing.name}' replaced by '{name}'.");
        registry[area] = this;
    }

    void OnDisable()
    {
        if (registry.TryGetValue(area, out var p) && p == this)
            registry.Remove(area);
    }
}