using System;
using UnityEngine;

public enum WorldArea { Home, Simulation, Models }

/// <summary>
/// Singleton that tracks which world area the player is in.
/// Fires OnAreaChanged when the area changes.
/// Place exactly one instance in the scene.
/// </summary>
public class WorldAreaManager : MonoBehaviour
{
    public static WorldAreaManager Instance { get; private set; }

    [SerializeField] private WorldArea startingArea = WorldArea.Home;

    private WorldArea _currentArea;
    public WorldArea CurrentArea
    {
        get => _currentArea;
        set
        {
            if (_currentArea != value)
            {
                _currentArea = value;
                Debug.Log($"[WorldAreaManager] Area → {value}");
                OnAreaChanged?.Invoke(value);
            }
        }
    }

    public event Action<WorldArea> OnAreaChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _currentArea = startingArea;
    }

    void Start()
    {
        // Fire once so UI initializes correctly on scene load
        OnAreaChanged?.Invoke(_currentArea);
    }
}