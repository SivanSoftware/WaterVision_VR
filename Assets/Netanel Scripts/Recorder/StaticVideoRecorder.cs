using UnityEngine;
using System.IO;
using System.Collections;
using System;
using TMPro; // אם אתה רוצה סטטוס טקסט

public class StaticVideoRecorder : MonoBehaviour
{
    [Header("Hardware")]
    public RenderTexture camTexture; // הטקסטורה של המצלמה
    public GameObject recIcon;       // הנורה האדומה על המצלמה (אופציונלי)
    
    [Header("UI Feedback")]
    public TMP_Text statusText;      // (אופציונלי) טקסט בתפריט שכותב "מקליט..."

    [Header("Settings")]
    public int framesPerSecond = 10; 

    private bool isRecording = false;
    private string currentSessionPath;
    private string micDevice;
    private AudioClip audioClip;

    void Start()
    {
        if (Microphone.devices.Length > 0) micDevice = Microphone.devices[0];
        if (recIcon) recIcon.SetActive(false);
        UpdateStatus("Ready to Record");
    }

    // --- פונקציות ציבוריות לחיבור לכפתורי UI ---

    public void StartRecording()
    {
        if (isRecording) return; // מניעת לחיצה כפולה

        isRecording = true;
        if (recIcon) recIcon.SetActive(true);
        UpdateStatus("🔴 Recording...");

        // 1. יצירת תיקייה
        string sessionName = $"Video_{DateTime.Now:yyyyMMdd_HHmmss}";
        currentSessionPath = Path.Combine(Application.persistentDataPath, sessionName);
        Directory.CreateDirectory(currentSessionPath);

        // 2. הקלטת סאונד
        if (micDevice != null)
        {
            audioClip = Microphone.Start(micDevice, false, 60, 22050); 
        }

        // 3. הקלטת וידאו
        StartCoroutine(CaptureLoop());
        
        Debug.Log($"Started recording to: {currentSessionPath}");
    }

    public void StopRecording()
    {
        if (!isRecording) return;

        isRecording = false;
        if (recIcon) recIcon.SetActive(false);
        UpdateStatus("Saved ✅");

        // שמירת סאונד
        if (micDevice != null)
        {
            int pos = Microphone.GetPosition(micDevice);
            Microphone.End(micDevice);
            if(audioClip != null)
                SavWav.Save(Path.Combine(currentSessionPath, "audio"), TrimClip(audioClip, pos));
        }

        Debug.Log("Recording stopped.");
    }

    // --- לוגיקה פנימית (אותו דבר כמו קודם) ---

    IEnumerator CaptureLoop()
    {
        int frameCount = 0;
        float interval = 1.0f / framesPerSecond;
        Texture2D frameTex = new Texture2D(camTexture.width, camTexture.height, TextureFormat.RGB24, false);

        while (isRecording)
        {
            yield return new WaitForEndOfFrame(); 

            RenderTexture.active = camTexture;
            frameTex.ReadPixels(new Rect(0, 0, camTexture.width, camTexture.height), 0, 0);
            frameTex.Apply();
            RenderTexture.active = null;

            byte[] bytes = frameTex.EncodeToJPG(50); 
            string filename = $"frame_{frameCount:D4}.jpg"; 
            File.WriteAllBytes(Path.Combine(currentSessionPath, filename), bytes);

            frameCount++;
            yield return new WaitForSeconds(interval);
        }
        Destroy(frameTex); // ניקוי זיכרון
    }

    AudioClip TrimClip(AudioClip original, int endSample)
    {
        if (endSample <= 0) return original;
        float[] data = new float[endSample * original.channels];
        original.GetData(data, 0);
        AudioClip newClip = AudioClip.Create("Trimmed", endSample, original.channels, original.frequency, false);
        newClip.SetData(data, 0);
        return newClip;
    }

    void UpdateStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }
}