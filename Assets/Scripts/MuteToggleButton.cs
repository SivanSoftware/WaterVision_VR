using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MuteToggleButton : MonoBehaviour
{
    [Header("UI")]
    public Image buttonImage;
    public TMP_Text labelText;
    public GameObject iconMute;
    public GameObject iconUnmute;

    [Header("Colors")]
    public Color mutedColor = Color.green;
    public Color unmutedColor = Color.red;

    private bool isMuted = false;

    public void ToggleMute()
    {
        isMuted = !isMuted;
        UpdateUI();

        Debug.Log(isMuted ? "MIC MUTED" : "MIC UNMUTED");
    }

    void Start()
    {
        UpdateUI();
    }

    void UpdateUI()
    {
        if (iconMute) iconMute.SetActive(isMuted);
        if (iconUnmute) iconUnmute.SetActive(!isMuted);

        if (labelText)
            labelText.text = isMuted ? "Mute" : "Unmute";

        if (buttonImage)
            buttonImage.color = isMuted ? mutedColor : unmutedColor;
    }
}
