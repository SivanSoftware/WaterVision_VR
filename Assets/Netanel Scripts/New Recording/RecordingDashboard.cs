using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using XRMultiplayer;

public class RecordingDashboard : MonoBehaviour
{
    [Header("--- Left Side: Recorder ---")]
    public Button btnRecord;
    public Button btnStop;
    public TMP_Text txtTimer;
    public TMP_Text txtStatus;
    public Image imgRecordingIndicator; // הנורה האדומה

    [Header("--- Right Side: Library ---")]
    public Transform listContainer;      // ה-Content של ה-ScrollView
    public GameObject fileButtonPrefab;  // כפתור פשוט שמייצג קובץ
    public Button btnRefresh;

    private bool isRecordingUI = false;
    // Tracks whether we've already locked the UI down because *another* client
    // is recording — used to debounce per-frame state writes.
    private bool isLockedForRemoteRecording = false;

    void Start()
    {
        // 1. חיבור כפתורי הקלטה
        btnRecord.onClick.AddListener(StartRecording);
        btnStop.onClick.AddListener(StopRecording);
        
        // 2. חיבור כפתור רענון
        if(btnRefresh) btnRefresh.onClick.AddListener(RefreshLibrary);

        // 3. אתחול
        ResetRecorderUI();
        RefreshLibrary(); // טעינת הרשימה מיד בהתחלה
    }

    void Update()
    {
        var rec = SpatialRecorder.Instance;
        if (rec == null) return;

        // ── Cluster state: another client owns the recording session ──
        bool remoteIsRecording = rec.IsAnyoneRecording && !rec.LocalIsRecorder;

        if (remoteIsRecording && !isLockedForRemoteRecording)
        {
            isLockedForRemoteRecording = true;
            string playerName = ResolvePlayerName(rec.CurrentRecorderClientId);
            txtStatus.text = $"Recording by {playerName}";
            txtStatus.color = new Color(1f, 0.85f, 0.2f);
            btnRecord.interactable = false;
            btnStop.interactable = false;
            if (txtTimer != null) txtTimer.text = "--:--";
            if (imgRecordingIndicator != null) imgRecordingIndicator.color = new Color(1, 0, 0, 1);
        }
        else if (!remoteIsRecording && isLockedForRemoteRecording)
        {
            isLockedForRemoteRecording = false;
            if (!isRecordingUI) ResetRecorderUI();
        }

        // ── Local recording UI (only when *I* am the one recording) ──
        if (isRecordingUI)
        {
            if (txtTimer != null)
            {
                // SpatialRecorder marks startTime with Time.unscaledTime — use the same clock
                // here so the delta is meaningful. Clamp negatives to zero: same-frame ordering
                // where Update() ran before StartRecording finished flipping startTime yields a
                // tiny negative delta, which the old `Mathf.Floor + ToString("00")` pipeline
                // rendered as the "-01:-10" glitch you saw (Mathf.Floor rounds toward -∞,
                // C# modulo preserves sign, and the "00" format prints the minus).
                float t = Time.unscaledTime - rec.GetStartTime();
                if (t < 0f) t = 0f;
                int mins = (int)(t / 60f);
                int secs = (int)(t % 60f);
                txtTimer.text = $"{mins:D2}:{secs:D2}";
            }

            if (imgRecordingIndicator != null)
            {
                float alpha = Mathf.Abs(Mathf.Sin(Time.time * 3));
                Color c = imgRecordingIndicator.color;
                c.a = alpha;
                imgRecordingIndicator.color = c;
            }
        }
    }

    private string ResolvePlayerName(ulong clientId)
    {
        if (XRINetworkGameManager.Instance != null &&
            XRINetworkGameManager.Instance.TryGetPlayerByID(clientId, out XRINetworkPlayer player) &&
            player != null && !string.IsNullOrEmpty(player.playerName))
        {
            return player.playerName;
        }
        return $"Player {clientId}";
    }

    // --- Recorder Logic ---

