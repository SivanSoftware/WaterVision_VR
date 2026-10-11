using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XRMultiplayer;

/// <summary>
/// "Sign-language assist" bubble: a world-space panel that, when enabled, shows the current
/// speaker's name + caption and displays a LIVE MIXAMO CHARACTER performing each ASL letter
/// (via ASLSigningCharacterManager → RenderTexture). Falls back to baked sprites (Resources/ASL)
/// if the character manager isn't ready yet.
///
/// Local, per-user view preference toggled from the radial menu — NOT networked; listens to
/// MessageManager.OnMessageReceived which fans transcripts to every client.
///
/// Self-contained: bootstrapped at runtime (no scene object), builds its own UI in code (no
/// prefab), parented to the XR camera (not DDOL) so URP renders it. Persists on/off in
/// PlayerPrefs.
/// </summary>
public class SignLanguageBubbleManager : MonoBehaviour
{
    public static SignLanguageBubbleManager Instance { get; private set; }

    const string PREF            = "ASL_Bubble_Enabled";
    const float  LETTER_INTERVAL = 0.45f;   // seconds per letter
    const float  WORD_GAP        = 0.50f;   // pause between words

    bool _enabled;
    public bool IsEnabled => _enabled;

    // Sprite fallback (used when ASLSigningCharacterManager isn't ready yet)
    readonly Dictionary<char, Texture2D> _hand = new();
    Texture2D _rest;

    GameObject _root;
    RawImage   _handImg;     // shows RT (live character) or sprite (fallback)
    TMP_Text   _header, _caption;
    readonly Queue<(string text, string sender, Color color)> _queue = new();
    Coroutine _player;

