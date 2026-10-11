using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WaterVision.ASL;

/// <summary>
/// Word-level ASL playback engine. Owned by ASLSigningCharacterManager but spawned
/// as a separate component on the same GameObject. Pipeline:
///
///   StartSentence(text)
///     → strip punctuation, split to words
///     → drop stop-words ("the","a","and",...)
///     → for each remaining word (capped at PER_SENTENCE_CAP):
///         · TryResolve → known sign → enqueue PlaySign step
///         · else                    → enqueue Fingerspell step
///     · between every word: enqueue ReadyPosition transition (0.15 s)
///     → coroutine drains the queue, lerping target Transforms + finger curls
///       with SmoothStep timing (accelerate into apex, decelerate out).
///
/// Sentence queue cap = 3 (drops oldest to bias toward most recent context).
/// </summary>
public class ASLSignPlayer : MonoBehaviour
{
    // ── Tuning ───────────────────────────────────────────────────────────────
    const float READY_TRANSITION  = 0.15f;
    const float FINGERSPELL_PER_LETTER = 0.45f;
    const int   PER_SENTENCE_CAP  = 6;
    const int   QUEUE_CAP         = 3;

    // ── State ────────────────────────────────────────────────────────────────
    SignCatalog _catalog;
    ASLSigningCharacterManager _char;

    readonly Queue<string> _sentenceQueue = new();
    Coroutine _runner;

    /// <summary>True while a sign is actively being played (Player driving targets).</summary>
    public bool IsPlaying => _runner != null;

