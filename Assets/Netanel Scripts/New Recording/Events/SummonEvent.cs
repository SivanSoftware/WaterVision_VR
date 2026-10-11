using UnityEngine;

namespace WaterVision.Recording.Events
{
    /// <summary>
    /// One user summoned another to their position. Captures both parties' client IDs
    /// and the summoner's position (where the summoned will be teleported). Apply()
    /// is a log marker only — replaying the teleport would conflict with the captured
    /// per-frame transforms.
    /// </summary>
    public class SummonEvent : IRecordableEvent
    {
        public string TypeKey => "Summon";
        public float Timestamp { get; set; }

        public ulong SummonerClientId;
        public Vector3 TargetPosition;

        [System.Serializable]
        private struct Payload
        {
            public ulong summonerClientId;
            public float x, y, z;
        }

        public string SerializePayload()
        {
            return JsonUtility.ToJson(new Payload
            {
                summonerClientId = SummonerClientId,
                x = TargetPosition.x,
                y = TargetPosition.y,
                z = TargetPosition.z,
            });
        }

        public void Apply()
        {
            Debug.Log($"[Replay] t={Timestamp:F2}s — SummonEvent: client {SummonerClientId} summoned others to {TargetPosition}");
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            WaterVision.Recording.WorldEventReplayer.Register("Summon", FromPayload);
        }

        private static IRecordableEvent FromPayload(string payload)
        {
            var p = JsonUtility.FromJson<Payload>(payload);
            return new SummonEvent
            {
                SummonerClientId = p.summonerClientId,
                TargetPosition = new Vector3(p.x, p.y, p.z),
            };
        }
    }
}
