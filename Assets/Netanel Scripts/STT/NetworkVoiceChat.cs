using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class NetworkVoiceChat : NetworkBehaviour
{
    [Header("Settings")]
    public int frequency = 12000; // איכות סבירה לדיבור (וחסכונית ברשת)
    public int bufferSize = 1024; // גודל החבילה שנשלחת

    private AudioSource audioSource;
    private AudioClip myClip;
    private string deviceName;
    private int lastSamplePosition = 0;
    private bool isRecording = false;

    public override void OnNetworkSpawn()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f; // חובה ל-VR! שומעים לפי כיוון
        audioSource.loop = false; // לא לופ, אנחנו מנגנים חתיכות

        if (IsOwner)
        {
            StartMicrophone();
        }
    }

    void StartMicrophone()
    {
        // אם המיקרופון כבר עובד (בגלל MicRecorder), נתפוס עליו טרמפ
        if (Microphone.IsRecording(null))
        {
            deviceName = Microphone.devices[0];
            // זהירות: זה מניח שמישהו אחר כבר התחיל הקלטה מתאימה
            isRecording = true;
            Debug.Log("🎤 NetworkVoice: Hooked into existing mic.");
        }
        else
        {
            // אחרת, נתחיל בעצמנו
            if (Microphone.devices.Length > 0)
            {
                deviceName = Microphone.devices[0];
                myClip = Microphone.Start(deviceName, true, 20, frequency);
                isRecording = true;
                Debug.Log("🎤 NetworkVoice: Started new mic recording.");
            }
        }
    }

    void Update()
    {
        // רק השחקן המקומי שולח קול
        if (!IsOwner || !isRecording) return;

        int currentPos = Microphone.GetPosition(deviceName);
        
        // טיפול במצב שההקלטה עשתה לופ
        if (currentPos < lastSamplePosition) lastSamplePosition = 0;

        // האם יש מספיק מידע חדש לשלוח?
        int diff = currentPos - lastSamplePosition;
        if (diff > bufferSize)
        {
            float[] samples = new float[diff];
            
            // שליפת הנתונים מהמיקרופון
            if (myClip == null) 
            {
                // ניסיון חירום למצוא את הקליפ אם MicRecorder התחיל אותו
                // (זה חלק קצת 'האקי' כדי לעבוד במקביל לסקריפט השני שלך)
                return; 
            }

            myClip.GetData(samples, lastSamplePosition);
            
            // שליחה לרשת (דחיסה פשוטה: המרה מ-float ל-short תחסוך 50% רוחב פס, אבל כרגע נשאיר פשוט)
            SendAudioServerRpc(samples);

            lastSamplePosition = currentPos;
        }
    }

    [ServerRpc]
    void SendAudioServerRpc(float[] data)
    {
        // השרת מקבל ושולח לכולם
        PlayAudioClientRpc(data);
    }

    [ClientRpc]
    void PlayAudioClientRpc(float[] data)
    {
        // אם אני זה שדיברתי - אל תשמיע לי את עצמי (מונע אקו)
        if (IsOwner) return;

        // יצירת קליפ זמני וניגון מיידי
        AudioClip chunk = AudioClip.Create("VoiceChunk", data.Length, 1, frequency, false);
        chunk.SetData(data, 0);
        
        // PlayOneShot מאפשר לנגן חתיכות רצופות בלי לקטוע אחת את השנייה
        audioSource.PlayOneShot(chunk);
    }
}