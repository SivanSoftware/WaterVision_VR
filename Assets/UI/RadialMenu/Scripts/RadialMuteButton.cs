using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Vivox;

/// <summary>
/// Radial menu Mute button. Toggles the mic mute state via Vivox and updates UI accordingly.
/// </summary>
public class RadialMuteButton : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text labelText;

    [Header("Sprites")]
    [SerializeField] private Sprite iconUnmuted;  // mic.png
    [SerializeField] private Sprite iconMuted;    // mic-off.png

    [Header("Label Text")]
    [SerializeField] private string textWhenUnmuted = "Mute";
    [SerializeField] private string textWhenMuted   = "Unmute";

    private bool _isMuted = false;

    void Start()
    {
        UpdateVisuals();
    }

    public void ToggleMute()
    {
        _isMuted = !_isMuted;
        UpdateVisuals();

        // 🎤 Actually mute/unmute via Vivox
        if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
        {
            if (_isMuted)
                VivoxService.Instance.MuteInputDevice();
            else
                VivoxService.Instance.UnmuteInputDevice();
        }
        else
        {
            Debug.LogWarning("[RadialMute] VivoxService not available — mute is visual only");
        }

        Debug.Log(_isMuted ? "[RadialMute] 🔇 MIC MUTED (Vivox)" : "[RadialMute] 🎤 MIC UNMUTED (Vivox)");
    }

    private void UpdateVisuals()
    {
        if (iconImage != null)
            iconImage.sprite = _isMuted ? iconMuted : iconUnmuted;

        if (labelText != null)
            labelText.text = _isMuted ? textWhenMuted : textWhenUnmuted;
    }
}