// AnalyticsModels.cs
// All data structures for the People Analytics panel.
// Place under: Assets/Scripts/Analytics/

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Top-level container for one meeting's full analytics.
/// </summary>
[Serializable]
public class MeetingAnalytics
{
    public string RoomName;
    public DateTime Date;
    public float DurationSeconds;
    public int ParticipantCount;

    // Summary metrics shown in the header cards
    public float TotalSpeechSeconds;
    public float AverageEngagement;   // 0-100
    public int DropsDetected;

    // Tab 1 — Participation
    public List<ParticipantStats> Participants = new List<ParticipantStats>();

    // Tab 2 — Timeline & Insights
    public List<TopicStats> Topics = new List<TopicStats>();
    public List<EngagementSample> EngagementTimeline = new List<EngagementSample>();
    public List<DropZone> Drops = new List<DropZone>();
    public List<Insight> Insights = new List<Insight>();

    public string DurationFormatted => FormatTime(DurationSeconds);
    public string TotalSpeechFormatted => FormatTime(TotalSpeechSeconds);

    public static string FormatTime(float seconds)
    {
        int total = Mathf.RoundToInt(seconds);
        int m = total / 60;
        int s = total % 60;
        return $"{m}:{s:D2}";
    }
}

/// <summary>
/// One participant's stats for a meeting.
/// </summary>
[Serializable]
public class ParticipantStats
{
    public string Id;
    public string DisplayName;
    public float SpeakTimeSeconds;
    public float SpeakPercent;          // 0-100
    public EngagementLevel Engagement;
    public int SpeakerTurns;
    public int InterruptedCount;

    /// <summary>Two-letter initials for the avatar circle (e.g. "JW").</summary>
    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) return "?";
            string[] parts = DisplayName.Trim().Split(' ');
            if (parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0)
                return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}";
            return char.ToUpper(DisplayName[0]).ToString();
        }
    }

    public string SpeakTimeFormatted => MeetingAnalytics.FormatTime(SpeakTimeSeconds);
}

public enum EngagementLevel
{
    Low,
    Medium,
    High
}

/// <summary>
/// One discussion topic and how much of the meeting it took.
/// </summary>
[Serializable]
public class TopicStats
{
    public string Name;
    public float Percent;           // 0-100
    public float DurationSeconds;

    public string DurationFormatted => MeetingAnalytics.FormatTime(DurationSeconds);
}

/// <summary>
/// One sample point on the engagement timeline.
/// </summary>
[Serializable]
public class EngagementSample
{
    public float TimeSeconds;
    public float EngagementPercent; // 0-100
}

/// <summary>
/// A time span where engagement dropped meaningfully.
/// </summary>
[Serializable]
public class DropZone
{
    public float StartSeconds;
    public float EndSeconds;

    public float DurationSeconds => EndSeconds - StartSeconds;
}

/// <summary>
/// An auto-generated finding from the analytics — surfaced to the user as a card.
/// </summary>
[Serializable]
public class Insight
{
    public InsightType Type;
    public string Headline;
    public string Detail;
}

public enum InsightType
{
    Drop,
    Recommendation
}

/// <summary>
/// Anything that can supply meeting analytics. The panel doesn't care
/// whether the data comes from mock, HTTP, or a websocket — it just asks.
/// </summary>
public interface IAnalyticsDataProvider
{
    MeetingAnalytics GetCurrentMeetingAnalytics();
}