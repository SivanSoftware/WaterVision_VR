using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using XRMultiplayer;

public class RadialMenuController : MonoBehaviour
{
    [Header("Analytics")]
    [SerializeField] private PeopleAnalyticsPanelController analyticsPanel;

    [Header("Hub UI References")]
    [SerializeField] private TMP_Text roomNameLabel;
    [SerializeField] private TMP_Text participantCountLabel;

    [Header("Area Switching - Icons (Image components)")]
    [SerializeField] private Image hubIcon;
    [SerializeField] private Image modelButtonIcon;
    [SerializeField] private Image simulationButtonIcon;

    [Header("Area Switching - Button Labels (TMP Text)")]
    [SerializeField] private TMP_Text modelButtonLabel;
    [SerializeField] private TMP_Text simulationButtonLabel;

    [Header("Area Switching - Sprites")]
    [SerializeField] private Sprite iconHome;
    [SerializeField] private Sprite iconModels;
    [SerializeField] private Sprite iconSimulation;

    [Header("Area Switching - Label Texts")]
    [SerializeField] private string textModels = "Models";
    [SerializeField] private string textSimulation = "Simulation";
    [SerializeField] private string textHome = "Home";

    private int _refreshCounter = 0;
    private const int REFRESH_INTERVAL = 30;
    private bool _sttMuted = false;

    // ============ Lifecycle ============

    void OnEnable()
    {
        UpdateHubInfo();

        if (WorldAreaManager.Instance != null)
        {
            WorldAreaManager.Instance.OnAreaChanged += OnAreaChanged;
            UpdateButtonsForArea(WorldAreaManager.Instance.CurrentArea);
        }
    }

    void OnDisable()
    {
        if (WorldAreaManager.Instance != null)
            WorldAreaManager.Instance.OnAreaChanged -= OnAreaChanged;
    }

    void Update()
    {
        _refreshCounter++;
        if (_refreshCounter >= REFRESH_INTERVAL)
        {
            UpdateHubInfo();
            _refreshCounter = 0;
        }
    }

    private void UpdateHubInfo()
    {
        if (roomNameLabel != null)
        {
            string name = XRINetworkGameManager.ConnectedRoomName != null
                ? XRINetworkGameManager.ConnectedRoomName.Value : null;
            roomNameLabel.text = string.IsNullOrEmpty(name) ? "Lobby" : name;
        }

        if (participantCountLabel != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            int count = NetworkManager.Singleton.ConnectedClients.Count;
            participantCountLabel.text = $"{count} {(count == 1 ? "Participant" : "Participants")}";
        }
        else if (participantCountLabel != null)
        {
            participantCountLabel.text = "Offline";
        }
    }

    // ============ Area Switching ============

    private void OnAreaChanged(WorldArea area) => UpdateButtonsForArea(area);

    private void UpdateButtonsForArea(WorldArea area)
    {
        // Hub icon (center)
        if (hubIcon != null)
        {
            hubIcon.sprite = area switch
            {
                WorldArea.Home => iconHome,
                WorldArea.Models => iconModels,
                WorldArea.Simulation => iconSimulation,
                _ => iconHome
            };
        }

        // Models button — becomes "Home" when in Models
        if (modelButtonIcon != null)
            modelButtonIcon.sprite = (area == WorldArea.Models) ? iconHome : iconModels;
        if (modelButtonLabel != null)
            modelButtonLabel.text = (area == WorldArea.Models) ? textHome : textModels;

        // Simulation button — becomes "Home" when in Simulation
        if (simulationButtonIcon != null)
            simulationButtonIcon.sprite = (area == WorldArea.Simulation) ? iconHome : iconSimulation;
        if (simulationButtonLabel != null)
            simulationButtonLabel.text = (area == WorldArea.Simulation) ? textHome : textSimulation;
    }

    public void OnModelButtonClicked()
    {
        var current = WorldAreaManager.Instance != null ? WorldAreaManager.Instance.CurrentArea : WorldArea.Home;
        TeleportToArea(current == WorldArea.Models ? WorldArea.Home : WorldArea.Models);
    }

    public void OnSimulationButtonClicked()
    {
        var current = WorldAreaManager.Instance != null ? WorldAreaManager.Instance.CurrentArea : WorldArea.Home;
        TeleportToArea(current == WorldArea.Simulation ? WorldArea.Home : WorldArea.Simulation);
    }

    private void TeleportToArea(WorldArea area)
    {
        Transform target = LandingPoint.Get(area);
        if (target == null) { Debug.LogWarning($"[RadialMenu] No landing point for {area}"); return; }
        if (Camera.main == null) { Debug.LogWarning("[RadialMenu] No Main Camera"); return; }

        GameObject xrOrigin = Camera.main.transform.root.gameObject;
        Transform cam = Camera.main.transform;
        CharacterController cc = xrOrigin.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        xrOrigin.transform.rotation = target.rotation;
        Physics.SyncTransforms();
        Vector3 camOffset = cam.position - xrOrigin.transform.position;
        xrOrigin.transform.position = new Vector3(
            target.position.x - camOffset.x,
            target.position.y,
            target.position.z - camOffset.z
        );
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;

        if (WorldAreaManager.Instance != null)
            WorldAreaManager.Instance.CurrentArea = area;

        Debug.Log($"[RadialMenu] Teleported to {area}");
        CloseMenu();
    }

    /// <summary>Kept for backward compat — old menu wiring still works.</summary>
    public void TeleportToSimulationArea() => OnSimulationButtonClicked();

    // ============ Existing Actions (unchanged) ============

    public void LeaveMeeting()
    {
        if (XRINetworkGameManager.Instance != null)
            XRINetworkGameManager.Instance.Disconnect();
        CloseMenu();
    }

    public void CloseMenu() => gameObject.SetActive(false);

    public void TakeScreenshot()
    {
        // Close the menu first so it isn't in the shot, then capture to the public Downloads
        // folder (Quest: Download/WaterVisionVR; Windows: %USERPROFILE%\Downloads\WaterVisionVR).
        // CaptureToDownloads runs on its own DontDestroyOnLoad runner, so it still completes
        // even though CloseMenu() deactivates this GameObject this frame.
        CloseMenu();
        ScreenshotHandler.CaptureToDownloads();
    }

    public void OpenAnalyticsPanel()
    {
        if (analyticsPanel == null) analyticsPanel = FindObjectOfType<PeopleAnalyticsPanelController>();
        if (analyticsPanel != null) analyticsPanel.Show();
        CloseMenu();
    }

    public void ToggleSTTMute()
    {
        _sttMuted = !_sttMuted;
        if (MicRecorder.Instance != null) MicRecorder.Instance.SetMuted(_sttMuted);
    }

    // Toggles the local ASL sign-language fingerspelling bubble (per-user accessibility view).
    public void ToggleSignLanguageMode()
    {
        if (SignLanguageBubbleManager.Instance != null)
            SignLanguageBubbleManager.Instance.Toggle();
        CloseMenu();
    }
}