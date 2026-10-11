using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PlayerAnalyticsState))]
public class ApplyPlayerNameOnSpawn : NetworkBehaviour
{
    private const string PrefKey = "WV_PlayerName";

    [Header("Optional - if you want to force-set the floating name tag too")]
    [SerializeField] private TextMeshProUGUI nameText; // Player Name Tag/Name

    private PlayerAnalyticsState state;

    public override void OnNetworkSpawn()
    {
        state = GetComponent<PlayerAnalyticsState>();

        if (!IsOwner) return;

        string saved = PlayerPrefs.GetString(PrefKey, "").Trim();

        // אם אין שם שמור, אל תדרוס (יישאר ברירת מחדל)
        if (string.IsNullOrEmpty(saved))
        {
            Debug.Log("[ApplyPlayerNameOnSpawn] No saved name in PlayerPrefs.");
            return;
        }

        var fs = new FixedString64Bytes(saved);

        // 1) רשת: זה מה שהעוגה/אנליטיקס צריכים לקרוא
        state.DisplayName.Value = fs;

        // 2) UI מקומי: אם יש name tag, עדכן גם אותו
        if (nameText == null)
        {
            // נסיון למצוא אוטומטית
            Transform t = transform.Find("Player Name Tag/Name");
            if (t != null) nameText = t.GetComponent<TextMeshProUGUI>();
        }

        if (nameText != null)
            nameText.text = saved;

        Debug.Log($"[ApplyPlayerNameOnSpawn] Applied name '{saved}' to NetworkVariable + NameTag.");
    }
}
