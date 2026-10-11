using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class PlaybackControlUI : MonoBehaviour
{
    [Header("UI Components")]
    public Slider progressSlider;
    public TMP_Text timeText;
    public Button btnPlayPause;
    public Button btnStop;
    public TMP_Text txtPlayPause; 

    private bool isDragging = false;

    void Start()
    {
        btnPlayPause.onClick.AddListener(TogglePlayPause);
        btnStop.onClick.AddListener(StopPlayback);
        
        // --- החיבור החדש לשינוי זמן ---
        progressSlider.onValueChanged.AddListener(OnSliderValueChange);
        
        SetupSliderEvents();
    }

    void Update()
    {
        // מעדכן את הסליידר ויזואלית רק אם המשתמש לא נוגע בו כרגע
        if (ReplayManager.Instance != null && ReplayManager.Instance.IsActive && !isDragging)
        {
            float current = ReplayManager.Instance.GetCurrentTime();
            float total = ReplayManager.Instance.GetTotalDuration();

            progressSlider.maxValue = total;
            progressSlider.value = current;

            timeText.text = $"{FormatTime(current)} / {FormatTime(total)}";
        }
    }

    // --- הפונקציה החדשה שמופעלת בגרירה ---
    public void OnSliderValueChange(float val)
    {
        // אנחנו מעדכנים את המנוע רק אם המשתמש גורר (isDragging)
        // זה מונע לולאה אינסופית שבה המנוע מעדכן את הסליידר שמעדכן את המנוע...
        if (isDragging && ReplayManager.Instance != null)
        {
            ReplayManager.Instance.JumpToTime(val);
            
            // עדכון הטקסט מיד כדי שיהיה פידבק נעים לעין
            timeText.text = $"{FormatTime(val)} / {FormatTime(ReplayManager.Instance.GetTotalDuration())}";
        }
    }

    void SetupSliderEvents()
    {
        EventTrigger trigger = progressSlider.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = progressSlider.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry entryDown = new EventTrigger.Entry();
        entryDown.eventID = EventTriggerType.PointerDown;
        entryDown.callback.AddListener((data) => { isDragging = true; });
        trigger.triggers.Add(entryDown);

        EventTrigger.Entry entryUp = new EventTrigger.Entry();
        entryUp.eventID = EventTriggerType.PointerUp;
        entryUp.callback.AddListener((data) => { isDragging = false; });
        trigger.triggers.Add(entryUp);
    }

    void TogglePlayPause()
    {
        ReplayManager.Instance.TogglePause();
        UpdatePlayButtonText();
    }

    void StopPlayback()
    {
        ReplayManager.Instance.StopReplay();
    }

    public void OnShow()
    {
        gameObject.SetActive(true);
        UpdatePlayButtonText();
    }

    public void OnHide()
    {
        gameObject.SetActive(false);
    }

    void UpdatePlayButtonText()
    {
        if (ReplayManager.Instance.isPlaying)
        {
            txtPlayPause.text = "⏸ Playing"; 
        }
        else
        {
            if (ReplayManager.Instance.GetCurrentTime() < 0.1f)
                txtPlayPause.text = "▶ Play";
            else
                txtPlayPause.text = "▶ Resume";
        }
    }

    string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60F);
        int seconds = Mathf.FloorToInt(time - minutes * 60);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}