using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Slowly pulses a UI Graphic's alpha between min and max — used on the Export panel's cube glow for a
/// gentle "alive" holographic breathing. Null-guarded; runs in play mode.
/// </summary>
public class GlowPulse : MonoBehaviour
{
    public Graphic target;
    public float minAlpha = 0.14f;
    public float maxAlpha = 0.30f;
    public float speed = 1.2f;

    float _t;

    void Update()
    {
        if (target == null) return;
        _t += Time.deltaTime * speed;
        float k = (Mathf.Sin(_t) + 1f) * 0.5f;
        var c = target.color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, k);
        target.color = c;
    }
}