    void StartRecording()
    {
        // בדיקה 1: האם המנוע קיים בכלל?
        if (SpatialRecorder.Instance == null)
        {
            txtStatus.text = "ERROR: No Recorder Script!";
            txtStatus.color = Color.magenta;
            return;
        }

        // Refuse if a remote client owns the recording session right now.
        if (SpatialRecorder.Instance.IsAnyoneRecording && !SpatialRecorder.Instance.LocalIsRecorder)
        {
            string playerName = ResolvePlayerName(SpatialRecorder.Instance.CurrentRecorderClientId);
            txtStatus.text = $"Already recording by {playerName}";
            txtStatus.color = new Color(1f, 0.85f, 0.2f);
            return;
        }

        // נסיון להתחיל הקלטה
        SpatialRecorder.Instance.StartRecording();
        
        // בדיקה 2: האם המנוע באמת התחיל? (אם לא, כנראה נכשל במציאת שחקנים)
        if (SpatialRecorder.Instance.isRecording)
        {
            isRecordingUI = true;
            txtStatus.text = "RECORDING LIVE...";
            txtStatus.color = Color.red;
            btnRecord.interactable = false;
            btnStop.interactable = true;
        }
        else
        {
            // אם המנוע סירב להתחיל, נציג הודעת שגיאה
            txtStatus.text = "ERROR: No Players Found?"; // כנראה בעיית התגית
            txtStatus.color = Color.yellow;
            
            // נחזיר את הכפתור להיות פעיל כדי לנסות שוב
            Invoke("ResetRecorderUI", 3f);
        }
    }

    void StopRecording()
    {
        if (SpatialRecorder.Instance != null) SpatialRecorder.Instance.StopRecording();
        
        isRecordingUI = false;
        
        // UI Updates
        txtStatus.text = "SAVED";
        txtStatus.color = Color.green;
        imgRecordingIndicator.color = new Color(1,0,0,1); // החזרת צבע מלא (בלי שקיפות)

        Invoke("ResetRecorderUI", 2f);
        Invoke("RefreshLibrary", 1f); // רענון הרשימה כדי לראות את הקובץ החדש
    }

    void ResetRecorderUI()
    {
        if (isRecordingUI) return; // הגנה
        if (isLockedForRemoteRecording) return; // someone else still owns the session
        txtStatus.text = "READY TO RECORD";
        txtStatus.color = Color.white;
        txtTimer.text = "00:00";
        btnRecord.interactable = true;
        btnStop.interactable = false;
    }

    // --- Library Logic ---

    public void RefreshLibrary()
    {
        // 1. ניקוי הרשימה הישנה
        foreach (Transform child in listContainer)
        {
            Destroy(child.gameObject);
        }

        // 2. מציאת הקבצים בתיקייה
        string path = Application.persistentDataPath;
        string[] files = Directory.GetFiles(path, "*.json"); // מחפש רק קבצי הקלטה

        if (files.Length == 0)
        {
            CreateListEntry("No recordings found.", "");
            return;
        }

        // 3. יצירת כפתור לכל קובץ
        foreach (string filePath in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            CreateListEntry(fileName, filePath);
        }
    }

    void CreateListEntry(string name, string fullPath)
    {
        GameObject newBtn = Instantiate(fileButtonPrefab, listContainer);
        
        // שינוי הטקסט של הכפתור
        TMP_Text btnText = newBtn.GetComponentInChildren<TMP_Text>();
        if (btnText) btnText.text = name;

        // הוספת פעולה בלחיצה (Play)
        Button btn = newBtn.GetComponent<Button>();
        if (btn && !string.IsNullOrEmpty(fullPath))
        {
            btn.onClick.AddListener(() => OnFileSelected(fullPath));
        }
    }

   void OnFileSelected(string path)
    {
        Debug.Log("📂 Selected Recording: " + path);
        txtStatus.text = "LOADING...";
        txtStatus.color = Color.yellow;

        // --- החיבור החדש ---
        if (ReplayManager.Instance != null)
        {
            ReplayManager.Instance.LoadAndPlay(path);
            
            txtStatus.text = "PLAYING ▶";
            txtStatus.color = Color.cyan;
        }
        else
        {
            Debug.LogError("❌ ReplayManager missing from scene!");
        }
    }
}