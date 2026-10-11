using UnityEngine;
using System.IO;
using System.Text;
using System;
using System.Collections.Generic;

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance;
    private string filePath;
    private string sessionID;
    
    // --- נתונים לחישוב זמן אמת ---
    private float sessionStartTime;
    
    // --- משתנים חדשים לניתוח דיבור (Speech Analytics) ---
    private float lastSpeechEndTime; // מתי הפעם האחרונה שמישהו דיבר?
    
    // המידע המסוכם
    public AnalyticsSessionData sessionData = new AnalyticsSessionData();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        DontDestroyOnLoad(gameObject);
        Debug.Log("🚀 AnalyticsManager: Speech Analytics Module Loaded.");

        sessionStartTime = Time.time;
        lastSpeechEndTime = Time.time; // מתחילים לספור שתיקה מההתחלה
        
        sessionID = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        sessionData.sessionDate = sessionID;

        string fileName = $"Session_{sessionID}.csv";
        filePath = Path.Combine(Application.persistentDataPath, fileName);

        if (!File.Exists(filePath))
        {
            // עדכנתי את הכותרות לתמוך במשך זמן (Duration)
            File.WriteAllText(filePath, "Timestamp,UserID,EventType,Duration,ObjectName,Details\n");
        }
    }

    void Update()
    {
        // --- 6.7.3 Engagement Drop Analysis ---
        // חישוב שתיקה נוכחית (Current Silence)
        float currentSilence = Time.time - lastSpeechEndTime;
        
        // אם השתיקה הנוכחית ארוכה יותר מהשיא הקודם, נעדכן את השיא
        if (currentSilence > sessionData.longestSilenceSeconds)
        {
            sessionData.longestSilenceSeconds = currentSilence;
        }
    }

    // --- פונקציה ייעודית לדיווח על דיבור (תקרא לזה מה-STT שלך) ---
    public void LogSpeechEvent(float durationInSeconds, string transcribedText)
    {
        // עדכון מדדי השתתפות (6.7.2)
        sessionData.voiceCommandsCount++;
        sessionData.totalSpeakingTime += durationInSeconds;
        
        // איפוס מונה השתיקה (כי עכשיו מדברים)
        lastSpeechEndTime = Time.time + durationInSeconds; // מוסיפים את האורך כדי שהשתיקה תתחיל רק כשיסיימו לדבר

        // כתיבה ל-CSV עם משך הזמן
        WriteToCSV("Speech", durationInSeconds.ToString("F2"), "Microphone", transcribedText);

        // דיווח ל-Dashboard
        if (ServerLogManager.Instance != null)
        {
            ServerLogManager.Instance.LogAnalytics("Speech", "Audio Clip", Vector3.zero, $"Length: {durationInSeconds:F1}s | \"{transcribedText}\"");
        }
    }

    // הפונקציה הרגילה לאירועים אחרים (בנייה, תפריט וכו')
    public void LogEvent(string eventType, string objectName, Vector3 position, string extraDetails = "")
    {
        UpdateMetrics(eventType);
        WriteToCSV(eventType, "0", objectName, extraDetails); // Duration is 0 for instant events

        if (ServerLogManager.Instance != null)
        {
            ServerLogManager.Instance.LogAnalytics(eventType, objectName, position, extraDetails);
        }
    }

    private void UpdateMetrics(string eventType)
    {
        string type = eventType.ToLower();
        if (type.Contains("summon") || type.Contains("build")) sessionData.totalObjectsPlaced++;
        else if (type.Contains("delete")) sessionData.totalObjectsDeleted++;
        else if (type.Contains("menu")) sessionData.menuInteractionsCount++;
    }

    private void WriteToCSV(string eventType, string duration, string objectName, string details)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        // מבנה CSV מעודכן: Timestamp, User, Event, Duration, Object, Details
        string newLine = $"{timestamp},User_1,{eventType},{duration},{objectName},{details}";
        try { File.AppendAllText(filePath, newLine + "\n"); } catch { }
    }

    public void FinalizeSession()
    {
        sessionData.totalDurationSeconds = Time.time - sessionStartTime;
        
        // חישוב אחוז השתתפות (Participation %) - חלק ממשימה 6.7.2
        if (sessionData.totalDurationSeconds > 0)
        {
            sessionData.participationPercentage = (sessionData.totalSpeakingTime / sessionData.totalDurationSeconds) * 100f;
        }
    }
    
    private void OnDestroy() => FinalizeSession();
}

// --- מבנה הנתונים המותאם לדרישות החדשות ---
[System.Serializable]
public class AnalyticsSessionData
{
    public string sessionDate;
    public float totalDurationSeconds;
    
    // Action Metrics
    public int totalObjectsPlaced = 0;
    public int totalObjectsDeleted = 0;
    public int menuInteractionsCount = 0;

    // Speech Metrics (6.7.1 - 6.7.3)
    public int voiceCommandsCount = 0;      // תדירות (Frequency)
    public float totalSpeakingTime = 0;     // משך (Duration)
    public float longestSilenceSeconds = 0; // נפילת מעורבות (Engagement Drop)
    public float participationPercentage = 0; // אחוז מעורבות
}