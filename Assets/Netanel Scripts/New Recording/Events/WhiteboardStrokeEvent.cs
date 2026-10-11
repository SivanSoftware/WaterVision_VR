using UnityEngine;

namespace WaterVision.Recording.Events
{
    /// <summary>
    /// One whiteboard stroke at one pixel cell. Mirrors the parameters of
    /// <c>WhiteboardSync.DrawStroke</c>: pixel x/y, pen size, color.
    ///
    /// On replay, applies the stroke directly to the local Whiteboard texture
    /// via <see cref="WhiteboardSync.ApplyStrokeLocal"/> (no network propagation —
    /// every client's ReplayManager is reading the same recording file).
    /// </summary>
    public class WhiteboardStrokeEvent : IRecordableEvent
    {
        public string TypeKey => "Whiteboard.Stroke";
        public float Timestamp { get; set; }

        public int X;
        public int Y;
        public int PenSize;
        public Color Color;

        // ── JsonUtility-compatible serialization payload ─────────────────────
        // JsonUtility can't serialize an interface directly, but it CAN serialize
        // a [Serializable] struct. We round-trip through this payload type.
        [System.Serializable]
        private struct Payload
        {
            public int x;
            public int y;
            public int penSize;
            public float r, g, b, a;
        }

        public string SerializePayload()
        {
            var p = new Payload
            {
                x = X,
                y = Y,
                penSize = PenSize,
                r = Color.r,
                g = Color.g,
                b = Color.b,
                a = Color.a,
            };
            return JsonUtility.ToJson(p);
        }

        public void Apply()
        {
            // Find any WhiteboardSync in the scene. Typically there's exactly one;
            // if there are several, all of them get the same stroke (which is
            // probably what you want for replay of a "global whiteboard").
            var syncs = Object.FindObjectsByType<WhiteboardSync>(FindObjectsSortMode.None);
            if (syncs == null || syncs.Length == 0) return;
            for (int i = 0; i < syncs.Length; i++)
                syncs[i].ApplyStrokeLocal(X, Y, PenSize, Color);
        }

        // ── Factory registration ─────────────────────────────────────────────

        [UnityEngine.RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            WaterVision.Recording.WorldEventReplayer.Register("Whiteboard.Stroke", FromPayload);
        }

        private static IRecordableEvent FromPayload(string payload)
        {
            var p = JsonUtility.FromJson<Payload>(payload);
            return new WhiteboardStrokeEvent
            {
                X = p.x,
                Y = p.y,
                PenSize = p.penSize,
                Color = new Color(p.r, p.g, p.b, p.a),
            };
        }
    }
}
