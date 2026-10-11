using UnityEngine;

/// <summary>
/// Mirrors this GameObject's active state onto a target GameObject. Whenever this object becomes
/// active or inactive, the target is set to the same state. Used by the WWTP cutscene system so
/// the dark subtitle background (Panel) appears and disappears together with the subtitle text —
/// CameraMove.cs only toggles its `uiTextBox` GameObject, so we mirror that onto the background.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class MirrorActiveState : MonoBehaviour
{
    [Tooltip("The GameObject to keep in sync with this object's active state.")]
    public GameObject target;

    void OnEnable()  { if (target != null) target.SetActive(true);  }
    void OnDisable() { if (target != null) target.SetActive(false); }
}
