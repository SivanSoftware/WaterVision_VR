using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WaterVision.AR;

/// <summary>
/// The floating utility toolbar shown only while AR mode is active: Save / Exit AR / Delete.
/// Head-locked lazy-follow (~1 m in front, eases with a slight lag). Built by ARUtilityToolbarSetup.
///
/// Decoupling: Save finds the export station by name + SendMessage("ExportNow") (no ref to the WaterVision.Export
/// assembly); Delete uses ActivePipeRegistry + BroadcastMessage("RequestDespawn") (no ref to the Models assembly);
/// only ARModeController (same Assembly-CSharp) is referenced directly, for the show/hide event + Exit.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class ARUtilityToolbar : MonoBehaviour
{
    [Header("Wired by the setup tool")]
    [SerializeField] private ARModeController m_Controller;
    [SerializeField] private Button m_SaveButton;
    [SerializeField] private Button m_ExitButton;
    [SerializeField] private Button m_DeleteButton;
    [SerializeField] private Button m_RecenterButton;
    [Tooltip("GameObject (with the PipeAssemblyExporter) whose ExportNow() the Save button calls. Found by name if empty.")]
    [SerializeField] private string m_ExportStationName = "ExportStation_Canvas";

    [Header("Head-locked lazy-follow")]
    [SerializeField] private float m_Distance = 0.9f;  // metres in front of the head
    [SerializeField] private float m_Height = -0.15f;  // metres relative to eye height (slightly below)
    [SerializeField] private float m_FollowLerp = 6f;  // higher = snappier; lower = more lag

    [SerializeField] private bool m_DebugLog = true;

    [Tooltip("Set by ARGrabbablePanelSetup. While placed (grabbed at least once), the toolbar stops head-following.")]
    [SerializeField] private GrabbableUIPanel m_GrabPanel;

    private Canvas m_Canvas;
    private Camera m_Cam;
    private bool m_Shown;

    private bool IsPlaced => m_GrabPanel != null && m_GrabPanel.IsPlaced;

    private void Awake()
    {
        m_Canvas = GetComponent<Canvas>();
        if (m_Controller == null) m_Controller = FindFirstObjectByType<ARModeController>(FindObjectsInactive.Include);
        if (m_GrabPanel == null) m_GrabPanel = GetComponent<GrabbableUIPanel>();

        if (m_SaveButton != null)   m_SaveButton.onClick.AddListener(Save);
        if (m_ExitButton != null)   m_ExitButton.onClick.AddListener(ExitAR);
        if (m_DeleteButton != null) m_DeleteButton.onClick.AddListener(DeleteCurrentPipe);
        if (m_RecenterButton != null) m_RecenterButton.onClick.AddListener(Recenter);
    }

    private void OnEnable()
    {
        if (m_Controller != null) m_Controller.ARModeChanged += OnARModeChanged;
        SetShown(m_Controller != null && m_Controller.IsARModeActive);
    }

    private void OnDisable()
    {
        if (m_Controller != null) m_Controller.ARModeChanged -= OnARModeChanged;
    }

    private void OnARModeChanged(bool on) => SetShown(on);

    private void SetShown(bool on)
    {
        m_Shown = on;
        if (m_Canvas != null) m_Canvas.enabled = on;                       // hides rendering + children
        foreach (var rc in GetComponents<BaseRaycaster>()) rc.enabled = on; // GraphicRaycaster + TrackedDeviceGraphicRaycaster
        if (on && !IsPlaced) Place(snap: true); // once the user has placed it, keep its spot instead of head-following
    }

    private void LateUpdate()
    {
        if (m_Shown && !IsPlaced) Place(snap: false);
    }

    private void Place(bool snap)
    {
        if (m_Cam == null) m_Cam = Camera.main;
        if (m_Cam == null) return;

        Vector3 fwd = m_Cam.transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) return;
        fwd.Normalize();

        Vector3 target = m_Cam.transform.position + fwd * m_Distance + Vector3.up * m_Height;
        transform.position = snap ? target : Vector3.Lerp(transform.position, target, Time.deltaTime * m_FollowLerp);
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up); // +Z away from user → readable -Z face toward user, non-mirrored (matches MenuController)
    }

    // ── Button actions ────────────────────────────────────────────────────────
    public void Save()
    {
        var go = string.IsNullOrEmpty(m_ExportStationName) ? null : GameObject.Find(m_ExportStationName);
        if (go != null)
        {
            go.SendMessage("ExportNow", SendMessageOptions.DontRequireReceiver);
            if (m_DebugLog) Debug.Log("[ARUtilityToolbar] Save → ExportNow on '" + go.name + "'.");
        }
        else if (m_DebugLog)
        {
            Debug.LogWarning("[ARUtilityToolbar] Export station '" + m_ExportStationName + "' not found — set m_ExportStationName.");
        }
    }

    public void ExitAR()
    {
        if (m_Controller != null) m_Controller.ExitARMode();
    }

    public void DeleteCurrentPipe()
    {
        var pipe = ActivePipeRegistry.Current;
        if (pipe != null)
        {
            pipe.BroadcastMessage("RequestDespawn", SendMessageOptions.DontRequireReceiver);
            if (m_DebugLog) Debug.Log("[ARUtilityToolbar] Delete → RequestDespawn on '" + pipe.name + "'.");
        }
        else if (m_DebugLog)
        {
            Debug.Log("[ARUtilityToolbar] No active pipe to delete — grab a pipe first.");
        }
    }

    public void Recenter()
    {
        var placer = m_Controller != null ? m_Controller.GetComponent<ARWorkspacePlacer>() : null;
        if (placer != null)
        {
            placer.RecenterWorkspace();
            if (m_DebugLog) Debug.Log("[ARUtilityToolbar] Recenter → re-placed workspace in front of the user.");
        }
        else if (m_DebugLog) Debug.LogWarning("[ARUtilityToolbar] No ARWorkspacePlacer found — can't recenter.");
    }
}
