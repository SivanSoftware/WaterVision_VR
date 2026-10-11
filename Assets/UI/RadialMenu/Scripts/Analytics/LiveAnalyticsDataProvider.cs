using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using XRMultiplayer;

/// <summary>
/// Collects real per-player analytics during a live multiplayer session.
/// Add this component to the same GameObject as PeopleAnalyticsPanelController.
/// </summary>
public class LiveAnalyticsDataProvider : MonoBehaviour, IAnalyticsDataProvider
{
    [Header("Detection Tuning")]
    [Tooltip("Voice amplitude above which a player is considered 'speaking'.")]
    [Range(0f, 1f)] public float voiceActivityThreshold = 0.05f;

    [Tooltip("How often (seconds) to record an engagement sample.")]
    public float engagementSampleInterval = 5f;

    [Tooltip("Engagement % below which a window counts as a 'drop'.")]
    public float dropThresholdPercent = 30f;

    [Tooltip("Minimum dropp duration (seconds) to be reported.")]
    public float minDropDuration = 15f;

    [Tooltip("If a speaker starts while another is speaking within this window, count as interruption.")]
    public float interruptionWindowSeconds = 0.3f;

    private float _meetingStartTime;
    private float _nextSampleTime;
    private readonly Dictionary<ulong, ParticipantTracking> _data = new();
    private readonly List<EngagementSample> _engagementSamples = new();
    private readonly List<DropZone> _drops = new();
    private float? _currentDropStart;
    private ulong? _lastSpeakerId;
    private float _lastSpeakerStopTime;

    private class ParticipantTracking
    {
        public string Name;
        public float SpeakTime;
        public int SpeakerTurns;
        public int InterruptedCount;
        public bool WasSpeakingLastFrame;
    }

    void Start()
    {
        _meetingStartTime = Time.time;
        _nextSampleTime = Time.time + engagementSampleInterval;
    }

    void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
            return;

        var players = FindObjectsOfType<XRINetworkPlayer>();
        int speakingCount = 0;
        ulong? currentSpeakerThisFrame = null;

        foreach (var player in players)
        {
            ulong id = player.OwnerClientId;
            if (!_data.TryGetValue(id, out var data))
            {
                data = new ParticipantTracking { Name = string.IsNullOrEmpty(player.playerName) ? $"Player{id}" : player.playerName };
                _data[id] = data;
            }
            // Refresh display name (might be set after spawn)
            if (!string.IsNullOrEmpty(player.playerName)) data.Name = player.playerName;

            bool speakingNow = player.playerVoiceAmp > voiceActivityThreshold;
            if (speakingNow)
            {
                data.SpeakTime += Time.deltaTime;
                speakingCount++;
                currentSpeakerThisFrame = id;
                if (!data.WasSpeakingLastFrame)
                {
                    data.SpeakerTurns++;
                    // Interruption: speaker started while previous speaker was recently active and is also still speaking
                    if (_lastSpeakerId.HasValue && _lastSpeakerId.Value != id && Time.time - _lastSpeakerStopTime < interruptionWindowSeconds)
                    {
                        if (_data.TryGetValue(_lastSpeakerId.Value, out var interrupted))
                            interrupted.InterruptedCount++;
                    }
                }
            }
            else if (data.WasSpeakingLastFrame)
            {
                _lastSpeakerId = id;
                _lastSpeakerStopTime = Time.time;
            }
            data.WasSpeakingLastFrame = speakingNow;
        }

