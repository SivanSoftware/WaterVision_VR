using Unity.Netcode;

namespace WaterVision.Recording
{
    /// <summary>
    /// Replicated snapshot of "who is currently recording, and since when". Lives as a
    /// <see cref="NetworkVariable{T}"/> on the SpatialRecorder once it is upgraded to a
    /// NetworkBehaviour. Non-recorders use this to surface a "Recording by &lt;user&gt;"
    /// banner and to hide the Stop button.
    ///
    /// Server time (not local Time.time) is used so all clients agree on when the
    /// session started — relevant if Phase 3's synced playback ever wants to coordinate
    /// frame offsets.
    /// </summary>
    [System.Serializable]
    public struct RecorderStateData : INetworkSerializable
    {
        public bool isRecording;
        public ulong recorderClientId;
        public double sessionStartServerTime;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref isRecording);
            serializer.SerializeValue(ref recorderClientId);
            serializer.SerializeValue(ref sessionStartServerTime);
        }
    }
}
