using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// VR hover affordance for the Export button (Direction B). On pointer-enter (laser hover) it brightens
/// the top sheen and nudges the button scale up; reverts on exit. Skips when the button is disabled
/// (e.g. mid-export). Purely cosmetic and null-guarded; the export state machine drives the base colour
/// separately, so there is no conflict.
/// </summary>
public class ExportButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Graphic sheen;
    public RectTransform scaleTarget;
    public Selectable button;
    public float idleAlpha = 0.22f;
    public float hoverAlpha = 0.45f;
    public float hoverScale = 1.03f;

    void OnEnable() => Apply(false);

    public void OnPointerEnter(PointerEventData e)
    {
        if (button != null && !button.interactable) return;
        Apply(true);
    }

    public void OnPointerExit(PointerEventData e) => Apply(false);

    void Apply(bool hover)
    {
        if (sheen != null) { var c = sheen.color; c.a = hover ? hoverAlpha : idleAlpha; sheen.color = c; }
        if (scaleTarget != null) scaleTarget.localScale = Vector3.one * (hover ? hoverScale : 1f);
    }
}
