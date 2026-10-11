// EngagementChartGraphic.cs
// Custom UI Graphic that renders an engagement curve as a polyline with
// red drop-zone rectangles behind it. Designed to be attached to a single
// UI GameObject — no children needed.
// Place under: Assets/UI/RadialMenu/Scripts/Analytics/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class EngagementChartGraphic : MaskableGraphic
{
    [Header("Visual Settings")]
    [Tooltip("Color of the engagement curve.")]
    [SerializeField] private Color lineColor = new Color(0.22f, 0.54f, 0.87f); // #378ADD blue
    [Tooltip("Color of the drop zone rectangles (use alpha to make them transparent).")]
    [SerializeField] private Color dropZoneColor = new Color(0.91f, 0.30f, 0.24f, 0.18f); // red w/ alpha
    [Tooltip("Thickness of the line in pixels.")]
    [SerializeField] private float lineThickness = 2.5f;
    [Tooltip("Padding inside the rect (left, right, top, bottom).")]
    [SerializeField] private RectOffset padding;

    // Normalized data (0..1 in both axes) — set by SetData().
    private readonly List<Vector2> _points = new List<Vector2>();
    private readonly List<Vector2> _dropZonesNormX = new List<Vector2>(); // (xStart, xEnd)

    /// <summary>
    /// Update the chart with the current meeting data.
    /// </summary>
    public void SetData(MeetingAnalytics data)
    {
        _points.Clear();
        _dropZonesNormX.Clear();

        if (data == null || data.DurationSeconds <= 0f)
        {
            SetVerticesDirty();
            return;
        }

        // Normalize sample points to (0..1, 0..1).
        if (data.EngagementTimeline != null)
        {
            foreach (var s in data.EngagementTimeline)
            {
                float x = Mathf.Clamp01(s.TimeSeconds / data.DurationSeconds);
                float y = Mathf.Clamp01(s.EngagementPercent / 100f);
                _points.Add(new Vector2(x, y));
            }
        }

        // Normalize drop-zone X ranges.
        if (data.Drops != null)
        {
            foreach (var dz in data.Drops)
            {
                float x1 = Mathf.Clamp01(dz.StartSeconds / data.DurationSeconds);
                float x2 = Mathf.Clamp01(dz.EndSeconds / data.DurationSeconds);
                _dropZonesNormX.Add(new Vector2(x1, x2));
            }
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = rectTransform.rect;
        // Apply padding to get the inner draw area.
        int padL = padding != null ? padding.left : 0;
        int padR = padding != null ? padding.right : 0;
        int padT = padding != null ? padding.top : 0;
        int padB = padding != null ? padding.bottom : 0;

        float xMin = r.xMin + padL;
        float xMax = r.xMax - padR;
        float yMin = r.yMin + padB;
        float yMax = r.yMax - padT;

        if (xMax <= xMin || yMax <= yMin) return;

        // 1) Draw drop zones first (behind the line).
        foreach (var dz in _dropZonesNormX)
        {
            float x1 = Mathf.Lerp(xMin, xMax, dz.x);
            float x2 = Mathf.Lerp(xMin, xMax, dz.y);
            AddQuad(vh, x1, yMin, x2, yMax, dropZoneColor);
        }

        // 2) Draw connecting line segments.
        if (_points.Count >= 2)
        {
            for (int i = 0; i < _points.Count - 1; i++)
            {
                Vector2 p1 = NormToRect(_points[i], xMin, xMax, yMin, yMax);
                Vector2 p2 = NormToRect(_points[i + 1], xMin, xMax, yMin, yMax);
                AddLineSegment(vh, p1, p2, lineThickness, lineColor);
            }
        }
    }

    private static Vector2 NormToRect(Vector2 norm, float xMin, float xMax, float yMin, float yMax)
    {
        return new Vector2(
            Mathf.Lerp(xMin, xMax, norm.x),
            Mathf.Lerp(yMin, yMax, norm.y));
    }

    private static void AddQuad(VertexHelper vh, float x1, float y1, float x2, float y2, Color color)
    {
        int idx = vh.currentVertCount;
        vh.AddVert(new Vector3(x1, y1), color, Vector2.zero);
        vh.AddVert(new Vector3(x1, y2), color, Vector2.up);
        vh.AddVert(new Vector3(x2, y2), color, Vector2.one);
        vh.AddVert(new Vector3(x2, y1), color, Vector2.right);
        vh.AddTriangle(idx + 0, idx + 1, idx + 2);
        vh.AddTriangle(idx + 0, idx + 2, idx + 3);
    }

    private static void AddLineSegment(VertexHelper vh, Vector2 p1, Vector2 p2, float thickness, Color color)
    {
        Vector2 dir = p2 - p1;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        Vector2 perp = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        int idx = vh.currentVertCount;
        vh.AddVert(p1 - perp, color, Vector2.zero);
        vh.AddVert(p1 + perp, color, Vector2.up);
        vh.AddVert(p2 + perp, color, Vector2.one);
        vh.AddVert(p2 - perp, color, Vector2.right);
        vh.AddTriangle(idx + 0, idx + 1, idx + 2);
        vh.AddTriangle(idx + 0, idx + 2, idx + 3);
    }
}