        // Sample engagement at intervals
        if (Time.time >= _nextSampleTime)
        {
            float engagementPct = players.Length > 0 ? (float)speakingCount / players.Length * 100f : 0f;
            _engagementSamples.Add(new EngagementSample
            {
                TimeSeconds = Time.time - _meetingStartTime,
                EngagementPercent = engagementPct,
            });

            // Drop tracking
            if (engagementPct < dropThresholdPercent)
            {
                if (!_currentDropStart.HasValue)
                    _currentDropStart = Time.time - _meetingStartTime;
            }
            else
            {
                if (_currentDropStart.HasValue)
                {
                    float end = Time.time - _meetingStartTime;
                    if (end - _currentDropStart.Value >= minDropDuration)
                        _drops.Add(new DropZone { StartSeconds = _currentDropStart.Value, EndSeconds = end });
                    _currentDropStart = null;
                }
            }

            _nextSampleTime = Time.time + engagementSampleInterval;
        }
    }

    public MeetingAnalytics GetCurrentMeetingAnalytics()
    {
        // Close out an in-progress drop for reporting (don't mutate persistent state)
        var drops = new List<DropZone>(_drops);
        if (_currentDropStart.HasValue)
        {
            float end = Time.time - _meetingStartTime;
            if (end - _currentDropStart.Value >= minDropDuration)
                drops.Add(new DropZone { StartSeconds = _currentDropStart.Value, EndSeconds = end });
        }

        float duration = Time.time - _meetingStartTime;
        float totalSpeech = _data.Values.Sum(d => d.SpeakTime);
        float avgEngagement = _engagementSamples.Count > 0 ? _engagementSamples.Average(s => s.EngagementPercent) : 0f;

        var participants = _data
            .OrderByDescending(kv => kv.Value.SpeakTime)
            .Select(kv => new ParticipantStats
            {
                Id = kv.Key.ToString(),
                DisplayName = kv.Value.Name,
                SpeakTimeSeconds = kv.Value.SpeakTime,
                SpeakPercent = totalSpeech > 0 ? (kv.Value.SpeakTime / totalSpeech) * 100f : 0f,
                Engagement = ClassifyEngagement(totalSpeech > 0 ? (kv.Value.SpeakTime / totalSpeech) * 100f : 0f),
                SpeakerTurns = kv.Value.SpeakerTurns,
                InterruptedCount = kv.Value.InterruptedCount,
            })
            .ToList();

        return new MeetingAnalytics
        {
            RoomName = XRINetworkGameManager.ConnectedRoomName != null && !string.IsNullOrEmpty(XRINetworkGameManager.ConnectedRoomName.Value)
                ? XRINetworkGameManager.ConnectedRoomName.Value
                : "VR Meeting",
            Date = System.DateTime.Now,
            DurationSeconds = duration,
            ParticipantCount = participants.Count,
            TotalSpeechSeconds = totalSpeech,
            AverageEngagement = avgEngagement,
            DropsDetected = drops.Count,
            Participants = participants,
            Topics = new List<TopicStats>(), // Empty — requires NLP on STT transcripts (future)
            EngagementTimeline = new List<EngagementSample>(_engagementSamples),
            Drops = drops,
            Insights = GenerateInsights(participants, drops),
        };
    }

    private EngagementLevel ClassifyEngagement(float speakPercent)
    {
        if (speakPercent > 20f) return EngagementLevel.High;
        if (speakPercent > 10f) return EngagementLevel.Medium;
        return EngagementLevel.Low;
    }

    private List<Insight> GenerateInsights(List<ParticipantStats> participants, List<DropZone> drops)
    {
        var insights = new List<Insight>();

        foreach (var d in drops)
        {
            int min = Mathf.FloorToInt(d.StartSeconds / 60f);
            int sec = Mathf.FloorToInt(d.StartSeconds % 60f);
            insights.Add(new Insight
            {
                Type = InsightType.Drop,
                Headline = $"Drop at {min:00}:{sec:00}",
                Detail = $"Engagement dropped. Lasted {MeetingAnalytics.FormatTime(d.DurationSeconds)}.",
            });
        }

        var mostInterrupted = participants
            .Where(p => p.InterruptedCount > 0)
            .OrderByDescending(p => p.InterruptedCount)
            .FirstOrDefault();
        if (mostInterrupted != null && mostInterrupted.InterruptedCount >= 3)
        {
            insights.Add(new Insight
            {
                Type = InsightType.Recommendation,
                Headline = $"{mostInterrupted.DisplayName} was interrupted {mostInterrupted.InterruptedCount}× — more than anyone else",
                Detail = $"Consider giving them more uninterrupted floor time (their participation: {mostInterrupted.SpeakPercent:F1}%).",
            });
        }

        var quietest = participants.Where(p => p.SpeakPercent < 5f).ToList();
        if (quietest.Count > 0)
        {
            insights.Add(new Insight
            {
                Type = InsightType.Recommendation,
                Headline = $"{quietest.Count} participant(s) barely spoke (< 5%)",
                Detail = string.Join(", ", quietest.Select(p => p.DisplayName)),
            });
        }

        return insights;
    }

    [ContextMenu("Reset Analytics")]
    public void Reset()
    {
        _meetingStartTime = Time.time;
        _nextSampleTime = Time.time + engagementSampleInterval;
        _data.Clear();
        _engagementSamples.Clear();
        _drops.Clear();
        _currentDropStart = null;
    }
}