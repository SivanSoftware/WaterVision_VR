using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro; // הוספנו את זה

[RequireComponent(typeof(PlayerAnalyticsState))]
public class PlayerSpeechAnalyticsReporter : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float sendEverySeconds = 2f;

    private MicRecorder mic;
    private PlayerAnalyticsState state;
    private TextMeshProUGUI playerNameText; // נמצא אוטומטית

    private float pendingTalkSeconds;
    private int pendingTurns;
    private float timer;

    private bool isSpeaking;
    private float speechStartTime;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            Debug.Log($"[PeopleAnalytics] Not owner - skipping Reporter setup. OwnerClientId={OwnerClientId}");
            return;
        }

        state = GetComponent<PlayerAnalyticsState>();
        if (state == null)
        {
            Debug.LogError("[PeopleAnalytics] ❌ PlayerAnalyticsState missing on this object.");
            return;
        }

        // מצא את TextMeshPro של השם
        FindPlayerNameText();

        // שימוש ב-Singleton
        mic = MicRecorder.Instance;

        if (mic == null)
        {
            Debug.LogError("[PeopleAnalytics] ❌ MicRecorder.Instance is null! Make sure MicRecorder exists in the scene.");
            return;
        }

        // לוגים מפורטים
        Debug.Log($"[PeopleAnalytics] ✅ Found MicRecorder: Name='{mic.gameObject.name}', " +
                  $"InstanceID={mic.GetInstanceID()}, " +
                  $"IsActive={mic.gameObject.activeInHierarchy}, " +
                  $"Enabled={mic.enabled}");

        // רישום לאירועים
        mic.OnSpeechStart += HandleSpeechStart;
        mic.OnSpeechEnd += HandleSpeechEnd;

        Debug.Log($"[PeopleAnalytics] 🎤 Reporter ready for Player '{gameObject.name}'. " +
                  $"OwnerClientId={OwnerClientId}, " +
                  $"IsLocalPlayer={IsLocalPlayer}");
    }

    private void FindPlayerNameText()
    {
        // חפש Player Name Tag -> Name -> TextMeshProUGUI
        Transform nameTag = transform.Find("Player Name Tag");
        if (nameTag != null)
        {
            Transform nameObj = nameTag.Find("Name");
            if (nameObj != null)
            {
                playerNameText = nameObj.GetComponent<TextMeshProUGUI>();
                if (playerNameText != null)
                {
                    Debug.Log($"[PeopleAnalytics] ✅ Found player name text: '{playerNameText.text}'");
                    return;
                }
            }
        }

        // אם לא מצאנו, חפש בכל הילדים
        var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in allTexts)
        {
            if (txt.gameObject.name == "Name")
            {
                playerNameText = txt;
                Debug.Log($"[PeopleAnalytics] ✅ Found player name text (recursive): '{txt.text}'");
                return;
            }
        }

        Debug.LogWarning("[PeopleAnalytics] ⚠️ Could not find player name TextMeshPro!");
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (mic != null)
        {
            mic.OnSpeechStart -= HandleSpeechStart;
            mic.OnSpeechEnd -= HandleSpeechEnd;
            Debug.Log($"[PeopleAnalytics] Unsubscribed from MicRecorder events.");
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        timer += Time.deltaTime;
        if (timer < sendEverySeconds) return;
        timer = 0f;

        FlushIfNeeded();
    }

    private void HandleSpeechStart()
    {
        Debug.Log($"[PeopleAnalytics] 🗣️ HandleSpeechStart FIRED! Player: {gameObject.name}");
        
        pendingTurns += 1;
        isSpeaking = true;
        speechStartTime = Time.time;
        
        Debug.Log($"[PeopleAnalytics] PendingTurns now: {pendingTurns}");
    }

    private void HandleSpeechEnd(float durationSecFromMic)
    {
        Debug.Log($"[PeopleAnalytics] 🤫 HandleSpeechEnd FIRED! Player: {gameObject.name}, Duration from Mic: {durationSecFromMic:F2}s");
        
        float computed = 0f;

        if (isSpeaking)
        {
            computed = Time.time - speechStartTime;
            Debug.Log($"[PeopleAnalytics] Computed duration from Timer: {computed:F2}s");
        }
        else
        {
            computed = Mathf.Max(0f, durationSecFromMic);
            Debug.Log($"[PeopleAnalytics] Using Mic duration: {computed:F2}s");
        }

        isSpeaking = false;

        pendingTalkSeconds += Mathf.Max(0f, computed);
        
        Debug.Log($"[PeopleAnalytics] PendingTalkSeconds now: {pendingTalkSeconds:F2}s, PendingTurns: {pendingTurns}");
        
        FlushIfNeeded();
    }

    private void FlushIfNeeded()
    {
        if (state == null)
        {
            Debug.LogWarning("[PeopleAnalytics] ⚠️ Cannot flush, state is null!");
            return;
        }

        if (pendingTalkSeconds <= 0f && pendingTurns <= 0)
            return;

        Debug.Log($"[PeopleAnalytics] 📤 FLUSHING: TalkSeconds={pendingTalkSeconds:F2}s, Turns={pendingTurns}");

        // 1) קודם כל נשתמש בשם שכבר שמור ב NetworkVariable
        FixedString64Bytes displayName = state.DisplayName.Value;

        // 2) אם אין שם שמור, ננסה לקחת מה TextMeshPro
        if (displayName.Length == 0 && playerNameText != null && !string.IsNullOrEmpty(playerNameText.text))
        {
            displayName = new FixedString64Bytes(playerNameText.text);
            Debug.Log($"[PeopleAnalytics] Using name from TextMeshPro: '{displayName}'");
        }

        // 3) אם עדיין אין שם, רק אז ברירת מחדל
        if (displayName.Length == 0)
        {
            displayName = new FixedString64Bytes($"Player_{OwnerClientId}");
            Debug.Log($"[PeopleAnalytics] Using default name: '{displayName}'");
        }

        Debug.Log($"[PeopleAnalytics] Before AddDelta, State.DisplayName='{state.DisplayName.Value}', TalkSeconds={state.TalkSeconds.Value:F2}, Turns={state.Turns.Value}");

        state.AddDelta(pendingTalkSeconds, pendingTurns, displayName);

        Debug.Log($"[PeopleAnalytics] ✅ After AddDelta, State.DisplayName='{state.DisplayName.Value}', TalkSeconds={state.TalkSeconds.Value:F2}, Turns={state.Turns.Value}");

        pendingTalkSeconds = 0f;
        pendingTurns = 0;
    }

}