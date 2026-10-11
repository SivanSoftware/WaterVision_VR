using WaterVision.Recording.Events;

namespace WaterVision.Recording
{
    /// <summary>
    /// Static façade for emitting world events. Gameplay systems call
    /// <see cref="Record"/> unconditionally — the call is a cheap no-op when no
    /// recording is active, so call sites can stay always-on.
    ///
    /// Example from a door controller:
    /// <code>
    /// void SetOpen(bool open) {
    ///     m_IsOpen = open;
    ///     WorldEventRecorder.Record(new DoorEvent { DoorId = m_Id, IsOpen = open });
    /// }
    /// </code>
    /// </summary>
    public static class WorldEventRecorder
    {
        /// <summary>
        /// Append an event to the active recording. No-op when no recording is in progress.
        /// The recorder stamps the timestamp; callers must not pre-populate it.
        /// </summary>
        public static void Record(IRecordableEvent evt)
        {
            if (evt == null) return;
            // Never record during a replay — Apply() calls from the replayer could
            // otherwise re-enter the recording path and create a self-amplifying
            // loop in any system that records the same action it just applied.
            if (WorldEventReplayer.IsReplaying) return;
            var recorder = SpatialRecorder.Instance;
            if (recorder == null || !recorder.isRecording) return;
            recorder.RecordEvent(evt);
        }
    }
}
