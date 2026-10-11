using System;
using UnityEngine;

namespace WaterVision.ASL
{
    /// <summary>
    /// One keyframe in a sign animation. Lerped between adjacent keyframes.
    /// All positions are LOCAL to the character root.
    /// </summary>
    [Serializable]
    public struct SignKeyframe
    {
        // ── RIGHT HAND ───────────────────────────────────────────────────────
        public Vector3 rightHandLocalPos;
        public Vector3 rightHandLocalEuler;

        [Range(0, 1)] public float r_idx;
        [Range(0, 1)] public float r_mid;
        [Range(0, 1)] public float r_ring;
        [Range(0, 1)] public float r_pinky;
        [Range(0, 1)] public float r_thumb;

        // ── LEFT HAND ────────────────────────────────────────────────────────
        public Vector3 leftHandLocalPos;
        public Vector3 leftHandLocalEuler;

        [Range(0, 1)] public float l_idx;
        [Range(0, 1)] public float l_mid;
        [Range(0, 1)] public float l_ring;
        [Range(0, 1)] public float l_pinky;
        [Range(0, 1)] public float l_thumb;

        // ── NON-MANUAL MARKERS (proxy — Ch08 has no blendshapes) ─────────────
        public float headTiltDeg;   // pitch nod, ±15° typical
        public float torsoLeanDeg;  // forward/back, ±8° typical

        // ── TIMING ───────────────────────────────────────────────────────────
        /// <summary>Seconds spent at this keyframe BEFORE lerping to next.
        /// 0 = no hold, just a waypoint. 0.10-0.20 = brief pause.</summary>
        public float holdSeconds;

        /// <summary>Seconds to lerp INTO this keyframe from the previous one.
        /// First keyframe of a sign uses this as the time to lerp in from ready position.</summary>
        public float transitionSeconds;

        /// <summary>Semantic palm direction for the RIGHT hand at this keyframe.
        /// Default = use <see cref="rightHandLocalEuler"/> as-is (legacy / unauthored).
        /// Any other value OVERRIDES the raw Euler using the palm-direction table in
        /// <see cref="ASLBubbleSettings"/>, so per-sign palm orientation is one tag
        /// instead of a per-Euler number to dial. Resolves the "HELLO palm forward vs
        /// PLEASE palm inward" mismatch per the GPT validation pass.</summary>
        public PalmDir palmDirRight;

        /// <summary>Semantic palm direction for the LEFT hand at this keyframe.</summary>
        public PalmDir palmDirLeft;
    }

    /// <summary>
    /// A full sign — one English word mapped to a sequence of keyframes.
    /// </summary>
    [Serializable]
    public struct SignDefinition
    {
        /// <summary>Lowercase canonical, e.g. "hello".</summary>
        public string word;

        public SignKeyframe[] frames;

        /// <summary>1.0 = default. Higher = snappier (e.g. NO ≈ 1.4). Lower = gentler (THANK_YOU ≈ 0.8).</summary>
        public float playbackSpeed;

        /// <summary>Facial expression played for the duration of this sign. Defaults to
        /// Neutral (enum 0) so existing/un-tagged signs are unaffected. Expressed only
        /// through the avatar's real channels (smile / mouth-open / head / gaze).</summary>
        public SignEmotion emotion;

        [Multiline] public string notes;
    }

    /// <summary>Serializable string pair for the alias map (Unity can't serialize Dictionary).</summary>
    [Serializable]
    public struct StringPair
    {
        public string from;   // e.g. "running"
        public string to;     // e.g. "run"
    }
}
