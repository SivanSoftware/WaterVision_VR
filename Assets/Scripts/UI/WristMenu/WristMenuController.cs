using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

/// <summary>
/// Local-only "wristwatch" menu controller.
///
/// While the player is in hand-tracking mode, a small button is parented to the LEFT wrist.
/// Poking it with the right index finger toggles a copy of <see cref="menuPrefabToClone"/>
/// (the existing <c>Player_Menu_UI.prefab</c>) anchored above the same wrist. Switching back
/// to motion controllers tears everything down.
///
/// This script lives on the networked player root, but it disables itself for non-owners
/// in <see cref="OnNetworkSpawn"/> so only the wearer sees / interacts with their own watch.
///
/// Pairs with — but does not modify — <c>MenuController</c>, <c>PlayerOptions</c>, and
/// <c>Player_Menu_UI.prefab</c>; both the floating menu and the cloned wrist menu hit the
/// same <see cref="PlayerOptions"/> singletons, so state stays consistent.
/// </summary>
public class WristMenuController : NetworkBehaviour
{
    [Header("Prefabs (assign on the player rig)")]
    [Tooltip("Small world-space canvas with a Button + XRPokeFilter. ~3 cm circular watch face.")]
    [SerializeField] private GameObject wristButtonPrefab;

    [Tooltip("The existing VR_MainMenu.prefab (Assets/Menu/) — cloned into a wrist-attached instance " +
             "on toggle. The plan originally named Player_Menu_UI.prefab, but that file has no Canvas " +
             "at its root and isn't referenced anywhere in the project; VR_MainMenu is the live " +
             "floating menu (see MenuController.cs).")]
    [SerializeField] private GameObject menuPrefabToClone;

    [Header("Anchor override (optional)")]
    [Tooltip("If set, used directly as the left-wrist anchor. If null, the controller resolves the " +
             "wrist each time hand-tracking activates — first via XRHandSkeletonDriver, then via the " +
             "Mixamo bone 'mixamorig7:LeftHand' under this transform.")]
    [SerializeField] private Transform leftWristOverride;

    [Header("Watch button placement on the wrist (palm/+Y side)")]
    [Tooltip("Local position offset from the wrist transform. POSITIVE Y = the PALM side in this " +
             "rig (where the user looks when they turn their palm toward their face; the watch shows " +
             "on the palm). +0.05 m sits the canvas on the palm side of the wrist within finger reach.")]
    [SerializeField] private Vector3 watchLocalPosition = new Vector3(0f, 0.05f, -0.04f);
    [Tooltip("Local Euler for the world-space CANVAS. (90, 0, 180) orients the canvas face toward " +
             "+wrist.up (the palm side). If the icon reads mirrored/upside-down in-headset, " +
             "try (270,0,180) or (90,0,0).")]
    [SerializeField] private Vector3 watchLocalEuler = new Vector3(90f, 0f, 180f);

    // At runtime the watch + menu are placed/oriented from the true palm normal (from the hand
    // metacarpals), NOT the wrist joint's tilted axes — so they sit just off the palm facing you.
    // The watchLocalPosition/Euler offsets above become a fallback used only when the knuckle joints
    // aren't available.
    [Header("Palm-relative placement (geometric — OVERRIDES the wrist-local offsets above)")]
    [Tooltip("Distance (m) the watch sits OFF the palm surface, along the palm normal toward you. " +
             "~0.03 floats it just above the palm like a watch face.")]
    [SerializeField] private float m_PalmSurfaceOffset = 0.03f;
    [Tooltip("Distance (m) along the hand toward the fingers from the wrist. 0 = at the wrist.")]
    [SerializeField] private float m_PalmFingerOffset = 0f;
    [Tooltip("Flip if the watch face / menu point AWAY from you instead of toward you.")]
    [SerializeField] private bool m_FlipWatchFacing = false;
    [Tooltip("Flip if the gear icon renders upside-down.")]
    [SerializeField] private bool m_FlipWatchUp = false;
    [Tooltip("Invert the computed palm-out normal. Its sign is anchored to the wrist's tilted up-axis, " +
             "which on this rig points to the BACK of the hand — that put the button on the back (like a " +
             "wristwatch) and showed it when the back of the hand faced you. ON = button sits on the PALM " +
             "side (under the wrist) and shows when the palm faces your face. Tunable live in Play.")]
    [SerializeField] private bool m_InvertPalmNormal = true;

