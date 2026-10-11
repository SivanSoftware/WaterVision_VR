using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Plays a short hover and click sound for every interactive child of this
/// GameObject. Attach to the RadialMenu root (the World-Space Canvas).
///
/// **Design note (very important):** the click sound is wired by SUBSCRIBING
/// to <see cref="Button.onClick"/> on every Button in the subtree — NOT by
/// implementing <see cref="IPointerClickHandler"/> on the receiver. The first
/// version did the latter and that caused a regression where the audio
/// receiver intercepted the pointer event before it could bubble up to the
/// parent Button (the EventSystem stops on the first handler that handles an
/// event), so users had to click twice for the menu to react. Subscribing to
/// the existing Button event is invisible to the EventSystem — the click
/// goes to Button.OnPointerClick exactly as before, and we just hear the
/// invocation.
///
/// Hover is fine to keep as IPointerEnter because there is no equivalent
/// "onHover" UnityEvent on Button, and Button itself doesn't claim that
/// interface — so adding a sibling IPointerEnterHandler is safe and triggers
/// alongside Button's own enter/exit visual transitions.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class RadialMenuAudioFeedback : MonoBehaviour
{
    [Tooltip("Played when the controller ray enters a button. ~0.2s SFX recommended (e.g. Button_14_Hover.wav).")]
    [SerializeField] AudioClip hoverClip;

    [Tooltip("Played when the controller trigger fires Button.onClick. ~0.3s SFX recommended (e.g. Button_22_Click.wav).")]
    [SerializeField] AudioClip clickClip;

    [Range(0f, 1f)]
    [Tooltip("Volume multiplier applied to both clips via AudioSource.PlayOneShot.")]
    [SerializeField] float volume = 0.6f;

    AudioSource m_AudioSource;

    void Awake()
    {
        m_AudioSource = GetComponent<AudioSource>();
        m_AudioSource.spatialBlend = 0f;        // 2D — UI sound, not positioned in world
        m_AudioSource.playOnAwake = false;

        AttachReceivers();
    }

    void AttachReceivers()
    {
        // Find every Selectable (Button, Toggle, etc.) in the subtree. We
        // attach a receiver on each Selectable's GameObject — never on inner
        // child Graphics like Glow rings, icons or labels. Attaching to inner
        // children would either steal events (the OLD bug) or duplicate audio.
        foreach (var sel in GetComponentsInChildren<Selectable>(includeInactive: true))
        {
            if (sel.GetComponent<RadialMenuAudioReceiver>() == null)
            {
                var receiver = sel.gameObject.AddComponent<RadialMenuAudioReceiver>();
                receiver.Initialize(this);
            }
        }
    }

    internal void PlayHover()
    {
        if (hoverClip != null && m_AudioSource != null)
            m_AudioSource.PlayOneShot(hoverClip, volume);
    }

    internal void PlayClick()
    {
        if (clickClip != null && m_AudioSource != null)
            m_AudioSource.PlayOneShot(clickClip, volume);
    }
}

/// <summary>
/// Per-button audio listener installed automatically by
/// <see cref="RadialMenuAudioFeedback"/>, or pre-attached to a button prefab
/// (e.g. <c>RadialButton.prefab</c>). Plays hover via
/// <see cref="IPointerEnterHandler"/> and click by hooking
/// <see cref="Button.onClick"/> directly so the click never gets intercepted.
/// </summary>
public class RadialMenuAudioReceiver : MonoBehaviour, IPointerEnterHandler
{
    RadialMenuAudioFeedback m_Feedback;
    Button m_Button;
    bool m_ClickHooked;

    void Start()
    {
        // Self-bootstrap fallback for receivers pre-attached on a button prefab.
        if (m_Feedback == null)
        {
            m_Feedback = GetComponentInParent<RadialMenuAudioFeedback>(includeInactive: true);
        }
        HookButtonClick();
    }

    void OnEnable()
    {
        // Re-hook on enable in case the listener was lost (prefab reloads, etc.)
        if (m_Feedback != null) HookButtonClick();
    }

    void OnDisable()
    {
        UnhookButtonClick();
    }

    void HookButtonClick()
    {
        if (m_ClickHooked) return;
        if (m_Button == null) m_Button = GetComponent<Button>();
        if (m_Button == null) return;       // no Button on this GO — hover-only
        m_Button.onClick.AddListener(OnClickSound);
        m_ClickHooked = true;
    }

    void UnhookButtonClick()
    {
        if (!m_ClickHooked) return;
        if (m_Button != null) m_Button.onClick.RemoveListener(OnClickSound);
        m_ClickHooked = false;
    }

    void OnClickSound()
    {
        if (m_Feedback != null) m_Feedback.PlayClick();
    }

    internal void Initialize(RadialMenuAudioFeedback feedback)
    {
        m_Feedback = feedback;
    }

    void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
    {
        if (m_Feedback != null) m_Feedback.PlayHover();
    }
}