    void Awake()
    {
        _char    = GetComponent<ASLSigningCharacterManager>();
        _catalog = Resources.Load<SignCatalog>("ASL/SignCatalog");
        Debug.Log($"[ASL-Player] Awake — char={(_char!=null?"OK":"NULL")}, catalog={(_catalog!=null?$"OK ({_catalog.SignCount} signs)":"NULL")}");
        if (_catalog == null)
            Debug.LogWarning("[ASL-Player] SignCatalog not found at Resources/ASL/SignCatalog. " +
                             "Run Tools/ASL/Seed Sign Catalog first. Falling back to fingerspelling only.");
        else
            _catalog.RebuildIndices();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Enqueue a sentence for sign-language playback.</summary>
    public void StartSentence(string text)
    {
        Debug.Log($"[ASL-Player] StartSentence(\"{text}\") — char={(_char!=null?"OK":"NULL")}, IsReady={_char?.IsReady}, runner={(_runner!=null?"running":"idle")}");
        if (string.IsNullOrWhiteSpace(text)) return;
        if (_char == null || !_char.IsReady) return;

        // Bias toward most recent: when full, drop oldest
        if (_sentenceQueue.Count >= QUEUE_CAP)
        {
            var dropped = _sentenceQueue.Dequeue();
            Debug.Log($"[ASL] Dropping stale sentence (queue full): \"{dropped}\"");
        }
        _sentenceQueue.Enqueue(text);

        if (_runner == null) _runner = StartCoroutine(RunQueue());
    }

    /// <summary>Cancel everything and return to ready position.</summary>
    public void Stop()
    {
        if (_runner != null) { StopCoroutine(_runner); _runner = null; }
        _sentenceQueue.Clear();
        if (_char != null) _char.SetRest();
    }

    // ── Main runner ──────────────────────────────────────────────────────────
    IEnumerator RunQueue()
    {
        while (_sentenceQueue.Count > 0)
        {
            string sentence = _sentenceQueue.Dequeue();
            yield return RunSentence(sentence);
        }
        // Return to neutral ready position after the queue drains
        if (_char != null) _char.SetRest();
        _runner = null;
    }

    IEnumerator RunSentence(string sentence)
    {
        Debug.Log($"[ASL-Player] RunSentence(\"{sentence}\") starting");
        // Tokenize + clean
        var tokens = sentence.Split(new[] { ' ', '\t', '\n', '\r' },
            System.StringSplitOptions.RemoveEmptyEntries);
        Debug.Log($"[ASL-Player] Tokens: [{string.Join(",", tokens)}] (count={tokens.Length})");

        int signedCount = 0;
        bool anySigned  = false;

        foreach (var rawTok in tokens)
        {
            string clean = SignCatalog.Clean(rawTok);
            if (string.IsNullOrEmpty(clean)) continue;
            if (_catalog != null && _catalog.IsStopWord(clean)) { Debug.Log($"[ASL-Player] '{clean}' = stopword, skipped"); continue; }
            if (signedCount >= PER_SENTENCE_CAP)
            {
                Debug.Log($"[ASL] Per-sentence cap ({PER_SENTENCE_CAP}) reached; " +
                          $"remaining words skipped for: \"{sentence}\"");
                break;
            }

            anySigned = true;
            signedCount++;

            WaterVision.ASL.SignDefinition def = default;
            bool found = _catalog != null && _catalog.TryResolve(clean, out def);
            Debug.Log($"[ASL-Player] '{clean}' → {(found ? $"PlaySign({def.word}, {(def.frames != null ? def.frames.Length : 0)} frames)" : "Fingerspell")}");
            if (found)
            {
                yield return PlaySign(def);
            }
            else
            {
                yield return Fingerspell(clean);
            }

            // Brief return-to-ready transition between words
            yield return LerpToReady(READY_TRANSITION);
        }

        Debug.Log($"[ASL-Player] RunSentence done. signed={signedCount}, anySigned={anySigned}");
        if (!anySigned)
            Debug.Log($"[ASL] All stop-words / no signable content in: \"{sentence}\"");
    }

    // ── Single sign playback ────────────────────────────────────────────────
    IEnumerator PlaySign(SignDefinition def)
    {
        if (def.frames == null || def.frames.Length == 0) yield break;

        float speed = def.playbackSpeed > 0.05f ? def.playbackSpeed : 1f;
        Debug.Log($"[ASL-Player] PlaySign({def.word}) — frames={def.frames.Length}, speed={speed}, RightHandTarget={(_char.RightHandTarget != null ? "OK" : "NULL")}");

        // Wave 2: drive the facial expression for this sign (smile/mouth/head/gaze).
        if (_char != null) _char.SetFaceEmotion(def.emotion, 1f);

        // Capture starting pose (current target transforms + curls) as frame -1
        var startR = _char.RightHandTarget != null ? _char.RightHandTarget.localPosition : ASLSigningCharacterManager.READY_RIGHT_POS;
        var startL = _char.LeftHandTarget  != null ? _char.LeftHandTarget.localPosition  : ASLSigningCharacterManager.READY_LEFT_POS;
        var startRrot = _char.RightHandTarget != null ? _char.RightHandTarget.localRotation : Quaternion.identity;
        var startLrot = _char.LeftHandTarget  != null ? _char.LeftHandTarget.localRotation  : Quaternion.identity;

        // Wave 2.7: resolve each keyframe's effective wrist Euler ONCE up front,
        // honouring the semantic PalmDir tag (Forward/Inward/SideRight/Up/...) via
        // the live-tunable table in Settings. PalmDir.Default falls back to the
        // legacy rightHandLocalEuler/leftHandLocalEuler authored on the keyframe.
        // The same table is applied to both hands; existing `leftHandRotationOffset`
        // on the IK already handles any per-hand rig-axis mirroring.
        var settings = _char != null ? _char.Settings : null;
        Vector3 EffR(int idx) =>
            (settings != null && def.frames[idx].palmDirRight != PalmDir.Default)
                ? settings.GetPalmEuler(def.frames[idx].palmDirRight)
                : def.frames[idx].rightHandLocalEuler;
        Vector3 EffL(int idx) =>
            (settings != null && def.frames[idx].palmDirLeft != PalmDir.Default)
                ? settings.GetPalmEuler(def.frames[idx].palmDirLeft)
                : def.frames[idx].leftHandLocalEuler;

        for (int i = 0; i < def.frames.Length; i++)
        {
            var prevR = (i == 0) ? startR : def.frames[i - 1].rightHandLocalPos;
            var prevL = (i == 0) ? startL : def.frames[i - 1].leftHandLocalPos;
            var prevRrot = (i == 0) ? startRrot : Quaternion.Euler(EffR(i - 1));
            var prevLrot = (i == 0) ? startLrot : Quaternion.Euler(EffL(i - 1));
            var prevRcurls = (i == 0)
                ? new[] { 0f, 0f, 0f, 0f, 0f }
                : new[] { def.frames[i-1].r_idx, def.frames[i-1].r_mid, def.frames[i-1].r_ring, def.frames[i-1].r_pinky, def.frames[i-1].r_thumb };
            var prevLcurls = (i == 0)
                ? new[] { 0f, 0f, 0f, 0f, 0f }
                : new[] { def.frames[i-1].l_idx, def.frames[i-1].l_mid, def.frames[i-1].l_ring, def.frames[i-1].l_pinky, def.frames[i-1].l_thumb };

            var f = def.frames[i];
            float lerpDur = Mathf.Max(0.04f, f.transitionSeconds) / speed;
            float holdDur = Mathf.Max(0f,    f.holdSeconds)       / speed;
            Debug.Log($"[ASL-Player]  frame {i}: prevR={prevR:F3} → targetR={f.rightHandLocalPos:F3}, lerpDur={lerpDur}, holdDur={holdDur}");

            // Lerp from prev → this frame
            float t = 0f;
            bool loggedFirstWrite = false;
            while (t < lerpDur)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / lerpDur));

                if (_char.RightHandTarget != null)
                {
                    var newPos = Vector3.LerpUnclamped(prevR, f.rightHandLocalPos, u);
                    _char.RightHandTarget.localPosition = newPos;
                    _char.RightHandTarget.localRotation = Quaternion.Slerp(prevRrot, Quaternion.Euler(EffR(i)), u);
                    if (!loggedFirstWrite)
                    {
                        loggedFirstWrite = true;
                        Debug.Log($"[ASL-Player]   first write: wrote {newPos:F3} → readback={_char.RightHandTarget.localPosition:F3}");
                    }
                }
                if (_char.LeftHandTarget != null)
                {
                    _char.LeftHandTarget.localPosition  = Vector3.LerpUnclamped(prevL, f.leftHandLocalPos, u);
                    _char.LeftHandTarget.localRotation  = Quaternion.Slerp(prevLrot, Quaternion.Euler(EffL(i)), u);
                }

                _char.SetFingerCurls(true,
                    Mathf.Lerp(prevRcurls[0], f.r_idx,   u),
                    Mathf.Lerp(prevRcurls[1], f.r_mid,   u),
                    Mathf.Lerp(prevRcurls[2], f.r_ring,  u),
                    Mathf.Lerp(prevRcurls[3], f.r_pinky, u),
                    Mathf.Lerp(prevRcurls[4], f.r_thumb, u));
                _char.SetFingerCurls(false,
                    Mathf.Lerp(prevLcurls[0], f.l_idx,   u),
                    Mathf.Lerp(prevLcurls[1], f.l_mid,   u),
                    Mathf.Lerp(prevLcurls[2], f.l_ring,  u),
                    Mathf.Lerp(prevLcurls[3], f.l_pinky, u),
                    Mathf.Lerp(prevLcurls[4], f.l_thumb, u));
                yield return null;
            }

