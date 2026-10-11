// MockAnalyticsDataProvider.cs
// Hardcoded data matching the V3 mockup. When the team's backend (Speech-to-Text + audit
// pipeline, tasks 6.7.1 - 6.7.4) is ready, swap this for a real provider — no UI changes needed.
// Place under: Assets/Scripts/Analytics/

using System;
using System.Collections.Generic;

public class MockAnalyticsDataProvider : IAnalyticsDataProvider
{
    public MeetingAnalytics GetCurrentMeetingAnalytics()
    {
        return new MeetingAnalytics
        {
            RoomName = "Water team weekly sync",
            Date = new DateTime(2026, 11, 5),
            DurationSeconds = 2843f,    // 47:23
            ParticipantCount = 5,

            TotalSpeechSeconds = 2563f, // 42:43
            AverageEngagement = 73f,
            DropsDetected = 2,

            Participants = new List<ParticipantStats>
            {
                new ParticipantStats
                {
                    Id = "u_jw",
                    DisplayName = "James Wilson",
                    SpeakTimeSeconds = 861f, // 14:21
                    SpeakPercent = 30.3f,
                    Engagement = EngagementLevel.High,
                    SpeakerTurns = 28,
                    InterruptedCount = 0,
                },
                new ParticipantStats
                {
                    Id = "u_sm",
                    DisplayName = "Sarah Mitchell",
                    SpeakTimeSeconds = 754f, // 12:34
                    SpeakPercent = 26.5f,
                    Engagement = EngagementLevel.High,
                    SpeakerTurns = 24,
                    InterruptedCount = 1,
                },
                new ParticipantStats
                {
                    Id = "u_dc",
                    DisplayName = "David Chen",
                    SpeakTimeSeconds = 492f, // 8:12
                    SpeakPercent = 17.3f,
                    Engagement = EngagementLevel.Medium,
                    SpeakerTurns = 18,
                    InterruptedCount = 2,
                },
                new ParticipantStats
                {
                    Id = "u_mg",
                    DisplayName = "Maya Goldberg",
                    SpeakTimeSeconds = 405f, // 6:45
                    SpeakPercent = 14.2f,
                    Engagement = EngagementLevel.Medium,
                    SpeakerTurns = 14,
                    InterruptedCount = 1,
                },
                new ParticipantStats
                {
                    Id = "u_rb",
                    DisplayName = "Rachel Brooks",
                    SpeakTimeSeconds = 331f, // 5:31
                    SpeakPercent = 11.7f,
                    Engagement = EngagementLevel.Low,
                    SpeakerTurns = 9,
                    InterruptedCount = 4,
                },
            },

            Topics = new List<TopicStats>
            {
                new TopicStats { Name = "Pump maintenance",          Percent = 42f, DurationSeconds = 1075f },
                new TopicStats { Name = "pH levels & water quality", Percent = 24f, DurationSeconds = 614f  },
                new TopicStats { Name = "Budget approval Q1",        Percent = 18f, DurationSeconds = 461f  },
                new TopicStats { Name = "Team scheduling",           Percent = 10f, DurationSeconds = 256f  },
                new TopicStats { Name = "Other",                     Percent = 6f,  DurationSeconds = 157f  },
            },

            EngagementTimeline = BuildEngagementTimeline(),

            Drops = new List<DropZone>
            {
                new DropZone { StartSeconds = 720f,  EndSeconds = 930f  }, // 12:00 - 15:30
                new DropZone { StartSeconds = 1905f, EndSeconds = 2060f }, // 31:45 - 34:20
            },

            Insights = new List<Insight>
            {
                new Insight
                {
                    Type     = InsightType.Drop,
                    Headline = "Drop at 12:00 — engagement fell to 30%",
                    Detail   = "Sarah and Maya disengaged during the cost discussion. Lasted 3:30.",
                },
                new Insight
                {
                    Type     = InsightType.Drop,
                    Headline = "Drop at 31:45 — engagement fell to 25%",
                    Detail   = "Possible loss of focus before the action items were assigned. Lasted 2:35.",
                },
                new Insight
                {
                    Type     = InsightType.Recommendation,
                    Headline = "Rachel was interrupted 4× — more than anyone else",
                    Detail   = "May explain her below-average participation (11.7%). Consider giving her more uninterrupted floor time.",
                },
            },
        };
    }

    /// <summary>
    /// Generates the engagement curve sample points. (time-in-minutes, engagement-%).
    /// Two synthetic dips align with the DropZones above.
    /// </summary>
    private static List<EngagementSample> BuildEngagementTimeline()
    {
        var raw = new (float minutes, float pct)[]
        {
            (0f,    60f),
            (3f,    80f),
            (6f,    85f),
            (9f,    75f),
            (12f,   50f),  // drop 1 starts
            (13.5f, 30f),
            (15.5f, 60f),  // drop 1 ends
            (18f,   80f),
            (21f,   90f),
            (24f,   85f),
            (27f,   80f),
            (30f,   70f),
            (31.75f, 45f), // drop 2 starts
            (33f,   25f),
            (34.3f, 55f),  // drop 2 ends
            (37f,   75f),
            (40f,   85f),
            (43f,   90f),
            (47.4f, 80f),
        };

        var list = new List<EngagementSample>(raw.Length);
        foreach (var p in raw)
        {
            list.Add(new EngagementSample
            {
                TimeSeconds = p.minutes * 60f,
                EngagementPercent = p.pct,
            });
        }
        return list;
    }
}