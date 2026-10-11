using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using XRMultiplayer;

public class AvatarLayerController : NetworkBehaviour
{
    [Header("Drag Head Parts Here (To hide from self)")]
    public GameObject[] headParts;

    [Header("Drag Body Parts Here (To keep visible)")]
    public GameObject[] bodyParts;

    public override void OnNetworkSpawn()
    {
        if (headParts == null || headParts.Length == 0)
        {
            Debug.LogWarning($"[AvatarLayerController] Head Parts list is empty on {gameObject.name}");
            return;
        }

        if (IsOwner)
        {
            // אם זה אני: הסתר את הראש מהמצלמה שלי
            HideRenderersFromCamera(headParts);
        }
        // אם זה לא אני, לא צריך לעשות כלום - הכל נשאר ב-Default
    }

    void HideRenderersFromCamera(GameObject[] parts)
    {
        int mirrorLayer = LayerMask.NameToLayer("Mirror");

        if (mirrorLayer == -1)
        {
            Debug.LogError($"[AvatarLayerController] ❌ Layer 'Mirror' doesn't exist! Create it in Project Settings -> Tags and Layers");
            return;
        }

        foreach (var part in parts)
        {
            if (part == null) continue;

            // שנה רק את ה-Renderers - לא את כל ה-GameObject!
            Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                renderer.gameObject.layer = mirrorLayer;
            }
        }
    }

    // ── Ghost mode (used by ReplayManager) ────────────────────────────────────
    // When ghost mode is enabled, every live networked player's renderers are
    // turned off so only replay ghosts are visible. State is restored on disable.

    private static readonly Dictionary<Renderer, bool> s_GhostModeOriginalState = new();
    private static bool s_GhostModeActive;

    public static void SetGhostMode(bool enable)
    {
        if (enable == s_GhostModeActive) return;
        s_GhostModeActive = enable;

        if (enable)
        {
            s_GhostModeOriginalState.Clear();
            HideRenderersOn<XRINetworkPlayer_RPM>();
            HideRenderersOn<XRINetworkPlayer>();
        }
        else
        {
            foreach (var kvp in s_GhostModeOriginalState)
                if (kvp.Key != null) kvp.Key.enabled = kvp.Value;
            s_GhostModeOriginalState.Clear();
        }
    }

    private static void HideRenderersOn<T>() where T : Component
    {
        var players = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var p in players)
        {
            var renderers = p.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!s_GhostModeOriginalState.ContainsKey(r))
                    s_GhostModeOriginalState[r] = r.enabled;
                r.enabled = false;
            }
        }
    }
}