using UnityEngine;

namespace WaterVision.Recording
{
    /// <summary>
    /// Tiny helper: wire a Button.onClick → <see cref="Hide"/> and the target GameObject is
    /// deactivated. Lives outside the Editor folder so it survives builds (the editor builder
    /// adds it via AddComponent at edit-time, but the type must exist at runtime).
    /// </summary>
    public class HidePanelOnClick : MonoBehaviour
    {
        [Tooltip("GameObject to deactivate when Hide() is called. Defaults to this if null.")]
        public GameObject target;

        public void Hide()
        {
            var go = target != null ? target : gameObject;
            go.SetActive(false);
        }
    }
}
