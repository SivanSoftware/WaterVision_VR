using UnityEngine;

/// <summary>
/// Opens the session-recording dashboard from a main-menu button.
///
/// Lives on the <c>VR_MainMenu</c> prefab (alongside the button), so a UI Button onClick can call
/// <see cref="OpenRecordingPanel"/> directly — mirroring how <c>MuteToggleButton</c> is wired.
/// Because the menu is instantiated at runtime, it cannot serialize a reference to the in-scene
/// recording dashboard, so the dashboard is located at runtime via <see cref="RecordingDashboard"/>
/// (include-inactive search). The dashboard's world-space Canvas is then activated and summoned in
/// front of the player — the same placement logic <c>MenuController</c> uses for the menu itself.
/// </summary>
public class RecordingPanelLauncher : MonoBehaviour
{
    [Header("Placement (matches MenuController)")]
    [Tooltip("How far in front of the player the recording panel appears (meters).")]
    [SerializeField] private float distance = 1.5f;

    [Tooltip("Vertical offset from camera height (negative = lower).")]
    [SerializeField] private float heightOffset = -0.2f;

    [Tooltip("If true, OpenRecordingPanel re-summons the panel in front of the player every press. " +
             "If false, it only repositions the first time and otherwise just re-shows it where it is.")]
    [SerializeField] private bool repositionEveryOpen = true;

    private bool m_Positioned;

    /// <summary>Wire this to the menu's recording button onClick.</summary>
    public void OpenRecordingPanel()
    {
        Transform target = ResolvePanelRoot();
        if (target == null)
        {
            Debug.LogWarning("[RecordingPanelLauncher] No RecordingDashboard found in the scene — " +
                             "cannot open the recording panel.");
            return;
        }

        target.gameObject.SetActive(true);

        if (repositionEveryOpen || !m_Positioned)
        {
            PositionInFrontOfPlayer(target);
            m_Positioned = true;
        }
    }

    /// <summary>Hide the recording panel (e.g. from a Close button).</summary>
    public void HideRecordingPanel()
    {
        Transform target = ResolvePanelRoot();
        if (target != null) target.gameObject.SetActive(false);
    }

    /// <summary>Show if hidden, hide if shown.</summary>
    public void ToggleRecordingPanel()
    {
        Transform target = ResolvePanelRoot();
        if (target == null) return;

        if (target.gameObject.activeSelf)
        {
            target.gameObject.SetActive(false);
        }
        else
        {
            target.gameObject.SetActive(true);
            PositionInFrontOfPlayer(target);
            m_Positioned = true;
        }
    }

    /// <summary>
    /// The visual root to show/move: the dashboard's world-space Canvas if present, else the
    /// dashboard GameObject itself. Searched include-inactive so it resolves even when hidden.
    /// </summary>
    private Transform ResolvePanelRoot()
    {
        var dash = FindFirstObjectByType<RecordingDashboard>(FindObjectsInactive.Include);
        if (dash == null) return null;

        var canvas = dash.GetComponentInParent<Canvas>(includeInactive: true);
        return canvas != null ? canvas.transform : dash.transform;
    }

    private void PositionInFrontOfPlayer(Transform target)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[RecordingPanelLauncher] Main Camera not found! Tag your VR camera 'MainCamera'.");
            return;
        }

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 pos = cam.transform.position + forward * distance;
        pos.y += heightOffset;
        target.position = pos;
        target.rotation = Quaternion.LookRotation(forward); // face the player (canvas +Z toward forward)
    }
}
