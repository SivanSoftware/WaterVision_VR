using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerAnalyticsState : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> DisplayName =
        new NetworkVariable<FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    public NetworkVariable<float> TalkSeconds =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    public NetworkVariable<int> Turns =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[AnalyticsState] OnNetworkSpawn - GameObject: '{gameObject.name}', " +
                  $"IsOwner={IsOwner}, " +
                  $"IsLocalPlayer={IsLocalPlayer}, " +
                  $"OwnerClientId={OwnerClientId}, " +
                  $"NetworkObjectId={NetworkObjectId}");

        if (!IsOwner)
        {
            Debug.Log($"[AnalyticsState] Not owner - skipping initialization.");
            return;
        }

        if (DisplayName.Value.Length == 0)
        {
            DisplayName.Value = new FixedString64Bytes(gameObject.name);
            Debug.Log($"[AnalyticsState] Set DisplayName to '{DisplayName.Value}'");
        }

        // רישום לשינויים (לדיבאג)
        TalkSeconds.OnValueChanged += OnTalkSecondsChanged;
        Turns.OnValueChanged += OnTurnsChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            TalkSeconds.OnValueChanged -= OnTalkSecondsChanged;
            Turns.OnValueChanged -= OnTurnsChanged;
        }
    }

    private void OnTalkSecondsChanged(float previousValue, float newValue)
    {
        Debug.Log($"[AnalyticsState] 📊 TalkSeconds changed: {previousValue:F2}s → {newValue:F2}s (Player: {gameObject.name})");
    }

    private void OnTurnsChanged(int previousValue, int newValue)
    {
        Debug.Log($"[AnalyticsState] 📊 Turns changed: {previousValue} → {newValue} (Player: {gameObject.name})");
    }

    public void AddDelta(float talkDelta, int turnsDelta, FixedString64Bytes name)
    {
        Debug.Log($"[AnalyticsState] 🔧 AddDelta called - IsOwner={IsOwner}, " +
                  $"talkDelta={talkDelta:F2}s, turnsDelta={turnsDelta}, name='{name}'");

        if (!IsOwner)
        {
            Debug.LogError($"[AnalyticsState] ❌ AddDelta called but NOT OWNER! " +
                          $"OwnerClientId={OwnerClientId}, LocalClientId={NetworkManager.Singleton.LocalClientId}");
            return;
        }

        if (name.Length > 0 && name != DisplayName.Value)
        {
            string incoming = name.ToString();
            string current = DisplayName.Value.ToString();

            bool incomingIsDefault = incoming.StartsWith("Player_");
            bool currentIsReal = DisplayName.Value.Length > 0 && !current.StartsWith("Player_");

            // לא דורסים שם אמיתי עם Player_#
            if (!(incomingIsDefault && currentIsReal))
            {
                Debug.Log($"[AnalyticsState] Updating DisplayName: '{DisplayName.Value}' → '{name}'");
                DisplayName.Value = name;
            }
            else
            {
                Debug.Log($"[AnalyticsState] Keeping real DisplayName '{DisplayName.Value}', ignoring incoming '{name}'");
            }
        }


        float oldTalk = TalkSeconds.Value;
        int oldTurns = Turns.Value;

        float newTalk = Mathf.Max(0f, TalkSeconds.Value + Mathf.Max(0f, talkDelta));
        int newTurns = Mathf.Max(0, Turns.Value + Mathf.Max(0, turnsDelta));

        Debug.Log($"[AnalyticsState] Updating values - TalkSeconds: {oldTalk:F2}→{newTalk:F2}, Turns: {oldTurns}→{newTurns}");

        TalkSeconds.Value = newTalk;
        Turns.Value = newTurns;

        Debug.Log($"[AnalyticsState] ✅ Values set. Current TalkSeconds={TalkSeconds.Value:F2}, Turns={Turns.Value}");
    }
}