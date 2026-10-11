using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class IconZoomOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform iconRect;   // ה-Icon (Image)
    public float zoom = 1.25f;       // כמה להתקרב (1.15-1.35)

    Vector2 baseSizeDelta;
    Vector2 baseAnchoredPos;

    void Awake()
    {
        if (iconRect == null) return;
        baseSizeDelta = iconRect.sizeDelta;
        baseAnchoredPos = iconRect.anchoredPosition;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (iconRect == null) return;

        // אם אתה על Stretch (sizeDelta=0), נעשה zoom ע"י scale פנימי של ה-Icon בלבד:
        iconRect.localScale = Vector3.one * zoom;
        iconRect.anchoredPosition = baseAnchoredPos; // נשאר באמצע
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (iconRect == null) return;
        iconRect.localScale = Vector3.one;
        iconRect.anchoredPosition = baseAnchoredPos;
    }
}