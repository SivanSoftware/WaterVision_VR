// InsightCardView.cs
// Displays a single insight card with a headline, detail, and a type-colored side bar.
// Place under: Assets/UI/RadialMenu/Scripts/Analytics/

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InsightCardView : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Bold headline text at the top of the card.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Multi-line detail / description text below the headline.")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Tooltip("Thin colored bar on the left edge — indicates insight type.")]
    [SerializeField] private Image typeIndicator;

    [Header("Type Colors")]
    [Tooltip("Color for Drop-type insights (engagement drops, alerts).")]
    [SerializeField] private Color dropColor = new Color(0.91f, 0.30f, 0.24f); // #E74C3C red
    [Tooltip("Color for Recommendation-type insights (suggestions, tips).")]
    [SerializeField] private Color recommendationColor = new Color(0.13f, 0.59f, 0.95f); // #2196F3 blue

    /// <summary>
    /// Set the displayed insight.
    /// </summary>
    public void Bind(Insight insight)
    {
        if (titleText != null) titleText.text = insight.Headline;
        if (descriptionText != null) descriptionText.text = insight.Detail;
        if (typeIndicator != null) typeIndicator.color = GetColorForType(insight.Type);
    }

    private Color GetColorForType(InsightType type)
    {
        switch (type)
        {
            case InsightType.Drop: return dropColor;
            case InsightType.Recommendation: return recommendationColor;
            default: return Color.gray;
        }
    }

    [ContextMenu("Preview With Sample Data")]
    private void PreviewWithSampleData()
    {
        Bind(new Insight
        {
            Type = InsightType.Drop,
            Headline = "Drop at 12:00 — engagement fell to 30%",
            Detail = "Sarah and Maya disengaged during the cost discussion. Lasted 3:30."
        });
    }
}