namespace WaterVision.Recording.Events
{
    /// <summary>
    /// Marker contract for a "world event" that can be recorded during a session and
    /// played back later — door opens, whiteboard stroke, summon, etc.
    ///
    /// Event lifecycle:
    ///   1. Gameplay system creates an instance (e.g. <c>new DoorEvent { DoorId="A", IsOpen=true }</c>).
    ///   2. Calls <c>WorldEventRecorder.Record(evt)</c>. The recorder stamps a timestamp and
    ///      serializes the payload via <see cref="SerializePayload"/>.
    ///   3. On replay, <c>WorldEventReplayer.Deserialize(typeKey, payload, timestamp)</c>
    ///      reconstructs the instance via a registered factory and calls <see cref="Apply"/>
    ///      at the right moment in the timeline.
    ///
    /// Each concrete event type MUST register a factory via <c>WorldEventReplayer.Register</c>
    /// (typically with <c>[RuntimeInitializeOnLoadMethod]</c>) so it is discoverable on replay.
    /// </summary>
    public interface IRecordableEvent
    {
        /// <summary>
        /// Stable string key for this event type (e.g. "Door", "Whiteboard.Stroke").
        /// Used to look up the factory on replay. Must be unique across event types and
        /// stable across project versions, since it lands in stored recording files.
        /// </summary>
        string TypeKey { get; }

        /// <summary>
        /// Seconds since recording start. Set by the recorder when the event is captured;
        /// read by the replayer to know when to fire.
        /// </summary>
        float Timestamp { get; set; }

        /// <summary>
        /// Serialize event-specific fields to a string (typically JSON via
        /// <see cref="UnityEngine.JsonUtility"/>). Must round-trip with the factory
        /// registered for this <see cref="TypeKey"/>.
        /// </summary>
        string SerializePayload();

        /// <summary>
        /// Re-apply this event to the live world during replay. Should be idempotent
        /// where reasonable. Live gameplay systems should respect
        /// <see cref="WorldEventReplayer.IsReplaying"/> to avoid fighting replay state.
        /// </summary>
        void Apply();
    }
}
