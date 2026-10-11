using TMPro;
using UnityEngine;

public class PlayerNameUI : MonoBehaviour
{
    private const string PrefKey = "WV_PlayerName";

    [Header("UI")]
    [SerializeField] private TMP_InputField nameInput;

    public void OnConfirmClicked()
    {
        if (nameInput == null)
        {
            Debug.LogError("[PlayerNameUI] nameInput is not assigned.");
            return;
        }

        string n = (nameInput.text ?? "").Trim();

        if (string.IsNullOrEmpty(n))
        {
            Debug.LogWarning("[PlayerNameUI] Empty name - not saving.");
            return;
        }

        PlayerPrefs.SetString(PrefKey, n);
        PlayerPrefs.Save();

        Debug.Log($"[PlayerNameUI] Saved player name: '{n}'");
    }

    // אם תרצה שבפתיחה זה ימלא את השדה מהשם הקודם
    private void OnEnable()
    {
        if (nameInput == null) return;

        string saved = PlayerPrefs.GetString(PrefKey, "");
        if (!string.IsNullOrEmpty(saved) && string.IsNullOrEmpty(nameInput.text))
            nameInput.text = saved;
    }
}
