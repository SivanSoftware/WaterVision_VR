using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Whiteboard))]
public class WhiteboardSync : NetworkBehaviour
{
    private Whiteboard _whiteboard;

    void Awake()
    {
        _whiteboard = GetComponent<Whiteboard>();
        Debug.Log($"[WhiteboardSync] Awake on {name}. Whiteboard component: {(_whiteboard != null ? "OK" : "NULL")}");
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[WhiteboardSync] OnNetworkSpawn: IsSpawned={IsSpawned}, IsClient={IsClient}, NetworkObjectId={NetworkObjectId}, IsOwner={IsOwner}, OwnerClientId={OwnerClientId}, LocalId={(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId.ToString() : "?")}");
    }

    public void DrawStroke(int x, int y, int penSize, Color color)
    {
        Debug.Log($"[WhiteboardSync] DrawStroke: ({x},{y}) IsSpawned={IsSpawned}");

        // Apply locally first for instant feedback
        ApplyStroke(x, y, penSize, color);

        // Record for replay — no-op when no session is active. Recorder also
        // gates on its own isRecording flag, so this is cheap.
        WaterVision.Recording.WorldEventRecorder.Record(
            new WaterVision.Recording.Events.WhiteboardStrokeEvent
            {
                X = x, Y = y, PenSize = penSize, Color = color,
            });

        if (!IsSpawned)
        {
            Debug.LogWarning("[WhiteboardSync] NOT spawned — RPC skipped (local-only).");
            return;
        }

        // Modern NGO 2.x RPC — works cleanly under Distributed Authority.
        // SendTo.NotMe = send to every connected client except the sender.
        Debug.Log("[WhiteboardSync] Calling DrawStrokeRpc(SendTo.NotMe) ...");
        DrawStrokeRpc(x, y, penSize, color);
    }

    [Rpc(SendTo.NotMe)]
    private void DrawStrokeRpc(int x, int y, int penSize, Color color)
    {
        Debug.Log($"[WhiteboardSync] 🎨 Rpc RECEIVED on remote client: ({x},{y}) — applying");
        ApplyStroke(x, y, penSize, color);
    }

    /// <summary>
    /// Apply a stroke to this whiteboard's texture WITHOUT broadcasting it on the
    /// network and WITHOUT recording it. Used by <c>WhiteboardStrokeEvent.Apply</c>
    /// during replay — every client's ReplayManager reads the same recording file,
    /// so each client applies the stroke locally; no RPC needed and double-recording
    /// would cause a feedback loop.
    /// </summary>
    public void ApplyStrokeLocal(int x, int y, int penSize, Color color)
    {
        ApplyStroke(x, y, penSize, color);
    }

    private void ApplyStroke(int x, int y, int penSize, Color color)
    {
        if (_whiteboard == null) { Debug.LogError("[WhiteboardSync] ApplyStroke: _whiteboard is NULL"); return; }
        if (_whiteboard.texture == null) { Debug.LogError("[WhiteboardSync] ApplyStroke: texture is NULL"); return; }

        int tw = (int)_whiteboard.textureSize.x;
        int th = (int)_whiteboard.textureSize.y;
        if (x < 0 || y < 0 || x + penSize > tw || y + penSize > th)
        {
            Debug.LogWarning($"[WhiteboardSync] Out of bounds: ({x},{y}) size={penSize} tex={tw}x{th}");
            return;
        }

        var colors = new Color[penSize * penSize];
        for (int i = 0; i < colors.Length; i++) colors[i] = color;
        _whiteboard.texture.SetPixels(x, y, penSize, penSize, colors);
        _whiteboard.texture.Apply();
        Debug.Log($"[WhiteboardSync] ✓ ApplyStroke at ({x},{y})");
    }
}