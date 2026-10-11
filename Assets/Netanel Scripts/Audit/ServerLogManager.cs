using UnityEngine;
using Unity.Netcode;
using TMPro;
using System;
using System.Collections.Generic;
using System.IO; 
using System.Text; 
using System.Linq; 

public class ServerLogManager : MonoBehaviour
{
    public static ServerLogManager Instance;
    [SerializeField] private TMP_Text logTextComponent; 

    // Maps clientId → name, so we can still show the name on disconnect (player object already gone).
    private Dictionary<ulong, string> _knownPlayerNames = new Dictionary<ulong, string>();

    public struct LogEntry { public string Timestamp, Speaker, Message, Type, Color; }
    public struct AnalyticsEntry { public string Timestamp, EventType, ObjectName, Details, Position; }

    private List<LogEntry> allLogs = new List<LogEntry>();
    private List<AnalyticsEntry> analyticsData = new List<AnalyticsEntry>();

    private void Awake() { if (Instance == null) Instance = this; else Destroy(gameObject); }

   private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong id)
    {
        // Player name (NetworkVariable) takes a few frames to sync. Wait briefly.
        StartCoroutine(LogConnectedWhenNameReady(id));
    }

    private void OnClientDisconnected(ulong id)
    {
        // The player object is already gone; use the cached name.
        string name = _knownPlayerNames.TryGetValue(id, out var cached) ? cached : $"Player {id}";
        LogSystem($"{name} Disconnected");
        _knownPlayerNames.Remove(id);
    }

    private System.Collections.IEnumerator LogConnectedWhenNameReady(ulong id)
    {
        string name = $"Player {id}";
        float deadline = Time.time + 1.5f;
        while (Time.time < deadline)
        {
            if (XRMultiplayer.XRINetworkGameManager.Instance != null &&
                XRMultiplayer.XRINetworkGameManager.Instance.TryGetPlayerByID(id, out XRMultiplayer.XRINetworkPlayer p) &&
                !string.IsNullOrEmpty(p.playerName))
            {
                name = p.playerName;
                break;
            }
            yield return null;
        }
        _knownPlayerNames[id] = name;
        LogSystem($"{name} Connected");
    }

    // --- System Logging ---
    public void LogSystem(string message) 
    {
        var entry = new LogEntry { Timestamp = DateTime.Now.ToString("HH:mm:ss"), Speaker = "SYSTEM", Message = message, Type = "sys", Color = "#FFD700" };
        allLogs.Add(entry);
        UpdateLogUI();
    }

    // --- Chat Logging (הפונקציה שהייתה חסרה!) ---
    public void LogChat(string speaker, string message, Color color)
    {
        var entry = new LogEntry { 
            Timestamp = DateTime.Now.ToString("HH:mm:ss"), 
            Speaker = speaker, 
            Message = message, 
            Type = "chat", 
            Color = "#" + ColorUtility.ToHtmlStringRGB(color) 
        };
        allLogs.Add(entry);
        UpdateLogUI();
    }

    // --- Analytics Logging ---
    public void LogAnalytics(string type, string obj, Vector3 pos, string details)
    {
        analyticsData.Add(new AnalyticsEntry { 
            Timestamp = DateTime.Now.ToString("HH:mm:ss"), 
            EventType = type, 
            ObjectName = obj, 
            Position = (pos == Vector3.zero) ? "-" : $"({pos.x:F1}, {pos.y:F1}, {pos.z:F1})", 
            Details = details 
        });
    }

    // --- הפונקציה החסרה: ציור הטקסט על המסך ---
    // --- התיקון: סינון התמלול מהתצוגה ---
    private void UpdateLogUI()
    {
        if (logTextComponent == null) return;

        StringBuilder sb = new StringBuilder();

        // 1. יצירת רשימה מסוננת: רק הודעות מערכת (sys), ללא הודעות צ'אט/תמלול
        // (אנחנו משתמשים ב-Where של LINQ כדי לסנן)
        var displayLogs = allLogs.Where(l => l.Type == "sys").ToList();

        // 2. חישוב נקודת ההתחלה (מציג את ה-8 האחרונות מהרשימה המסוננת)
        int start = Mathf.Max(0, displayLogs.Count - 8);
        
        for (int i = start; i < displayLogs.Count; i++)
        {
            var log = displayLogs[i];
            
            // בניית השורה עם העיצוב המתוקן
            sb.AppendLine($"<color=#808080><size=100%>[{log.Timestamp}]</size></color> <color={log.Color}><b>{log.Speaker}:</b> {log.Message}</color>");        
        }

        logTextComponent.text = sb.ToString();
    }

    private void OnDestroy() => GenerateDashboard();

    // --- HTML Dashboard Generation ---
    private void GenerateDashboard()
    {
        var stats = AnalyticsManager.Instance != null ? AnalyticsManager.Instance.sessionData : new AnalyticsSessionData();
        if(AnalyticsManager.Instance != null) AnalyticsManager.Instance.FinalizeSession();

        StringBuilder html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html><html><head><title>WaterVision Report</title><meta charset='utf-8'>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: 'Segoe UI', sans-serif; background: #1e1e1e; color: #ecf0f1; padding: 20px; }");
        html.AppendLine(".kpi-container { display: flex; gap: 15px; margin-bottom: 30px; flex-wrap: wrap; }");
        html.AppendLine(".card { flex: 1; min-width: 150px; background: #2c3e50; padding: 20px; border-radius: 10px; text-align: center; box-shadow: 0 4px 6px rgba(0,0,0,0.3); border-bottom: 4px solid #3498db; }");
        html.AppendLine(".card h3 { margin: 0 0 10px; font-size: 13px; text-transform: uppercase; color: #bdc3c7; letter-spacing: 1px; }");
        html.AppendLine(".card .val { font-size: 32px; font-weight: 700; color: #fff; }");
        html.AppendLine(".card.alert { border-bottom-color: #e74c3c; }"); 
        html.AppendLine(".card.success { border-bottom-color: #2ecc71; }");
        html.AppendLine("table { width: 100%; border-collapse: collapse; background: #252525; margin-top: 10px; border-radius: 8px; overflow: hidden; }");
        html.AppendLine("th, td { padding: 12px 15px; text-align: left; border-bottom: 1px solid #34495e; }");
        html.AppendLine("th { background: #34495e; color: #ecf0f1; font-weight: 600; }");
        html.AppendLine("tr:hover { background-color: #2c3e50; }");
        html.AppendLine(".hidden { display: none; }");
        html.AppendLine(".tab-btn { padding: 10px 20px; background: #34495e; border: none; color: white; cursor: pointer; margin-right: 5px; border-radius: 5px 5px 0 0; }");
        html.AppendLine(".tab-btn.active { background: #3498db; }");
        html.AppendLine("</style></head><body>");

        html.AppendLine($"<h1>🎙️ WaterVision Session Report <small style='font-size:14px; color:#95a5a6'>{DateTime.Now}</small></h1>");

        // KPI Cards
        html.AppendLine("<div class='kpi-container'>");
        html.AppendLine($"<div class='card'><h3>Session Time</h3><div class='val'>{stats.totalDurationSeconds:F0}s</div></div>");
        html.AppendLine($"<div class='card success'><h3>Speaking Time</h3><div class='val'>{stats.totalSpeakingTime:F1}s</div></div>");
        html.AppendLine($"<div class='card'><h3>Participation</h3><div class='val'>{stats.participationPercentage:F1}%</div></div>");
        string alertClass = stats.longestSilenceSeconds > 30 ? "alert" : ""; 
        html.AppendLine($"<div class='card {alertClass}'><h3>Max Silence</h3><div class='val'>{stats.longestSilenceSeconds:F0}s</div></div>");
        html.AppendLine($"<div class='card'><h3>Voice Cmds</h3><div class='val'>{stats.voiceCommandsCount}</div></div>");
        html.AppendLine("</div>");

        // Tabs
        html.AppendLine("<div><button class='tab-btn active' onclick=\"show('analytics')\">📊 Audit Log</button><button class='tab-btn' onclick=\"show('sys')\">📝 System & Chat</button></div>");

        // Audit Table
        html.AppendLine("<div id='analytics' class='tab-content'>");
        html.AppendLine("<table><thead><tr><th>Time</th><th>Type</th><th>Duration/Data</th><th>Details</th></tr></thead><tbody>");
        foreach (var a in analyticsData) 
        {
            string style = a.EventType == "Speech" ? "style='background: #2c3e50; font-weight:bold; color:#3498db'" : "";
            html.AppendLine($"<tr {style}><td>{a.Timestamp}</td><td>{a.EventType}</td><td>{a.ObjectName}</td><td>{a.Details}</td></tr>");
        }
        html.AppendLine("</tbody></table></div>");

        // System & Chat Table
        html.AppendLine("<div id='sys' class='tab-content hidden'>");
        html.AppendLine("<table><thead><tr><th>Time</th><th>Speaker</th><th>Message</th></tr></thead><tbody>");
        foreach (var l in allLogs) 
        {
             html.AppendLine($"<tr><td>{l.Timestamp}</td><td style='color:{l.Color}'>{l.Speaker}</td><td>{l.Message}</td></tr>");
        }
        html.AppendLine("</tbody></table></div>");

        // JS
        html.AppendLine("<script>function show(id) { document.querySelectorAll('.tab-content').forEach(d => d.classList.add('hidden')); document.getElementById(id).classList.remove('hidden'); }</script>");
        html.AppendLine("</body></html>");

        string path = Path.Combine(Application.persistentDataPath, $"Report_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.html");
        File.WriteAllText(path, html.ToString());
        Debug.Log($"📄 Report Saved: {path}");
    }
}