            // Hold at this keyframe
            if (holdDur > 0f) yield return new WaitForSeconds(holdDur);
        }
    }

    // ── Fingerspelling fallback ─────────────────────────────────────────────
    IEnumerator Fingerspell(string word)
    {
        if (string.IsNullOrEmpty(word)) yield break;

        // Position the right hand at a stable "fingerspelling rest" — chest height,
        // slightly outward, so letters are read in a consistent location.
        const float FINGERSPELL_TRANSITION = 0.20f;
        var spellPos = new Vector3(0.28f, 1.35f, 0.40f);
        var spellEul = new Vector3(0f, -20f, 0f);

        // Lerp to spelling position once at the start
        yield return LerpRightTo(spellPos, spellEul, FINGERSPELL_TRANSITION);

        foreach (char ch in word.ToUpperInvariant())
        {
            if (ch >= 'A' && ch <= 'Z')
            {
                _char.SetLetter(ch);
                yield return new WaitForSeconds(FINGERSPELL_PER_LETTER);
            }
        }
    }

    // ── Transitions ─────────────────────────────────────────────────────────
    IEnumerator LerpToReady(float dur)
    {
        // Wave 2: relax the face back to neutral as the hands return to ready.
        if (_char != null) _char.ClearFace();

        var endR = ASLSigningCharacterManager.READY_RIGHT_POS;
        var endL = ASLSigningCharacterManager.READY_LEFT_POS;

        var startR = _char.RightHandTarget != null ? _char.RightHandTarget.localPosition : endR;
        var startL = _char.LeftHandTarget  != null ? _char.LeftHandTarget.localPosition  : endL;
        var startRrot = _char.RightHandTarget != null ? _char.RightHandTarget.localRotation : Quaternion.identity;
        var startLrot = _char.LeftHandTarget  != null ? _char.LeftHandTarget.localRotation  : Quaternion.identity;
        // Return wrist to the RELAXED rest orientation (palms inward), not identity —
        // so idle hands don't sit palm-out. The manager owns this static.
        var restRrot = Quaternion.Euler(ASLSigningCharacterManager.READY_WRIST_EULER_R);
        var restLrot = Quaternion.Euler(ASLSigningCharacterManager.READY_WRIST_EULER_L);

        // Ready position has fingers softly curled
        const float READY_CURL = 0.18f;
        const float READY_THUMB = 0.10f;

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));

            if (_char.RightHandTarget != null)
            {
                _char.RightHandTarget.localPosition = Vector3.LerpUnclamped(startR, endR, u);
                _char.RightHandTarget.localRotation = Quaternion.Slerp(startRrot, restRrot, u);
            }
            if (_char.LeftHandTarget != null)
            {
                _char.LeftHandTarget.localPosition = Vector3.LerpUnclamped(startL, endL, u);
                _char.LeftHandTarget.localRotation = Quaternion.Slerp(startLrot, restLrot, u);
            }
            // Hands relax to the soft-curl ready pose
            _char.SetFingerCurls(true,  READY_CURL, READY_CURL, READY_CURL, READY_CURL, READY_THUMB);
            _char.SetFingerCurls(false, READY_CURL, READY_CURL, READY_CURL, READY_CURL, READY_THUMB);

            yield return null;
        }
    }

    IEnumerator LerpRightTo(Vector3 pos, Vector3 eul, float dur)
    {
        if (_char.RightHandTarget == null) yield break;

        var startP = _char.RightHandTarget.localPosition;
        var startR = _char.RightHandTarget.localRotation;
        var endR = Quaternion.Euler(eul);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            _char.RightHandTarget.localPosition = Vector3.LerpUnclamped(startP, pos, u);
            _char.RightHandTarget.localRotation = Quaternion.Slerp(startR, endR, u);
            yield return null;
        }
    }
}