    [Header("Menu placement (wrist-anchored)")]
    [Tooltip("Local position offset from the wrist for the cloned menu. Set to match watchLocalPosition " +
             "so the menu opens AT the button position and radially expands AROUND it (button becomes " +
             "the center of the menu). The menu parents to the wrist so it follows the hand naturally.")]
    [SerializeField] private Vector3 menuLocalOffset = new Vector3(0f, 0.05f, -0.04f);
    [Tooltip("Local Euler rotation for the menu, in the wrist's local space. Default mirrors the " +
             "watch's rotation so the menu face points the same direction as the watch face " +
             "(visible when palm rotates up).")]
    [SerializeField] private Vector3 menuLocalEuler = new Vector3(90f, 0f, 180f);
    [Tooltip("Multiplier applied to the cloned menu's existing scale. RadialMenu's native size at " +
             "scale 0.0015 × 800-pixel canvas = ~1.2 m wide. 0.333 → ~40 cm wide; 0.25 → ~30 cm; " +
             "0.5 → ~60 cm. Tune to taste.")]
    [SerializeField] private float menuScaleMultiplier = 0.2f;

    [Header("Menu radial expand animation (Stark-HUD style)")]
    [Tooltip("Seconds to expand from scale 0 to full menu size on open. Uses ease-out-back " +
             "(overshoot ~10% past target, then settles back) so the menu 'snaps' into place.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float menuOpenDuration = 0.30f;
    [Tooltip("Seconds to collapse on close. Shorter than open for a snappy retract feel; " +
             "uses ease-in cubic (gentle start, fast finish) and reverse-spins back.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float menuCloseDuration = 0.20f;
    [Tooltip("Degrees the menu rotates around its face normal during the open animation. The " +
             "menu starts rotated by this much and spins to the resting orientation as it " +
             "expands — gives an Iron Man / Stark HUD 'unfolds into place' feel. 0 disables " +
             "the spin; 90 is a quarter-turn (recommended); 180 is more dramatic.")]
    [Range(0f, 360f)]
    [SerializeField] private float menuOpenSpinDegrees = 90f;

    [Header("Watch visibility (palm-facing gate)")]
    [Tooltip("Show the watch only when the palm faces roughly toward the camera. Dot product " +
             "of palm-out direction (-wrist.up in world) with the wrist→camera direction must " +
             "exceed this to (re)show the watch. Higher = stricter alignment required.")]
    [Range(-1f, 1f)]
    [SerializeField] private float showFacingThreshold = 0.6f;
    [Tooltip("Hide the watch when the dot product drops below this. Lower than showFacingThreshold " +
             "by ~0.2 to give comfortable hysteresis and prevent flicker at the boundary.")]
    [Range(-1f, 1f)]
    [SerializeField] private float hideFacingThreshold = 0.4f;
    [Tooltip("Invert ONLY the visibility gate, independently of m_InvertPalmNormal (which controls the " +
             "button's SIDE). The placement was correct but the show/hide triggered on the BACK of the " +
             "hand; enable this so the watch appears when the PALM faces your face instead. Live-tunable.")]
    [SerializeField] private bool m_InvertVisibilityGate = true;

    [Header("Debug")]
    [Tooltip("Enabled by default while we're verifying the feature. Flip off once stable.")]
    [SerializeField] private bool debugLog = true;

    private XRInputModalityManager m_Modality;
    private Transform m_LeftWrist;
    // Knuckle metacarpals used to compute a robust GEOMETRIC palm normal (see TryGetPalmFrame).
    // Relying on the wrist joint's own axis (wrist.up) was wrong: that axis is tilted toward the
    // forearm, so the palm had to be over-rotated to show the watch AND a downward wrist-flex
    // (hand curl) falsely swung the axis toward the camera. The palm-plane normal from the
    // metacarpals tracks the actual palm orientation and ignores finger curl.
    private Transform m_LeftIndexMeta;
    private Transform m_LeftLittleMeta;
    private GameObject m_WatchInstance;
    private GameObject m_MenuInstance;
    // While the close animation runs, the menu is detached from m_MenuInstance (so a second
    // poke re-opens fresh) and parked here for cleanup. Lets us cancel a mid-close on rapid
    // re-open without leaking half-collapsed clones.
    private GameObject m_ClosingMenu;
    private Coroutine m_MenuAnimationCoroutine;
    // Tracks the watch's current visible state so we apply hysteresis correctly.
    // True when the GameObject is SetActive(true); false when hidden.
    private bool m_WatchCurrentlyShown = true;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (debugLog)
            Debug.Log($"[{nameof(WristMenuController)}] OnNetworkSpawn: IsOwner={IsOwner}, IsClient={IsClient}, IsServer={IsServer}.", this);

        // Local-only: only the owner spawns / interacts with their own wrist menu.
        // Disabling the component (rather than this.gameObject) keeps the rest of the
        // player rig intact for remote viewers.
        if (!IsOwner)
        {
            enabled = false;
            return;
        }
    }

    void Awake()
    {
        m_Modality = FindFirstObjectByType<XRInputModalityManager>();
        if (m_Modality == null)
        {
            Debug.LogWarning($"[{nameof(WristMenuController)}] No XRInputModalityManager in scene. " +
                             $"Watch will never appear — hand-tracking events can't be detected.", this);
        }
    }

    void OnEnable()
    {
        if (debugLog)
            Debug.Log($"[{nameof(WristMenuController)}] OnEnable. m_Modality={(m_Modality != null ? "found" : "NULL")}.", this);

        if (m_Modality == null) return;

        m_Modality.trackedHandModeStarted.AddListener(OnTrackedHandStarted);
        m_Modality.motionControllerModeStarted.AddListener(OnMotionControllerStarted);

        // Modality events only fire on TRANSITIONS, not for the mode already active when
        // this component enables. Sync immediately so we don't have to wait for the user
        // to switch modalities before the watch shows up.
        var current = XRInputModalityManager.currentInputMode.Value;
        if (debugLog)
            Debug.Log($"[{nameof(WristMenuController)}] Initial-sync modality = {current}.", this);
        if (current == XRInputModalityManager.InputMode.TrackedHand)
            OnTrackedHandStarted();
        // For MotionController / None we deliberately don't call OnMotionControllerStarted —
        // there's nothing to tear down on the first frame and we avoid a no-op log.
    }

    void OnDisable()
    {
        if (m_Modality != null)
        {
            m_Modality.trackedHandModeStarted.RemoveListener(OnTrackedHandStarted);
            m_Modality.motionControllerModeStarted.RemoveListener(OnMotionControllerStarted);
        }
        TeardownAll();
    }

    public override void OnNetworkDespawn()
    {
        TeardownAll();
        base.OnNetworkDespawn();
    }

    // ── Modality transitions ─────────────────────────────────────────────

    private void OnTrackedHandStarted()
    {
        m_LeftWrist = ResolveLeftWrist();
        if (m_LeftWrist == null)
        {
            if (debugLog)
                Debug.LogWarning($"[{nameof(WristMenuController)}] Hand tracking started but the left wrist " +
                                 $"couldn't be resolved. Will retry on the next modality event.", this);
            return;
        }

        ResolvePalmNormalJoints();
        SpawnWatchButton();

        if (debugLog)
            Debug.Log($"[{nameof(WristMenuController)}] Hand-tracking active — watch attached to '{m_LeftWrist.name}'.", this);
    }

    private void OnMotionControllerStarted()
    {
        TeardownAll();
        if (debugLog)
            Debug.Log($"[{nameof(WristMenuController)}] Controllers active — watch + menu torn down.", this);
    }

    // ── Spawning ─────────────────────────────────────────────────────────

    private void SpawnWatchButton()
    {
        if (wristButtonPrefab == null)
        {
            Debug.LogError($"[{nameof(WristMenuController)}] wristButtonPrefab is unassigned.", this);
            return;
        }
        if (m_WatchInstance != null)
        {
            if (debugLog) Debug.Log($"[{nameof(WristMenuController)}] SpawnWatchButton called but watch already exists. Skipping.", this);
            return;
        }

        m_WatchInstance = Instantiate(wristButtonPrefab, m_LeftWrist);
        m_WatchInstance.transform.localPosition = watchLocalPosition;
        m_WatchInstance.transform.localRotation = Quaternion.Euler(watchLocalEuler);
        m_WatchCurrentlyShown = true; // LateUpdate will hide on first frame if palm isn't facing

        if (debugLog)
            Debug.Log($"[{nameof(WristMenuController)}] Watch SPAWNED under '{m_LeftWrist.name}' " +
                      $"at local pos {watchLocalPosition}, world pos {m_WatchInstance.transform.position}. " +
                      $"Wrist world pos: {m_LeftWrist.position}.", this);

        // Wire the toggle to either a 3D pokable interactable (new pass — physical button with
        // XRSimpleInteractable + XRPokeFilter) OR a legacy UI Button (old Canvas-based prefab).
        // Tries the 3D path first; falls back to UI Button. Each instance has only one of these.
        var interactable = m_WatchInstance.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>(includeInactive: true);
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(_ => ToggleWristMenu());
            if (debugLog) Debug.Log($"[{nameof(WristMenuController)}] Wired XRSimpleInteractable.selectEntered → ToggleWristMenu (3D pokable path).", this);
        }
        else
        {
            var btn = m_WatchInstance.GetComponentInChildren<UnityEngine.UI.Button>(includeInactive: true);
            if (btn != null)
            {
                btn.onClick.AddListener(ToggleWristMenu);
                if (debugLog) Debug.Log($"[{nameof(WristMenuController)}] Wired Button.onClick → ToggleWristMenu (legacy UI path).", this);
            }
            else
            {
                Debug.LogWarning($"[{nameof(WristMenuController)}] WristMenuButton prefab has no " +
                                 $"XRSimpleInteractable OR UI Button in its hierarchy. Poke-to-toggle won't work.", this);
            }
        }
    }

    public void ToggleWristMenu()
    {
        // ── Currently open → start radial collapse ────────────────────────
        if (m_MenuInstance != null)
        {
            var closing = m_MenuInstance;
            m_MenuInstance = null;                       // detach so next toggle opens fresh
            if (m_MenuAnimationCoroutine != null) StopCoroutine(m_MenuAnimationCoroutine);
            m_ClosingMenu = closing;
            m_MenuAnimationCoroutine = StartCoroutine(CloseMenuRoutine(closing));
            if (debugLog) Debug.Log($"[{nameof(WristMenuController)}] Wrist menu collapsing.", this);
            return;
        }

        // ── Opening: clean up any lingering close-in-progress ─────────────
        // If the user rapid-double-taps (close → open), the previous close coroutine
        // is still animating its menu down to scale 0. Cancel it and destroy that
        // leftover clone so we don't accumulate ghost menus.
        if (m_ClosingMenu != null)
        {
            if (m_MenuAnimationCoroutine != null) StopCoroutine(m_MenuAnimationCoroutine);
            Destroy(m_ClosingMenu);
            m_ClosingMenu = null;
        }

        if (menuPrefabToClone == null)
        {
            Debug.LogError($"[{nameof(WristMenuController)}] menuPrefabToClone is unassigned.", this);
            return;
        }
        if (m_LeftWrist == null) return; // shouldn't happen — watch couldn't have been pressed

        // Spawn PARENTED to the wrist so the menu tracks the hand. When the user lowers
        // their wrist the menu lowers with it; when they rotate palm-down the menu rotates
        // out of view naturally. menuLocalOffset defaults to watchLocalPosition so the menu
        // appears AT the button's location and radially expands AROUND it.
        m_MenuInstance = Instantiate(menuPrefabToClone, m_LeftWrist);
        m_MenuInstance.transform.localPosition = menuLocalOffset;
        m_MenuInstance.transform.localRotation = Quaternion.Euler(menuLocalEuler);

        // Preserve the source prefab's authored scale (e.g. RadialMenu ships at 0.0015 with an
        // 800-pixel canvas → ~1.2 m wide). We multiply by menuScaleMultiplier to get the FINAL
        // target scale, then start the menu at zero and let OpenMenuRoutine lerp to it.
        Vector3 targetScale = m_MenuInstance.transform.localScale * menuScaleMultiplier;
        m_MenuInstance.transform.localScale = Vector3.zero;
        m_MenuInstance.SetActive(true); // the source prefab may ship inactive

        EnsurePokeCompatibility(m_MenuInstance);

        m_MenuAnimationCoroutine = StartCoroutine(OpenMenuRoutine(m_MenuInstance, targetScale));

        if (debugLog)
            Debug.Log($"[{nameof(WristMenuController)}] Wrist menu opening (radial expand) at local " +
                      $"{menuLocalOffset}, target scale ×{menuScaleMultiplier} over {menuOpenDuration:F2}s.", this);
    }

    /// <summary>
    /// Iron Man / Stark HUD style radial expand:
    /// <list type="bullet">
    /// <item><description><b>Scale</b>: 0 → target with ease-out-back (~10% overshoot, then settles) — the "snap into place" pop.</description></item>
    /// <item><description><b>Rotation</b>: starts pre-rotated by <see cref="menuOpenSpinDegrees"/> around the canvas face normal, slerps to the resting orientation — the menu "unfolds" as it grows.</description></item>
    /// </list>
    /// Scale and rotation use different easing curves on purpose: scale overshoots for impact,
    /// rotation eases out smoothly so the spin doesn't oscillate past target.
    /// </summary>
    private IEnumerator OpenMenuRoutine(GameObject menu, Vector3 targetScale)
    {
        // Target rotation = the resting orientation set in ToggleWristMenu.
        // Start rotation = target spun by menuOpenSpinDegrees around the menu's LOCAL Z
        // (canvas face normal). Right-multiplying by AngleAxis(_, Vector3.forward) applies
        // the spin in the rotated local frame — i.e. around the face normal, not world Z.
        Quaternion targetRotation = Quaternion.Euler(menuLocalEuler);
        Quaternion startRotation = targetRotation * Quaternion.AngleAxis(menuOpenSpinDegrees, Vector3.forward);

        float t = 0f;
        float duration = Mathf.Max(0.01f, menuOpenDuration);
        while (menu != null && t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);

            // Scale: ease-out-back gives the overshoot-then-settle pop.
            menu.transform.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, EaseOutBack(p));

            // Rotation: ease-out cubic (smooth, no overshoot — spinning past 0 would look glitchy).
            float rotP = 1f - Mathf.Pow(1f - p, 3f);
            menu.transform.localRotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, rotP);

            yield return null;
        }
        if (menu != null)
        {
            menu.transform.localScale = targetScale;
            menu.transform.localRotation = targetRotation;
        }
        m_MenuAnimationCoroutine = null;
    }

    /// <summary>
    /// Reverse animation: scale collapses to zero with ease-in cubic while rotation winds
    /// back to the pre-open spin offset, then the menu is destroyed. Captures the starting
    /// scale/rotation from the menu's current state so cancelling a mid-open closes smoothly
    /// from the partially-expanded pose rather than snapping to full first.
    /// </summary>
    private IEnumerator CloseMenuRoutine(GameObject menu)
    {
        if (menu == null) { m_MenuAnimationCoroutine = null; m_ClosingMenu = null; yield break; }
        Vector3 startScale = menu.transform.localScale;
        Quaternion startRotation = menu.transform.localRotation;
        // End rotation mirrors the open's start rotation — the HUD "folds away" the same
        // way it unfolded.
        Quaternion endRotation = Quaternion.Euler(menuLocalEuler) * Quaternion.AngleAxis(menuOpenSpinDegrees, Vector3.forward);

        float t = 0f;
        float duration = Mathf.Max(0.01f, menuCloseDuration);
        while (menu != null && t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = p * p * p;                     // ease-in cubic (gentle → snap)
            menu.transform.localScale = Vector3.LerpUnclamped(startScale, Vector3.zero, eased);
            menu.transform.localRotation = Quaternion.SlerpUnclamped(startRotation, endRotation, eased);
            yield return null;
        }
        if (menu != null) Destroy(menu);
        if (m_ClosingMenu == menu) m_ClosingMenu = null;
        m_MenuAnimationCoroutine = null;
    }

    /// <summary>
    /// Ease-out-back: t=0 → 0, t=1 → 1, but peaks ~1.10 around t=0.7 before settling back.
    /// Constants are the canonical "easings.net" values for back-out — gives a ~10% overshoot
    /// which reads as "snaps into place" without feeling rubbery.
    /// </summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float t1 = t - 1f;
        return 1f + c3 * t1 * t1 * t1 + c1 * t1 * t1;
    }

    /// <summary>
    /// VR_MainMenu was authored for ray-pointer input. For finger-poke to work, the canvas needs
    /// a <see cref="UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster"/>. Add
    /// one if it isn't already there — idempotent so cloning multiple times stays safe.
    /// </summary>
    private void EnsurePokeCompatibility(GameObject menuRoot)
    {
        var canvases = menuRoot.GetComponentsInChildren<Canvas>(includeInactive: true);
        foreach (var canvas in canvases)
        {
            var existing = canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            if (existing == null)
            {
                canvas.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
                if (debugLog)
                    Debug.Log($"[{nameof(WristMenuController)}] Added TrackedDeviceGraphicRaycaster to '{canvas.gameObject.name}' for poke support.", this);
            }
        }
    }

    private void TeardownAll()
    {
        // Stop any in-flight open/close animation so it doesn't tick on a destroyed transform.
        if (m_MenuAnimationCoroutine != null) { StopCoroutine(m_MenuAnimationCoroutine); m_MenuAnimationCoroutine = null; }
        if (m_ClosingMenu != null)   { Destroy(m_ClosingMenu);   m_ClosingMenu = null; }
        if (m_MenuInstance != null)  { Destroy(m_MenuInstance);  m_MenuInstance = null; }
        if (m_WatchInstance != null) { Destroy(m_WatchInstance); m_WatchInstance = null; }
    }

    // ── Per-frame: gate the watch's visibility on palm-facing-camera ─────
    //
    // The menu instance is NOT updated here — it sits static in world space once placed.
    // We only toggle the watch button, so users see it appear when they "look at their watch"
    // (rotate palm up) and tuck away otherwise.

    void LateUpdate()
    {
        if (m_WatchInstance == null || m_LeftWrist == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        // GEOMETRIC palm frame (from the hand metacarpals), NOT the wrist joint's own axes. wrist.up
        // is tilted toward the forearm — using it for the facing gate caused over-rotation + a
        // downward-curl false-trigger, and using it for PLACEMENT put the button "into the hand" and
        // rotated 180° (facing outward). The palm normal points straight out of the palm and the
        // along-vector points toward the fingers, so both the visibility gate AND the placement are
        // correct regardless of the wrist frame.
        TryGetPalmFrame(out Vector3 palmNormal, out Vector3 palmAlong);
        Vector3 toCam = (cam.transform.position - m_LeftWrist.position).normalized;
        float facing = Vector3.Dot(palmNormal, toCam);
        // Gate sign is INDEPENDENT of placement: the placement block below uses palmNormal/nLocal
        // directly (correct side, untouched), while the watch show/hide + menu auto-close read `facing`.
        // Flipping it here changes only WHEN the watch appears (palm-facing) without moving the button.
        if (m_InvertVisibilityGate) facing = -facing;

        // Place + orient the watch from the palm frame EVERY frame — geometric when the knuckle joints
        // are available, wrist.up fallback otherwise — expressed in the wrist's LOCAL space so the
        // button stays parented to the wrist (and the menu's open animation, which reads menuLocalEuler,
        // lands facing the user). The canvas's visible front is its local -Z, so we aim +Z at
        // -palmNormal to make the front face the user; "up" follows the fingers so the icon stays
        // upright. m_InvertPalmNormal (in TryGetPalmFrame) selects the palm vs back-of-hand side.
        {
            Vector3 nLocal     = m_LeftWrist.InverseTransformDirection(palmNormal).normalized;
            Vector3 alongLocal = m_LeftWrist.InverseTransformDirection(palmAlong).normalized;
            Vector3 faceFwd    = m_FlipWatchFacing ? nLocal : -nLocal;
            Vector3 upDir      = m_FlipWatchUp ? -alongLocal : alongLocal;
            if (faceFwd.sqrMagnitude > 1e-6f && upDir.sqrMagnitude > 1e-6f &&
                Vector3.Cross(faceFwd, upDir).sqrMagnitude > 1e-6f)
            {
                Quaternion rot = Quaternion.LookRotation(faceFwd, upDir);
                Vector3 pos    = nLocal * m_PalmSurfaceOffset + alongLocal * m_PalmFingerOffset;
                m_WatchInstance.transform.localPosition = pos;
                m_WatchInstance.transform.localRotation = rot;
                // Keep the menu placement in sync (it opens at / expands around the button).
                watchLocalPosition = pos; watchLocalEuler = rot.eulerAngles;
                menuLocalOffset    = pos; menuLocalEuler   = rot.eulerAngles;
            }
        }

        if (debugLog && Time.frameCount % 20 == 0)
            Debug.Log($"[{nameof(WristMenuController)}] palm-facing={facing:F2} (show>{showFacingThreshold} / hide<{hideFacingThreshold}) shown={m_WatchCurrentlyShown}", this);

        // Auto-close the open menu when the wrist rotates away from the camera. The opened menu
        // expands AROUND the button and occludes it, so re-poking the button to close it is
        // unreliable (the finger hits the menu canvas, not the button behind it). Rotating the
        // wrist away is the intuitive dismiss for a wrist-anchored menu, and guarantees the user
        // can always close it. Uses the same hide threshold as the watch for consistency.
        if (m_MenuInstance != null && facing < hideFacingThreshold)
        {
            if (debugLog) Debug.Log($"[{nameof(WristMenuController)}] Auto-closing menu (wrist rotated away).", this);
            ToggleWristMenu(); // m_MenuInstance != null → triggers the collapse path
        }

        // Hysteresis: must clearly satisfy show OR hide; in-between → keep current state.
        if (m_WatchCurrentlyShown && facing < hideFacingThreshold)
        {
            m_WatchInstance.SetActive(false);
            m_WatchCurrentlyShown = false;
        }
        else if (!m_WatchCurrentlyShown && facing > showFacingThreshold)
        {
            m_WatchInstance.SetActive(true);
            m_WatchCurrentlyShown = true;
        }
    }

    // ── Wrist resolution ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the left-wrist Transform to anchor the watch to. Priority:
    ///   1. The explicit <see cref="leftWristOverride"/> if assigned.
    ///   2. The live XRHandSkeletonDriver wrist joint under <c>m_Modality.leftHand</c>.
    ///   3. Fallback: the avatar bone <c>mixamorig7:LeftHand</c> under this transform.
    /// </summary>
    private Transform ResolveLeftWrist()
    {
        if (leftWristOverride != null)
        {
            if (debugLog) Debug.Log($"[{nameof(WristMenuController)}] Wrist resolution: override → '{leftWristOverride.name}'.", this);
            return leftWristOverride;
        }

        // (2) Live XR Hands joint — preferred so the watch tracks the actual hand position,
        // not just the avatar bone (which may be IK-driven and slightly lagged).
        if (m_Modality != null && m_Modality.leftHand != null)
        {
            var driver = m_Modality.leftHand.GetComponentInChildren<XRHandSkeletonDriver>(includeInactive: true);
            if (driver != null)
            {
                IList<JointToTransformReference> joints = driver.jointTransformReferences;
                for (int i = 0; i < joints.Count; i++)
                {
                    if (joints[i].xrHandJointID == XRHandJointID.Wrist)
                    {
                        if (debugLog) Debug.Log($"[{nameof(WristMenuController)}] Wrist resolution: XRHandSkeletonDriver → '{joints[i].jointTransform?.name}'.", this);
                        return joints[i].jointTransform;
                    }
                }
                if (debugLog) Debug.LogWarning($"[{nameof(WristMenuController)}] XRHandSkeletonDriver found but no Wrist joint among {joints.Count} entries. Falling back to bone.", this);
            }
            else if (debugLog)
            {
                Debug.LogWarning($"[{nameof(WristMenuController)}] m_Modality.leftHand='{m_Modality.leftHand.name}' has no XRHandSkeletonDriver. Falling back to bone.", this);
            }
        }
        else if (debugLog)
        {
            Debug.LogWarning($"[{nameof(WristMenuController)}] m_Modality null OR m_Modality.leftHand null. Falling back to bone.", this);
        }

        // (3) Avatar-bone fallback. mixamorig7:LeftHand is the Mixamo bone name on this project's
        // avatar (Ch08_nonPBR rig). Returns null if neither path resolves — caller handles that.
        var bone = FindDescendantByName(transform, "mixamorig7:LeftHand");
        if (debugLog)
        {
            Debug.Log($"[{nameof(WristMenuController)}] Wrist resolution: bone fallback → {(bone != null ? $"'{bone.name}'" : "NOT FOUND")}.", this);
        }
        return bone;
    }

    // Resolve the index + little metacarpal joints from the live XR Hands skeleton so we can build
    // a geometric palm normal. Falls back to the proximal joints if a driver doesn't reference the
    // metacarpals. Leaves the fields null if no driver is available (e.g., the bone-only wrist
    // fallback) — TryGetPalmFrame then degrades to wrist.up.
    private void ResolvePalmNormalJoints()
    {
        m_LeftIndexMeta = null;
        m_LeftLittleMeta = null;

        if (m_Modality == null || m_Modality.leftHand == null) return;
        var driver = m_Modality.leftHand.GetComponentInChildren<XRHandSkeletonDriver>(includeInactive: true);
        if (driver == null) return;

        IList<JointToTransformReference> joints = driver.jointTransformReferences;
        for (int i = 0; i < joints.Count; i++)
        {
            var id = joints[i].xrHandJointID;
            if (id == XRHandJointID.IndexMetacarpal && m_LeftIndexMeta == null) m_LeftIndexMeta = joints[i].jointTransform;
            else if (id == XRHandJointID.LittleMetacarpal && m_LeftLittleMeta == null) m_LeftLittleMeta = joints[i].jointTransform;
        }
        // Proximal fallback (metacarpals not always referenced). Proximals move a little with finger
        // curl, but the wrist menu is used with a relaxed hand, so this is acceptable.
        for (int i = 0; i < joints.Count && (m_LeftIndexMeta == null || m_LeftLittleMeta == null); i++)
        {
            var id = joints[i].xrHandJointID;
            if (m_LeftIndexMeta == null && id == XRHandJointID.IndexProximal) m_LeftIndexMeta = joints[i].jointTransform;
            else if (m_LeftLittleMeta == null && id == XRHandJointID.LittleProximal) m_LeftLittleMeta = joints[i].jointTransform;
        }

        if (debugLog)
            Debug.Log($"[{nameof(WristMenuController)}] Palm-normal joints: index={(m_LeftIndexMeta != null ? m_LeftIndexMeta.name : "NULL")}, " +
                      $"little={(m_LeftLittleMeta != null ? m_LeftLittleMeta.name : "NULL")}.", this);
    }

    // Geometric palm frame from the hand metacarpals (world space):
    //   normal — perpendicular to the wrist→knuckles plane, pointing OUT of the palm (toward the
    //            user when the palm faces them). Sign anchored to +wrist.up (the palm-ish side in
    //            this rig) only to choose palm-out vs back-of-hand — NOT used as the axis itself.
    //   along  — from the wrist toward the fingers, in the palm plane.
    // Convention-independent: doesn't trust the wrist joint's tilted axes. Returns false (with
    // wrist.up / wrist.forward fallbacks) when the knuckle joints aren't available.
    private bool TryGetPalmFrame(out Vector3 normal, out Vector3 along)
    {
        normal = m_LeftWrist != null ? m_LeftWrist.up      : Vector3.up;
        along  = m_LeftWrist != null ? m_LeftWrist.forward : Vector3.forward;
        bool geo = false;

        if (m_LeftIndexMeta != null && m_LeftLittleMeta != null && m_LeftWrist != null)
        {
            Vector3 mid    = (m_LeftIndexMeta.position + m_LeftLittleMeta.position) * 0.5f;
            Vector3 a      = mid - m_LeftWrist.position;                          // wrist → knuckles
            Vector3 across = m_LeftLittleMeta.position - m_LeftIndexMeta.position; // index → little
            Vector3 n      = Vector3.Cross(a, across);
            if (n.sqrMagnitude >= 1e-8f && a.sqrMagnitude >= 1e-8f)
            {
                n.Normalize();
                if (Vector3.Dot(n, m_LeftWrist.up) < 0f) n = -n; // orient to the wrist-up side first
                normal = n;
                along  = a.normalized;
                geo = true;
            }
        }

        // One user-tunable inversion applied to BOTH the geometric normal and the wrist.up fallback,
        // so the watch's side AND its palm-facing visibility gate flip together with a single toggle.
        if (m_InvertPalmNormal) normal = -normal;
        return geo;
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root.name == targetName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDescendantByName(root.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }
}
