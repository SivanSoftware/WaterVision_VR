using UnityEngine;
using XRMultiplayer;

/// <summary>
/// Plays a small one-shot SFX (water-drop confirmation) the moment the local player
/// successfully joins the multiplayer room. Subscribes to <see cref="XRINetworkGameManager.Connected"/>
/// and fires once on every false→true transition (so reconnecting plays it again).
///
/// Setup: place a GameObject in the scene with this component + an AudioSource, and
/// drag <c>water_drop.mp3</c> into the <see cref="clip"/> field. The script auto-adds
/// an AudioSource if one is missing.
/// </summary>
[DisallowMultipleComponent]
public class RoomJoinSfx : MonoBehaviour
{
    [SerializeField, Tooltip("Clip played when the local player joins the room.")]
    AudioClip clip;

    [SerializeField, Range(0f, 1f), Tooltip("Playback volume.")]
    float volume = 0.7f;

    [SerializeField, Tooltip("Also play when the editor enters Play and the manager " +
        "reports Connected=true synchronously on the first frame. Leave true for normal " +
        "use — turn off only if you see a duplicate trigger during a hot domain reload.")]
    bool fireOnInitialTrue = true;

    AudioSource m_Src;
    bool m_LastConnected = false;

    void Awake()
    {
        m_Src = GetComponent<AudioSource>();
        if (m_Src == null) m_Src = gameObject.AddComponent<AudioSource>();
        m_Src.playOnAwake  = false;
        m_Src.spatialBlend = 0f;     // 2D — bypass spatial attenuation so it always plays at full volume

        XRINetworkGameManager.Connected.Subscribe(OnConnectedChanged);
    }

    void Start()
    {
        // Catch the case where Connected was already true by the time we subscribed
        // (e.g. quick reconnect in editor + assembly reload).
        if (fireOnInitialTrue && XRINetworkGameManager.Connected.Value && !m_LastConnected)
            OnConnectedChanged(true);
    }

    void OnDestroy()
    {
        XRINetworkGameManager.Connected.Unsubscribe(OnConnectedChanged);
    }

    void OnConnectedChanged(bool isConnected)
    {
        // Only fire on the false→true edge — disconnect events shouldn't play the drop.
        if (isConnected && !m_LastConnected)
        {
            if (clip != null && m_Src != null) m_Src.PlayOneShot(clip, volume);
            else Debug.LogWarning("[RoomJoinSfx] Connected but no AudioClip assigned.", this);
        }
        m_LastConnected = isConnected;
    }
}
