using UnityEngine;
using UnityEngine.UI;

namespace WaterVision.Recording
{
    /// <summary>
    /// Drop this on a RadialMenu button to wire it (at runtime) to (1) open the recording dashboard
    /// and (2) close the radial menu — matching how every other radial button (LeaveMeeting,
    /// TakeScreenshot, OpenAnalyticsPanel, ToggleSignLanguageMode) ends by calling CloseMenu().
    /// Auto-wiring pattern lifted from SignLanguageRadialButton: avoids hand-authoring a UnityEvent
    /// PersistentCall in the prefab YAML.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RecordingRadialButton : MonoBehaviour
    {
        bool _wired;

        void OnEnable()
        {
            if (_wired) return;
            var btn = GetComponent<Button>();
            if (btn == null) return;

            // The radial menu controller — same lookup SignLanguageRadialButton uses. May be null
            // if this button somehow ended up outside the menu hierarchy; we still wire the open.
            var rc = GetComponentInParent<RadialMenuController>(true);

            // Prefer a launcher nested in the radial menu hierarchy; fall back to scene-wide search
            // so it still works when the launcher lives on a different menu prefab (e.g. VR_MainMenu).
            var launcher = GetComponentInParent<RecordingPanelLauncher>(true);
            if (launcher == null)
                launcher = Object.FindFirstObjectByType<RecordingPanelLauncher>(FindObjectsInactive.Include);

            if (launcher == null)
            {
                Debug.LogWarning("[RecordingRadialButton] No RecordingPanelLauncher found in the scene — " +
                                 "the recording button click will do nothing. Add a RecordingPanelLauncher to " +
                                 "the RadialMenu root or any menu prefab.", this);
                return;
            }

            btn.onClick.AddListener(() =>
            {
                launcher.OpenRecordingPanel();
                if (rc != null) rc.CloseMenu();
            });
            _wired = true;
        }
    }
}
