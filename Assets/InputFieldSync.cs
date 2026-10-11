using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InputFieldSync : MonoBehaviour
{
    [SerializeField] private TMP_InputField tmpInput;
    [SerializeField] private InputField legacyInput;

    private bool isSyncing;

    private void Awake()
    {
        if (tmpInput != null)
            tmpInput.onValueChanged.AddListener(OnTmpChanged);

        if (legacyInput != null)
            legacyInput.onValueChanged.AddListener(OnLegacyChanged);
    }

    private void OnTmpChanged(string value)
    {
        if (isSyncing)
            return;

        isSyncing = true;
        legacyInput.text = value;
        isSyncing = false;
    }

    private void OnLegacyChanged(string value)
    {
        if (isSyncing)
            return;

        isSyncing = true;
        tmpInput.text = value;
        isSyncing = false;
    }
}
