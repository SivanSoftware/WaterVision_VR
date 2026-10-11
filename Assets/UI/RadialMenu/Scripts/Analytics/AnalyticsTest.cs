using UnityEngine;

public class AnalyticsTest : MonoBehaviour
{
    void Start()
    {
        IAnalyticsDataProvider provider = new MockAnalyticsDataProvider();
        var data = provider.GetCurrentMeetingAnalytics();

        Debug.Log($"Meeting: {data.RoomName}");
        Debug.Log($"Duration: {data.DurationFormatted} | Speech: {data.TotalSpeechFormatted} | Engagement: {data.AverageEngagement}%");
        Debug.Log($"Participants: {data.Participants.Count}, Topics: {data.Topics.Count}, Insights: {data.Insights.Count}");
        
        foreach (var p in data.Participants)
        {
            Debug.Log($"  [{p.Initials}] {p.DisplayName} — {p.SpeakTimeFormatted} ({p.SpeakPercent}%) — {p.Engagement}");
        }
    }
}