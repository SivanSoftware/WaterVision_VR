// MetricCardView.cs
// Displays a single metric card with a big value and a small label.
// Place under: Assets/UI/RadialMenu/Scripts/Analytics/

using TMPro;
using UnityEngine;

public class MetricCardView : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Big number / value text (e.g., '42:43', '73%', '2').")]
    [SerializeField] private TextMeshProUGUI valueText;

    [Tooltip("Small label text under the value (e.g., 'Total Speech').")]
    [SerializeField] private TextMeshProUGUI labelText;

    /// <summary>
    /// Set the displayed value and label.
    /// </summary>
    public void Bind(string value, string label)
    {
        if (valueText != null) valueText.text = value;
        if (labelText != null) labelText.text = label;
    }

    [ContextMenu("Preview With Sample Data")]
    private void PreviewWithSampleData()
    {
        Bind("42:43", "Total Speech");
    }
}