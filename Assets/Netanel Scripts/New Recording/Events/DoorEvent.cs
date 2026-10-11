using UnityEngine;

namespace WaterVision.Recording.Events
{
    /// <summary>
    /// User teleported through a door at this moment. Stores the door's GameObject
    /// name (for human-readable logs) and the destination WorldArea so the timeline
    /// can be annotated with "User entered Models area".
    ///
    /// Apply() does NOT actually move any avatar — the avatar's position is already
    /// captured per-frame in the recording, so re-teleporting on replay would either
    /// be redundant (ghost) or destructive (live player). Apply() is a soft log/marker
    /// for now; a future Phase 5 polish could surface it on the replay UI's timeline.
    /// </summary>
    public class DoorEvent : IRecordableEvent
    {
        public string TypeKey => "Door";
        public float Timestamp { get; set; }

        public string DoorName;          // GameObject name of the DoorTeleporter
        public WorldArea DestinationArea;
        public ulong ClientId;           // who teleported

        [System.Serializable]
        private struct Payload
        {
            public string doorName;
            public int destinationArea;  // enum-as-int for JsonUtility
            public ulong clientId;
        }

        public string SerializePayload()
        {
            return JsonUtility.ToJson(new Payload
            {
                doorName = DoorName,
                destinationArea = (int)DestinationArea,
                clientId = ClientId,
            });
        }

        public void Apply()
        {
            // Soft marker — no live-world mutation. Future: surface on replay timeline UI.
            Debug.Log($"[Replay] t={Timestamp:F2}s — DoorEvent: client {ClientId} via '{DoorName}' → {DestinationArea}");
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            WaterVision.Recording.WorldEventReplayer.Register("Door", FromPayload);
        }

        private static IRecordableEvent FromPayload(string payload)
        {
            var p = JsonUtility.FromJson<Payload>(payload);
            return new DoorEvent
            {
                DoorName = p.doorName,
                DestinationArea = (WorldArea)p.destinationArea,
                ClientId = p.clientId,
            };
        }
    }
}
