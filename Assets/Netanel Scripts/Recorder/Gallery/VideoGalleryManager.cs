using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using TMPro;
using UnityEngine.Networking;

public class VideoGalleryManager : MonoBehaviour
{
    [Header("--- 1. Views ---")]
    public GameObject galleryViewGroup;
    public GameObject videoPlayerPanel;

    [Header("--- 2. Recording ---")]
    public StaticVideoRecorder cameraRecorder; 
    public Button btnRecord;
    public Button btnStop;
    public TMP_Text statusText;

    [Header("--- 3. Gallery List ---")]
    public Transform contentParent;
    public GameObject videoButtonPrefab;

    [Header("--- 4. Player Display ---")]
    public Image playerScreen;             
    public Button btnExitPlayer;           
    public AudioSource playerAudio;        

    [Header("--- 5. Video Controls (NEW) ---")]
    public Slider videoSlider;      // סליידר הזמן
    public Button btnPlayPause;     // כפתור עצירה/נגינה
    public TMP_Text timeText;       // (אופציונלי) טקסט זמן 00:00 / 00:10
    public Sprite iconPlay;         // תמונה לכפתור כשהוא במצב Play
    public Sprite iconPause;        // תמונה לכפתור כשהוא במצב Pause

    // הגדרות
    private float frameRate = 10.0f;

    // משתנים פנימיים
    private string[] currentFrames;
    private bool isPlaying = false;    // האם הוידאו רץ כרגע?
    private bool isPlayerActive = false; // האם אנחנו בתוך מסך הנגן?
    private int currentFrameIndex = 0;
    private Texture2D activeFrameTexture; 

    void Start()
    {
        // חיבור כפתורים
        if (btnExitPlayer) btnExitPlayer.onClick.AddListener(CloseVideoPlayer);
        if (btnRecord) btnRecord.onClick.AddListener(OnRecordClicked);
        if (btnStop) btnStop.onClick.AddListener(OnStopClicked);
        
        // חיבור כפתורי שליטה חדשים
        if (btnPlayPause) btnPlayPause.onClick.AddListener(TogglePlayPause);
        if (videoSlider) 
        {
            // חשוב: מקשיבים לשינוי בסליידר כדי לאפשר גרירה
            videoSlider.onValueChanged.AddListener(OnSliderDragged);
        }

        ShowGalleryView();
    }

    // =========================================================
    //                   לוגיקת נגן (Player Logic)
    // =========================================================

    public void PlayVideo(string folderPath)
    {
        galleryViewGroup.SetActive(false);
        videoPlayerPanel.SetActive(true);

        // איפוס
        if (playerScreen) { playerScreen.color = Color.white; playerScreen.raycastTarget = false; }
        
        // טעינת תמונות
        if (Directory.Exists(folderPath))
        {
            currentFrames = Directory.GetFiles(folderPath, "*.jpg");
            System.Array.Sort(currentFrames);
        }

        // הגדרת הסליידר
        if (videoSlider)
        {
            videoSlider.minValue = 0;
            videoSlider.maxValue = currentFrames.Length - 1;
            videoSlider.value = 0;
        }

        // טעינת אודיו
        string audioPath = Path.Combine(folderPath, "audio.wav");
        if (File.Exists(audioPath)) StartCoroutine(LoadAndPlayAudio(audioPath));

        // התחלה
        isPlayerActive = true;
        isPlaying = true; // מתחיל מנגן
        currentFrameIndex = 0;
        UpdatePlayPauseIcon();
        
        StartCoroutine(VideoLoop());
    }

    IEnumerator VideoLoop()
    {
        WaitForSeconds waitInterval = new WaitForSeconds(1.0f / frameRate);

        while (isPlayerActive)
        {
            // אנחנו מעדכנים את הפריים רק אם לוחצים Play ועדיין לא נגמר הסרט
            if (isPlaying && currentFrames != null && currentFrameIndex < currentFrames.Length)
            {
                ShowFrame(currentFrameIndex); // הצגת הפריים

                // עדכון הסליידר ויזואלית (בלי להפעיל את האירוע של הגרירה)
                if (videoSlider)
                {
                    // שימוש ב-SetValueWithoutNotify כדי למנוע לולאה אינסופית
                    videoSlider.SetValueWithoutNotify(currentFrameIndex);
                }

                // עדכון טקסט זמן (אופציונלי)
                if (timeText) 
                {
                    timeText.text = $"{currentFrameIndex / frameRate:F1}s";
                }

                // בדיקה אם נגמר האודיו וצריך לעצור (אופציונלי)
                // אם רוצים שהוידאו יתקדם:
                currentFrameIndex++;
            }
            else if (currentFrameIndex >= currentFrames.Length)
            {
                // הגענו לסוף
                isPlaying = false;
                UpdatePlayPauseIcon();
            }

            // המתנה לקצב הפריימים
            yield return waitInterval;
        }
    }

