using System.Collections.Generic;
using UnityEngine;

namespace WaterVision.Recording
{
    /// <summary>
    /// המעטפת הראשית של קובץ ההקלטה.
    /// אובייקט זה יומר ל-JSON וישמר בדיסק.
    /// </summary>
    [System.Serializable]
    public class RecordingSession
    {
        // Format version. v1: head root only. v2: head + both hands + per-user identity + join times.
        public int version = 2;

        // --- מטא-דאטה (מידע כללי) ---
        public string sessionID;        // מזהה ייחודי (למשל: "2026_01_15_WaterTeam")
        public string date;             // תאריך הקלטה בפורמט קריא
        public float totalDuration;     // אורך ההקלטה בשניות

        // OwnerClientId of whoever pressed Record. 0 if unknown / offline.
        public ulong recorderClientId;

        // --- שחקנים (מי היה בחדר?) ---
        // נשתמש בזה בטעינה כדי לדעת איזה צבע/סקין לתת לכל Ghost
        public List<ParticipantInfo> participants = new List<ParticipantInfo>();

        // --- התנועה (Continuous Data) ---
        // נשמר בקצב של 30Hz (כל 0.033 שניות)
        public List<RecordingFrame> frames = new List<RecordingFrame>();

        // --- World events (discrete) ---
        // v2: extensible event format. Each entry has a typeKey (e.g. "Door",
        // "Whiteboard.Stroke") and a JSON payload string. The replayer resolves
        // the typeKey to a registered factory and reconstructs the event.
        // Stored in chronological order so the replayer can advance a cursor.
        public List<RecordedWorldEvent> worldEvents = new List<RecordedWorldEvent>();
    }

    /// <summary>
    /// מידע סטטי על משתתף (לא משתנה במהלך ההקלטה)
    /// </summary>
    [System.Serializable]
    public class ParticipantInfo
    {
        public string networkId;    // המזהה הייחודי ברשת (ClientID / VoiceID)
        public string displayName;  // השם שמופיע מעל הראש
        public int avatarColorId;   // legacy: kept for v1 backwards compatibility
        public Color colorRGB = Color.white;        // v2: real player tint, used by replay
        public float joinedAtTimestamp = 0f;         // v2: when this participant entered the recording (0 = present at start)
    }

    /// <summary>
    /// "פריים" בודד בזמן. מכיל את המיקום של כל השחקנים ברגע נתון.
    /// </summary>
    [System.Serializable]
    public class RecordingFrame
    {
        public float timestamp; // זמן מתחילת ההקלטה (בשניות)
        public List<AvatarPose> avatarPoses = new List<AvatarPose>();
    }

    /// <summary>
    /// המיקום והסיבוב של שחקן ספציפי בתוך הפריים
    /// </summary>
    [System.Serializable]
    public class AvatarPose
    {
        public string networkId; // שיוך למשתתף

        // Head
        public Vector3 headPos;
        public Quaternion headRot;
        
        // Left Hand
        public Vector3 lHandPos;
        public Quaternion lHandRot;
        
        // Right Hand
        public Vector3 rHandPos;
        public Quaternion rHandRot;

        // אופציונלי: האם הוא מדבר כרגע? (עבור אנימציית פה)
        public bool isSpeaking;
    }

    /// <summary>
    /// Generic recorded event entry. Replaces the v1 fixed-schema WorldEvent.
    /// <c>payload</c> is a JSON string carrying event-specific fields; the replayer
    /// resolves <c>typeKey</c> to a registered factory to reconstruct the
    /// concrete <see cref="WaterVision.Recording.Events.IRecordableEvent"/> on playback.
    /// </summary>
    [System.Serializable]
    public class RecordedWorldEvent
    {
        public string typeKey;   // e.g. "Door", "Whiteboard.Stroke", "Gather", "Summon"
        public float timestamp;  // seconds since recording start
        public string payload;   // event-specific fields, JSON-encoded
    }
}