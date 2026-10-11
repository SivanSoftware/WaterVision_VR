using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drop this on a RadialMenu button to wire it (at runtime) to toggle the ASL sign-language bubble —
/// avoids hand-authoring a UnityEvent PersistentCall in the prefab YAML. Finds the RadialMenuController
/// in its parents and routes the click to ToggleSignLanguageMode().
/// </summary>
[RequireComponent(typeof(Button))]
public class SignLanguageRadialButton : MonoBehaviour
{
    bool _wired;

    void OnEnable()
    {
        if (_wired) return;
        var btn = GetComponent<Button>();
        var rc = GetComponentInParent<RadialMenuController>(true);
        if (btn != null && rc != null)
        {
            btn.onClick.AddListener(rc.ToggleSignLanguageMode);
            _wired = true;
        }
    }
}
