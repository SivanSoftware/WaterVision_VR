// PeopleAnalyticsPanelController.cs
// Controls the People Analytics panel visibility, positioning, content, tab switching, export, and close.
// Place under: Assets/UI/RadialMenu/Scripts/Analytics/

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PeopleAnalyticsPanelController : MonoBehaviour
{
    [Header("Panel References")]
    [Tooltip("The root GameObject that contains the panel UI (Canvas).")]
    [SerializeField] private GameObject panelRoot;

    [Header("Meeting Header")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Metric Cards")]
    [SerializeField] private MetricCardView totalSpeechCard;
    [SerializeField] private MetricCardView avgEngagementCard;
    [SerializeField] private MetricCardView dropsCard;

    [Header("Tabs")]
    [SerializeField] private Button tab1Button;
    [SerializeField] private Button tab2Button;
    [SerializeField] private GameObject tab1Content;
    [SerializeField] private GameObject tab2Content;
    [SerializeField] private Color activeTabColor = new Color(0.89f, 0.95f, 0.99f);
    [SerializeField] private Color inactiveTabColor = new Color(0.96f, 0.96f, 0.96f);

    [Header("Participant Rows")]
    [SerializeField] private ParticipantRowView participantRowPrefab;
    [SerializeField] private Transform participantsContainer;

    [Header("Engagement Chart")]
    [SerializeField] private EngagementChartGraphic engagementChart;

    [Header("Topic Bars")]
    [SerializeField] private TopicBarView topicBarPrefab;
    [SerializeField] private Transform topicsContainer;

    [Header("Insight Cards")]
    [SerializeField] private InsightCardView insightCardPrefab;
    [SerializeField] private Transform insightsContainer;

    [Header("Export")]
    [SerializeField] private Button exportButton;
    [SerializeField] private TextMeshProUGUI exportButtonText;
    [SerializeField] private string exportButtonDefaultLabel = "Export";
    [SerializeField] private string exportButtonSuccessLabel = "\u2713 Saved";
    [SerializeField] private string exportButtonFailureLabel = "\u2717 Failed";
    [SerializeField] private float exportFeedbackDuration = 2.5f;

    [Header("Close")]
    [Tooltip("Button that closes (hides) the panel.")]
    [SerializeField] private Button closeButton;

    [Header("VR Positioning")]
    [SerializeField] private float openDistance = 1.2f;
    [SerializeField] private float verticalOffset = -0.1f;

    private IAnalyticsDataProvider _provider;
    private MeetingAnalytics _currentData;
    private readonly List<ParticipantRowView> _spawnedRows = new List<ParticipantRowView>();
    private readonly List<TopicBarView> _spawnedTopicBars = new List<TopicBarView>();
    private readonly List<InsightCardView> _spawnedInsights = new List<InsightCardView>();
    private int _activeTabIndex = 0;
    private Coroutine _exportFeedbackCoroutine;

    void Awake()
    {
        // Prefer a live provider sibling, fall back to mock
        _provider = GetComponent<IAnalyticsDataProvider>();
        if (_provider == null)
        {
            _provider = new MockAnalyticsDataProvider();
            Debug.Log("[Analytics] Using MOCK data (no live provider on this GameObject)");
        }
        else
        {
            Debug.Log($"[Analytics] Using LIVE provider: {_provider.GetType().Name}");
        }

        if (panelRoot != null) panelRoot.SetActive(false);
        if (tab1Button != null) tab1Button.onClick.AddListener(() => ShowTab(0));
        if (tab2Button != null) tab2Button.onClick.AddListener(() => ShowTab(1));
        if (exportButton != null) exportButton.onClick.AddListener(OnExportClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
    }

    [ContextMenu("Show Panel")]
    public void Show()
    {
        if (panelRoot == null) return;

        _currentData = _provider.GetCurrentMeetingAnalytics();
        Debug.Log($"[Analytics] Showing panel for: {_currentData.RoomName} ({_currentData.ParticipantCount} participants)");

        BindHeader();
        BindMetricCards();
        PopulateParticipantRows();
        BindEngagementChart();
        PopulateTopics();
        PopulateInsights();
        ResetExportButton();

        ShowTab(0);

        PositionInFrontOfPlayer();
        panelRoot.SetActive(true);
    }

    public void ShowTab(int index)
    {
        _activeTabIndex = index;
        if (tab1Content != null) tab1Content.SetActive(index == 0);
        if (tab2Content != null) tab2Content.SetActive(index == 1);
        UpdateTabVisuals();
    }

    private void UpdateTabVisuals()
    {
        SetTabButtonColor(tab1Button, _activeTabIndex == 0);
        SetTabButtonColor(tab2Button, _activeTabIndex == 1);
    }

    private void SetTabButtonColor(Button btn, bool isActive)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = isActive ? activeTabColor : inactiveTabColor;
    }

    // ----------------- Bind / Populate -----------------

    private void BindHeader()
    {
        if (titleText != null)
            titleText.text = _currentData.RoomName;

        if (subtitleText != null)
        {
            string dateStr = _currentData.Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
            string durationStr = MeetingAnalytics.FormatTime(_currentData.DurationSeconds);
            subtitleText.text = $"{dateStr}  \u2022  {durationStr}  \u2022  {_currentData.ParticipantCount} participants";
        }
    }

    private void BindMetricCards()
    {
        if (totalSpeechCard != null)
            totalSpeechCard.Bind(MeetingAnalytics.FormatTime(_currentData.TotalSpeechSeconds), "Total Speech");
        if (avgEngagementCard != null)
            avgEngagementCard.Bind($"{Mathf.RoundToInt(_currentData.AverageEngagement)}%", "Avg Engagement");
        if (dropsCard != null)
            dropsCard.Bind(_currentData.DropsDetected.ToString(), "Drops Detected");
    }

    private void BindEngagementChart()
    {
        if (engagementChart != null)
            engagementChart.SetData(_currentData);
    }

    private void PopulateParticipantRows()
    {
        if (participantRowPrefab == null || participantsContainer == null) return;
        foreach (var row in _spawnedRows)
            if (row != null) Destroy(row.gameObject);
        _spawnedRows.Clear();
        foreach (var participant in _currentData.Participants)
        {
            var row = Instantiate(participantRowPrefab, participantsContainer);
            row.Bind(participant);
            _spawnedRows.Add(row);
        }
    }

    private void PopulateTopics()
    {
        if (topicBarPrefab == null || topicsContainer == null) return;
        foreach (var bar in _spawnedTopicBars)
            if (bar != null) Destroy(bar.gameObject);
        _spawnedTopicBars.Clear();
        foreach (var topic in _currentData.Topics)
        {
            var bar = Instantiate(topicBarPrefab, topicsContainer);
            bar.Bind(topic);
            _spawnedTopicBars.Add(bar);
        }
    }

    private void PopulateInsights()
    {
        if (insightCardPrefab == null || insightsContainer == null) return;
        foreach (var card in _spawnedInsights)
            if (card != null) Destroy(card.gameObject);
        _spawnedInsights.Clear();
        foreach (var insight in _currentData.Insights)
        {
            var card = Instantiate(insightCardPrefab, insightsContainer);
            card.Bind(insight);
            _spawnedInsights.Add(card);
        }
    }

    // ----------------- Export -----------------

    private void OnExportClicked()
    {
        if (_currentData == null)
        {
            Debug.LogWarning("[Analytics] No data to export.");
            return;
        }

        var result = AnalyticsExporter.SaveBoth(_currentData);

        if (result.Success)
        {
            Debug.Log($"[Analytics] Exported successfully:\n  JSON:     {result.JsonPath}\n  Markdown: {result.MarkdownPath}");
            ShowExportFeedback(exportButtonSuccessLabel);
        }
        else
        {
            Debug.LogError($"[Analytics] Export failed: {result.ErrorMessage}");
            ShowExportFeedback(exportButtonFailureLabel);
        }
    }

    private void ShowExportFeedback(string message)
    {
        if (exportButtonText == null) return;

        if (_exportFeedbackCoroutine != null)
            StopCoroutine(_exportFeedbackCoroutine);
        _exportFeedbackCoroutine = StartCoroutine(ExportFeedbackRoutine(message));
    }

    private IEnumerator ExportFeedbackRoutine(string message)
    {
        if (exportButton != null) exportButton.interactable = false;
        if (exportButtonText != null) exportButtonText.text = message;

        yield return new WaitForSeconds(exportFeedbackDuration);

        ResetExportButton();
        _exportFeedbackCoroutine = null;
    }

    private void ResetExportButton()
    {
        if (exportButton != null) exportButton.interactable = true;
        if (exportButtonText != null) exportButtonText.text = exportButtonDefaultLabel;
    }

    // ----------------- Hide / Toggle / Position -----------------

    [ContextMenu("Hide Panel")]
    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        Debug.Log("[Analytics] Panel closed.");
    }

    public void Toggle()
    {
        if (panelRoot == null) return;
        if (panelRoot.activeSelf) Hide();
        else Show();
    }

    private void PositionInFrontOfPlayer()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 panelPos = cam.transform.position + forward * openDistance + Vector3.up * verticalOffset;
        panelRoot.transform.position = panelPos;
        panelRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}