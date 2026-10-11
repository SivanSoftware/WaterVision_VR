// TopicBarView.cs
// Displays a single topic row: topic name, percentage bar, value text.
// Place under: Assets/UI/RadialMenu/Scripts/Analytics/

using TMPro;
using UnityEngine;

public class TopicBarView : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Topic name (left side, e.g., 'Pump maintenance').")]
    [SerializeField] private TextMeshProUGUI topicNameText;

    [Tooltip("Value text (right side, e.g., '42%  •  17:55').")]
    [SerializeField] private TextMeshProUGUI valueText;

    [Tooltip("Fill RectTransform inside the bar — its width is set proportional to the percentage.")]
    [SerializeField] private RectTransform barFill;

    public void Bind(TopicStats topic)
    {
        if (topicNameText != null)
            topicNameText.text = topic.Name;

        if (valueText != null)
            valueText.text = $"{Mathf.RoundToInt(topic.Percent)}%  \u2022  {topic.DurationFormatted}";

        if (barFill != null)
        {
            float fillRatio = Mathf.Clamp01(topic.Percent / 100f);
            barFill.anchorMin = new Vector2(0f, 0f);
            barFill.anchorMax = new Vector2(fillRatio, 1f);
            barFill.offsetMin = Vector2.zero;
            barFill.offsetMax = Vector2.zero;
        }
    }

    [ContextMenu("Preview With Sample Data")]
    private void PreviewWithSampleData()
    {
        Bind(new TopicStats
        {
            Name = "Pump maintenance",
            Percent = 42f,
            DurationSeconds = 1075f,
        });
    }
}