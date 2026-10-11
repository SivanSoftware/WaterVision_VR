using UnityEngine;

/// <summary>
/// Arranges immediate children of this GameObject in a circular pattern.
/// Children whose name starts with the skip prefix (default "_") are ignored,
/// allowing for a central hub or other non-radial elements.
/// </summary>
[ExecuteAlways]
public class RadialLayout : MonoBehaviour
{
    [Header("Radial Layout Settings")]

    [Tooltip("Distance from center to each child button (in canvas units)")]
    [SerializeField] private float radius = 280f;

    [Tooltip("Starting angle in degrees. 90 = top of circle, 0 = right, 270 = bottom")]
    [Range(0f, 360f)]
    [SerializeField] private float startAngle = 90f;

    [Tooltip("Direction: true = clockwise, false = counter-clockwise")]
    [SerializeField] private bool clockwise = true;

    [Header("Skip Settings")]

    [Tooltip("Children whose name STARTS with this prefix will NOT be arranged. Use for the central hub.")]
    [SerializeField] private string skipPrefix = "_";

    void Update()
    {
        ArrangeChildren();
    }

    private void ArrangeChildren()
    {
        // First pass: count only children that should be arranged
        int arrangeCount = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            if (ShouldArrange(transform.GetChild(i)))
                arrangeCount++;
        }

        if (arrangeCount == 0) return;

        float angleStep = 360f / arrangeCount;
        if (clockwise) angleStep = -angleStep;

        // Second pass: position each child
        int positionIndex = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!ShouldArrange(child)) continue;

            RectTransform rt = child as RectTransform;
            if (rt == null) continue;

            float angle = startAngle + (angleStep * positionIndex);
            float rad = angle * Mathf.Deg2Rad;

            Vector2 pos = new Vector2(
                Mathf.Cos(rad) * radius,
                Mathf.Sin(rad) * radius
            );

            rt.anchoredPosition = pos;
            positionIndex++;
        }
    }

    private bool ShouldArrange(Transform child)
    {
        if (!string.IsNullOrEmpty(skipPrefix) && child.name.StartsWith(skipPrefix))
            return false;
        return true;
    }
}