using System;
using UnityEngine;

namespace XRMultiplayer
{
    [Serializable]
    public class WastewaterStation
    {
        [Tooltip("Stage label, e.g. שלב א' (Stage A).")]
        public string nameHebrew;

        [Tooltip("English fallback label, e.g. 'Stage A — Pre-Treatment'.")]
        public string nameEnglish;

        [TextArea(3, 8)]
        [Tooltip("Short Hebrew explanation shown on the floating panel.")]
        public string description;

        [Tooltip("Root GameObject of this station in the plant prefab. Used as fallback waypoint anchor and for highlight.")]
        public Transform stationRoot;

        [Tooltip("Camera waypoints the guided tour passes through. Player teleports/lerps along them in order.")]
        public Transform[] tourWaypoints;

        [Tooltip("Where the floating StationInfoPanel sits (world-space). If null, falls back to stationRoot.")]
        public Transform infoPanelAnchor;

        [Tooltip("Particle systems played when this station is activated. Stopped when it ends.")]
        public ParticleSystem[] activationParticles;

        [Tooltip("Animators triggered when this station is activated.")]
        public Animator[] stationAnimators;

        [Tooltip("Trigger name fired on each animator. Leave blank to skip animators.")]
        public string activationTriggerName = "Activate";

        [Tooltip("Seconds to dwell at each waypoint (excluding travel time).")]
        public float dwellPerWaypoint = 4f;

        [Tooltip("Seconds to travel between consecutive waypoints.")]
        public float travelTimeBetweenWaypoints = 2f;

        // ============ Microscale ride (for inside-the-pipe immersion) ============

        [Header("Microscale Ride")]
        [Tooltip("If true, the player rig is shrunk to microscopic size at tour start and restored at tour end (Stage 1 inside-the-pipe immersion). Mirrors WaterProcessSim's microscopic effect.")]
        public bool useMicroscale = false;

        [Tooltip("Target scale of the player rig at microscopic phase. 0.03 = 3% of normal size.")]
        public float microScale = 0.03f;

        [Tooltip("Seconds to lerp the rig between original scale and micro scale at tour start/end.")]
        public float scaleTransitionDuration = 1.5f;
    }
}
