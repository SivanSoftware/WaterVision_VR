using System;
using System.Collections.Generic;
using UnityEngine;
using WaterVision.Recording.Events;

namespace WaterVision.Recording
{
    /// <summary>
    /// Registry for world-event type factories + global "is replay active" flag.
    ///
    /// Each concrete <see cref="IRecordableEvent"/> registers a factory that knows how
    /// to deserialize a payload string back into an instance. The replayer (ReplayManager)
    /// calls <see cref="Deserialize"/> at each event's timestamp and dispatches
    /// <see cref="IRecordableEvent.Apply"/>.
    ///
    /// Live gameplay systems should check <see cref="IsReplaying"/> and suppress their
    /// own input/networking while a replay is running — otherwise the replay's
    /// <c>Apply</c> calls fight the live system. ReplayManager toggles this flag on
    /// LoadAndPlay / StopReplay.
    /// </summary>
    public static class WorldEventReplayer
    {
        private static readonly Dictionary<string, Func<string, IRecordableEvent>> s_Factories =
            new Dictionary<string, Func<string, IRecordableEvent>>();

        /// <summary>
        /// True while a recording is being played back. Gameplay scripts should treat
        /// this as "freeze live input" — Apply() calls from the replayer should be the
        /// only thing changing world state for replayed systems during this window.
        /// </summary>
        public static bool IsReplaying { get; private set; }

        /// <summary>Internal: set by ReplayManager. Do not call from gameplay code.</summary>
        public static void SetReplayActive(bool active) => IsReplaying = active;

        /// <summary>
        /// Register a factory for a given <paramref name="typeKey"/>. Idempotent —
        /// re-registering the same key replaces the previous factory (useful when
        /// scripts get reloaded in the editor).
        /// </summary>
        public static void Register(string typeKey, Func<string, IRecordableEvent> factory)
        {
            if (string.IsNullOrEmpty(typeKey) || factory == null) return;
            s_Factories[typeKey] = factory;
        }

        /// <summary>
        /// Resolve a stored event back into an <see cref="IRecordableEvent"/> instance.
        /// Returns null + logs a warning if no factory is registered for the key
        /// (e.g. recording was made by a newer build with event types this build doesn't know).
        /// </summary>
        public static IRecordableEvent Deserialize(string typeKey, string payload, float timestamp)
        {
            if (!s_Factories.TryGetValue(typeKey, out var factory))
            {
                Debug.LogWarning($"[WorldEventReplayer] No factory registered for event typeKey '{typeKey}' — event at t={timestamp:F2}s skipped.");
                return null;
            }

            try
            {
                var evt = factory(payload);
                if (evt != null) evt.Timestamp = timestamp;
                return evt;
            }
            catch (Exception e)
            {
                Debug.LogError($"[WorldEventReplayer] Factory for '{typeKey}' threw on payload '{payload}': {e.Message}");
                return null;
            }
        }
    }
}
