using UnityEngine;

namespace WaterVision.Recording.Events
{
    /// <summary>
    /// User invoked "gather everyone here" at this moment. Stores the caller's
    /// network ID and the gather target position. Apply() is a log marker;
    /// re-popping the gather invitation on each remote ghost during replay would
    /// fight live state, so we don't.
    /// </summary>
    public class GatherEvent : IRecordableEvent
    {
        public string TypeKey => "Gather";
        public float Timestamp { get; set; }

        public ulong CallerClientId;
        public Vector3 TargetPosition;

        [System.Serializable]
        private struct Payload
        {
            public ulong callerClientId;
            public float x, y, z;
        }

        public string SerializePayload()
        {
            return JsonUtility.ToJson(new Payload
            {
                callerClientId = CallerClientId,
                x = TargetPosition.x,
                y = TargetPosition.y,
                z = TargetPosition.z,
            });
        }

        public void Apply()
        {
            Debug.Log($"[Replay] t={Timestamp:F2}s — GatherEvent: client {CallerClientId} called everyone to {TargetPosition}");
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            WaterVision.Recording.WorldEventReplayer.Register("Gather", FromPayload);
        }

        private static IRecordableEvent FromPayload(string payload)
        {
            var p = JsonUtility.FromJson<Payload>(payload);
            return new GatherEvent
            {
                CallerClientId = p.callerClientId,
                TargetPosition = new Vector3(p.x, p.y, p.z),
            };
        }
    }
}
