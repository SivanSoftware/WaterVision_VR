// ParticipantRowView.cs
// Visual representation of one participant in the analytics panel.
// Place under: Assets/UI/RadialMenu/Scripts/Analytics/

using TMPro;
using UnityEngine;

public class ParticipantRowView : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The single text element that displays all participant info.")]
    [SerializeField] private TextMeshProUGUI mainText;

    /// <summary>
    /// Populate this row with a participant's data.
    /// Called by the panel controller when the panel is shown.
    /// </summary>
    public void Bind(ParticipantStats stats)
    {
        if (mainText == null)
        {
            Debug.LogWarning("[ParticipantRow] mainText is not assigned!");
            return;
        }

        if (stats == null)
        {
            mainText.text = "(no data)";
            return;
        }

        // Build a formatted string with the participant's info.
        // Uses TMP rich-text tags for bold initials and a colored engagement label.
        string engagementColorHex = GetEngagementColorHex(stats.Engagement);

        mainText.text =
            $"<b>[{stats.Initials}]</b>  <b>{stats.DisplayName}</b>\n" +
            $"<size=70%>{stats.SpeakTimeFormatted} ({stats.SpeakPercent:F1}%)  •  " +
            $"<color={engagementColorHex}><b>{stats.Engagement}</b></color>  •  " +
            $"{stats.SpeakerTurns} turns  •  {stats.InterruptedCount} interrupted</size>";
    }

    /// <summary>
    /// Color codes per engagement level — green / amber / red.
    /// </summary>
    private string GetEngagementColorHex(EngagementLevel level)
    {
        switch (level)
        {
            case EngagementLevel.High: return "#33A852";   // green
            case EngagementLevel.Medium: return "#F5A623"; // amber
            case EngagementLevel.Low: return "#E74C3C";    // red
            default: return "#888888";
        }
    }

    /// <summary>
    /// Quick test method — shows what a row looks like with hardcoded data.
    /// Right-click the component and pick "Preview With Sample Data" to test.
    /// </summary>
    [ContextMenu("Preview With Sample Data")]
    private void PreviewSample()
    {
        var sample = new ParticipantStats
        {
            Id = "u_jw",
            DisplayName = "James Wilson",
            SpeakTimeSeconds = 861f,
            SpeakPercent = 30.3f,
            Engagement = EngagementLevel.High,
            SpeakerTurns = 28,
            InterruptedCount = 0
        };
        Bind(sample);
    }
}