    // ── Bootstrap ────────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("SignLanguageBubbleManager");
        DontDestroyOnLoad(go);
        go.AddComponent<SignLanguageBubbleManager>();
        Debug.Log("[ASL] SignLanguageBubbleManager bootstrapped.");
    }

    void Awake()
    {
        Instance = this;
        LoadSprites();
        BuildBubble();
        AttachToCamera();
        StartCoroutine(WireRT());       // waits for ASLSigningCharacterManager then wires RT
        MessageManager.OnMessageReceived += OnMessage;
        _enabled = false;
        if (_root) _root.SetActive(false);
    }

    // Parent the canvas to the XR camera so it lives inside the active scene
    // (not DontDestroyOnLoad) and is always rendered by the scene camera.
    void AttachToCamera()
    {
        var cam = FindCam();
        if (cam != null && _root != null)
        {
            _root.transform.SetParent(cam.transform, false);
            _root.transform.localPosition = new Vector3(0.22f, -0.17f, 0.9f);
            _root.transform.localRotation = Quaternion.identity;
        }
    }

    // Wire the signing character's RenderTexture once it is ready.
    // Falls through immediately if _handImg is already showing the RT (e.g. on scene reload).
    IEnumerator WireRT()
    {
        // Wait up to ~60 s for the character manager to finish spawning the avatar
        float timeout = 60f;
        while (timeout > 0f)
        {
            var cm = ASLSigningCharacterManager.Instance;
            if (cm != null && cm.IsReady)
            {
                if (_handImg != null) _handImg.texture = cm.RT;
                Debug.Log("[ASL] Signed character RenderTexture wired.");
                yield break;
            }
            yield return null;
            timeout -= Time.deltaTime;
        }
        Debug.LogWarning("[ASL] ASLSigningCharacterManager not ready after 60 s — " +
                         "run Tools/ASL/Create Signing Avatar Prefab and restart Play.");
    }

    void OnDestroy() { MessageManager.OnMessageReceived -= OnMessage; }

    // ── Toggle ────────────────────────────────────────────────────────────────
    public void SetEnabled(bool on)
    {
        _enabled = on;
        PlayerPrefs.SetInt(PREF, on ? 1 : 0);
        if (_root) _root.SetActive(on);
        if (on)
        {
            if (_header)  _header.text  = "ASL";
            if (_caption) _caption.text = "Sign language on";
            SetHandRest();
            Debug.Log("[ASL] Sign-language bubble ON.");
        }
        else
        {
            Debug.Log("[ASL] Sign-language bubble OFF.");
            _queue.Clear();
            if (_player != null) { StopCoroutine(_player); _player = null; }
        }
    }

    public void Toggle() => SetEnabled(!_enabled);

    // ── Message → fingerspelling ──────────────────────────────────────────────
    void OnMessage(string text, string sender, Color color)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(text)) return;
        _queue.Enqueue((text, sender, color));
        if (_player == null) _player = StartCoroutine(PlayQueue());
    }

    IEnumerator PlayQueue()
    {
        // v2 pipeline: hand each sentence to the ASLSignPlayer for word-level
        // signing + fingerspelling fallback. Caption text is shown immediately
        // for accessibility. Player drives the on-screen avatar through the
        // character manager (its RT feeds _handImg).
        while (_queue.Count > 0)
        {
            var (text, sender, color) = _queue.Dequeue();
            if (_header)  { _header.text = sender; _header.color = color; }
            if (_caption)   _caption.text = text;

            var cm = ASLSigningCharacterManager.Instance;
            Debug.Log($"[ASL-Bubble] Processing \"{text}\" — cm={(cm!=null?"OK":"NULL")}, IsReady={cm?.IsReady}, Player={(cm?.Player!=null?"OK":"NULL")} → path={(cm != null && cm.IsReady && cm.Player != null ? "PLAYER" : "LEGACY")}");
            if (cm != null && cm.IsReady && cm.Player != null)
            {
                cm.Player.StartSentence(text);
                // Hold this sentence's caption roughly the time it takes to sign it.
                // Player has its own queue-cap as a backstop against overflow.
                yield return new WaitForSeconds(Mathf.Min(6f, 0.35f * text.Length));
            }
            else
            {
                // Legacy fallback if character manager isn't ready yet
                foreach (char ch in text.ToUpperInvariant())
                {
                    if (ch >= 'A' && ch <= 'Z')
                    {
                        SetHandLetter(ch);
                        yield return new WaitForSeconds(LETTER_INTERVAL);
                    }
                    else
                    {
                        SetHandRest();
                        yield return new WaitForSeconds(WORD_GAP);
                    }
                }
                SetHandRest();
                yield return new WaitForSeconds(0.4f);
            }
        }
        _player = null;
    }

    // ── Hand display helpers ───────────────────────────────────────────────────

    /// <summary>Show ASL shape for letter ch: live RT if ready, sprite fallback otherwise.</summary>
    void SetHandLetter(char ch)
    {
        var s = ASLSigningCharacterManager.Instance;
        if (s != null && s.IsReady)
        {
            s.SetLetter(ch);
            if (_handImg != null && _handImg.texture != s.RT)
                _handImg.texture = s.RT;
        }
        else if (_handImg)
            _handImg.texture = _hand.TryGetValue(ch, out var t) ? t : _rest;
    }

    /// <summary>Return hand to rest: live RT if ready, sprite fallback otherwise.</summary>
    void SetHandRest()
    {
        var s = ASLSigningCharacterManager.Instance;
        if (s != null && s.IsReady) s.SetRest();
        else if (_handImg) _handImg.texture = _rest;
    }

    // ── View follow ───────────────────────────────────────────────────────────
    void LateUpdate()
    {
        if (_root == null) return;
        if (_root.transform.parent == null) AttachToCamera(); // re-attach on scene reload
    }

    Camera FindCam()
    {
        if (Camera.main != null) return Camera.main;
        var all = Camera.allCameras;
        if (all != null && all.Length > 0) return all[0];
        return null;
    }

    // ── Build ─────────────────────────────────────────────────────────────────
    void LoadSprites()
    {
        for (char ch = 'A'; ch <= 'Z'; ch++)
        {
            var t = Resources.Load<Texture2D>($"ASL/{ch}");
            if (t != null) _hand[ch] = t;
        }
        _rest = Resources.Load<Texture2D>("ASL/_rest");
    }

    void BuildBubble()
    {
        _root = new GameObject("SignLanguageBubble");
        _root.layer = 5;
        // NOT DontDestroyOnLoad — must stay in active scene so URP renders it.
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // Required for WorldSpace + TMP (matches RadialMenu flag=25).
        canvas.additionalShaderChannels =
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.Tangent   |
            AdditionalCanvasShaderChannels.Normal;

        var rt = (RectTransform)_root.transform;
        rt.sizeDelta = new Vector2(440, 600);           // portrait panel (slightly taller for RT)
        _root.transform.localScale    = Vector3.one * 0.00085f;
        _root.transform.localPosition = new Vector3(0.20f, -0.15f, 0.9f);
        _root.transform.localRotation = Quaternion.identity;

        // 1 ── dark teal background
        var bg = NewRect("BG", rt);
        bg.gameObject.AddComponent<Image>().color = new Color(0.024f, 0.106f, 0.133f, 0.95f);
        Stretch(bg);

        // 2 ── teal accent strip at top
        var strip = NewRect("Accent", rt);
        strip.gameObject.AddComponent<Image>().color = new Color(0.176f, 0.831f, 0.749f, 1f);
        strip.anchorMin = new Vector2(0, 1); strip.anchorMax = new Vector2(1, 1);
        strip.pivot     = new Vector2(0.5f, 1);
        strip.sizeDelta = new Vector2(0, 8); strip.anchoredPosition = Vector2.zero;

        // 3 ── speaker name header
        _header = NewText("Header", rt, 36, FontStyles.Bold,
            TextAlignmentOptions.Center, new Color(0.84f, 0.98f, 0.96f));
        Place(_header.rectTransform, new Vector2(400, 52), new Vector2(0, 266));

        // 4 ── live signing character (RenderTexture fed by ASLSigningCharacterManager)
        //       Shows the real Mixamo avatar forming each ASL letter with their right hand.
        //       Falls back to a baked sprite until the character manager is ready.
        var charView = NewRect("CharacterView", rt);
        _handImg = charView.gameObject.AddComponent<RawImage>();
        _handImg.texture = _rest;           // sprite shown until WireRT() wires the RT
        Place(charView, new Vector2(400, 440), new Vector2(0, 10));

        // 5 ── transcription caption below the character
        _caption = NewText("Caption", rt, 22, FontStyles.Normal,
            TextAlignmentOptions.Center, new Color(0.76f, 0.95f, 0.92f));
        Place(_caption.rectTransform, new Vector2(400, 68), new Vector2(0, -266));

        // 6 ── "ASL approx." disclaimer (bottom-left, small, semi-transparent)
        //      Honesty — this is an AI-assisted interpreter approximation, not
        //      certified ASL. Visible but unobtrusive.
        var disc = NewText("Disclaimer", rt, 14, FontStyles.Italic,
            TextAlignmentOptions.BottomLeft, new Color(0.84f, 0.98f, 0.96f, 0.45f));
        disc.text = "ASL approx.";
        Place(disc.rectTransform, new Vector2(180, 24), new Vector2(-118, -288));

        SetLayerRecursively(_root, 5);
        _root.SetActive(false);
    }

    // ── UI Helpers ────────────────────────────────────────────────────────────
    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
    }

    static TMP_Text NewText(string name, Transform parent, float size, FontStyles style,
                            TextAlignmentOptions align, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.fontSize = size; t.fontStyle = style; t.alignment = align; t.color = col; t.text = "";
        t.enableWordWrapping = true; t.raycastTarget = false;
        return t;
    }
}