    // פונקציה שמציגה פריים ספציפי (משמשת גם את הריצה וגם את הסליידר)
    void ShowFrame(int index)
    {
        if (currentFrames == null || index < 0 || index >= currentFrames.Length) return;

        string path = currentFrames[index];
        if (File.Exists(path))
        {
            byte[] data = File.ReadAllBytes(path);
            Texture2D newTex = new Texture2D(2, 2);
            if (newTex.LoadImage(data)) 
            {
                Sprite s = Sprite.Create(newTex, new Rect(0, 0, newTex.width, newTex.height), new Vector2(0.5f, 0.5f));

                if (activeFrameTexture != null) Destroy(activeFrameTexture);
                if (playerScreen.sprite != null && playerScreen.sprite.texture != null) Destroy(playerScreen.sprite);

                activeFrameTexture = newTex;
                playerScreen.sprite = s;
                //screen size
                playerScreen.transform.localScale = new Vector3(10, -5, 1);
            }
        }
    }

    // =========================================================
    //                   Video Controls (UI Functions)
    // =========================================================

    // נקרא כאשר המשתמש מזיז את הסליידר
    public void OnSliderDragged(float value)
    {
        currentFrameIndex = (int)value;
        ShowFrame(currentFrameIndex);
        
        // סנכרון אודיו (אם רוצים לקפוץ גם באודיו)
        if (playerAudio && playerAudio.clip)
        {
            playerAudio.time = currentFrameIndex / frameRate;
            if (!playerAudio.isPlaying && isPlaying) playerAudio.Play();
        }
    }

    // כפתור Play/Pause
    public void TogglePlayPause()
    {
        isPlaying = !isPlaying;
        
        if (isPlaying)
        {
            // אם היינו בסוף הסרטון ולחצנו Play - נתחיל מהתחלה
            if (currentFrameIndex >= currentFrames.Length - 1)
            {
                currentFrameIndex = 0;
                if (playerAudio) playerAudio.time = 0;
            }
            if (playerAudio) playerAudio.UnPause();
        }
        else
        {
            if (playerAudio) playerAudio.Pause();
        }

        UpdatePlayPauseIcon();
    }

    void UpdatePlayPauseIcon()
    {
        if (btnPlayPause == null) return;
        
        Image btnImg = btnPlayPause.GetComponent<Image>();
        if (btnImg)
        {
            if (isPlaying && iconPause != null) btnImg.sprite = iconPause;
            else if (!isPlaying && iconPlay != null) btnImg.sprite = iconPlay;
        }
    }

    public void CloseVideoPlayer()
    {
        isPlayerActive = false; // עוצר את הלולאה
        isPlaying = false;
        StopAllCoroutines();
        if (playerAudio) playerAudio.Stop();

        if (activeFrameTexture != null) Destroy(activeFrameTexture);
        if (playerScreen.sprite != null) Destroy(playerScreen.sprite);
        
        playerScreen.sprite = null;
        playerScreen.color = Color.black; 

        ShowGalleryView();
    }

    // =========================================================
    //                   שאר הפונקציות (ללא שינוי)
    // =========================================================
    public void ShowGalleryView()
    {
        videoPlayerPanel.SetActive(false);
        galleryViewGroup.SetActive(true);
        LoadThumbnails();
    }
    // ... (LoadThumbnails, OnRecordClicked, etc. - העתק מהקוד הקודם) ...
    // כדי לחסוך מקום, הנחתי שתעתיק את שאר הפונקציות (LoadThumbnails והלאה) מהקוד הקודם, הן לא השתנו.
    
    // הנה LoadThumbnails ליתר ביטחון:
    void LoadThumbnails()
    {
        foreach (Transform child in contentParent) Destroy(child.gameObject);
        string savePath = Application.persistentDataPath;
        if (!Directory.Exists(savePath)) return;
        string[] directories = Directory.GetDirectories(savePath, "Video_*");
        System.Array.Reverse(directories); 

        foreach (string dir in directories)
        {
            GameObject newBtn = Instantiate(videoButtonPrefab, contentParent);
            string firstFramePath = Path.Combine(dir, "frame_0000.jpg");
            if (File.Exists(firstFramePath))
            {
                byte[] fileData = File.ReadAllBytes(firstFramePath);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(fileData))
                {
                    Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    Transform thumbObj = newBtn.transform.Find("Thumbnail");
                    if (thumbObj != null) { thumbObj.GetComponent<Image>().sprite = s; thumbObj.localScale = new Vector3(1, -1, 1); }
                    else { newBtn.GetComponent<Image>().sprite = s; }
                }
            }
            TMP_Text btnText = newBtn.GetComponentInChildren<TMP_Text>();
            if (btnText != null) btnText.text = Path.GetFileName(dir).Replace("Video_", "").Replace("_", ":");
            string currentDir = dir; 
            newBtn.GetComponent<Button>().onClick.AddListener(() => PlayVideo(currentDir));
        }
    }
    void OnRecordClicked() { if (cameraRecorder) { cameraRecorder.StartRecording(); UpdateRecordButtons(true); if (statusText) statusText.text = "Recording..."; } }
    void OnStopClicked() { if (cameraRecorder) { cameraRecorder.StopRecording(); UpdateRecordButtons(false); if (statusText) statusText.text = "Saved."; Invoke("LoadThumbnails", 1.5f); } }
    void UpdateRecordButtons(bool isRecording) { if (btnRecord) btnRecord.interactable = !isRecording; if (btnStop) btnStop.interactable = isRecording; }
    IEnumerator LoadAndPlayAudio(string path) { using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV)) { yield return www.SendWebRequest(); if (www.result == UnityWebRequest.Result.Success) { playerAudio.clip = DownloadHandlerAudioClip.GetContent(www); playerAudio.Play(); } } }
}