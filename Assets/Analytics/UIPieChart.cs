using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPieChart : MaskableGraphic
{
    [SerializeField] private int segmentsPerSlice = 40;
    private readonly List<float> values = new List<float>();

    public void SetValues(List<float> newValues)
    {
        values.Clear();
        if (newValues != null) values.AddRange(newValues);
        SetVerticesDirty();
    }

    public static Color ColorForIndex(int i)
    {
        return Color.HSVToRGB((i * 0.17f) % 1f, 0.7f, 0.95f);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float total = 0f;
        for (int i = 0; i < values.Count; i++) total += Mathf.Max(0f, values[i]);
        if (total <= 0f) return;

        Rect r = rectTransform.rect;
        Vector2 center = r.center;
        float radius = Mathf.Min(r.width, r.height) * 0.5f;

        float angle = 0f;
        int vi = 0;

        for (int i = 0; i < values.Count; i++)
        {
            float v = Mathf.Max(0f, values[i]);
            if (v <= 0f) continue;

            float sliceAngle = (v / total) * 360f;
            int steps = Mathf.Max(2, Mathf.CeilToInt(segmentsPerSlice * (sliceAngle / 360f)));

            Color col = ColorForIndex(i);

            Vector2 prev = center + Dir(angle) * radius;
            for (int s = 1; s <= steps; s++)
            {
                float a = angle + (sliceAngle * s / steps);
                Vector2 next = center + Dir(a) * radius;

                AddTri(vh, center, prev, next, col, ref vi);
                prev = next;
            }

            angle += sliceAngle;
        }
    }

    private static Vector2 Dir(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private static void AddTri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color col, ref int vi)
    {
        vh.AddVert(a, col, Vector2.zero);
        vh.AddVert(b, col, Vector2.zero);
        vh.AddVert(c, col, Vector2.zero);
        vh.AddTriangle(vi, vi + 1, vi + 2);
        vi += 3;
    }
}
