using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WaterVision;

/// <summary>
/// Spawns a hidden Mixamo signing character at y = −500 and renders it to a
/// RenderTexture for the in-world Sign-Language Bubble. v2 architecture:
///
///   PILLAR 1  ALIVE BODY     — Animator stays ENABLED; the prefab's PlayerAnimator
///                              plays Idle at (MoveX,MoveZ)=(0,0). Body breathes for free.
///   PILLAR 2  NATURAL ARMS   — The prefab's existing RigBuilder + TwoBoneIKConstraints
///                              (RightArmIK, LeftArmIK) solve shoulder→arm→hand for us.
///                              At runtime we create ASL_RightHandTarget / ASL_LeftHandTarget
///                              GameObjects, re-parent the constraints' wrist sockets onto
///                              them, and expose the targets so ASLSignPlayer can drive them.
///   PILLAR 3  WORD-LEVEL     — Fingers are written by us in LateUpdate (right + left).
///                              Letter handshape table (BuildPose) is used as fingerspelling
///                              fallback only. ASLSignPlayer drives word-level signs by
///                              setting hand-target transforms + calling SetFingerCurls.
///
/// FIRST-TIME SETUP: in the Editor run "Tools/ASL/Create Signing Avatar Prefab" once
/// to materialise Assets/Resources/ASL/SigningAvatar.prefab.
/// </summary>
public class ASLSigningCharacterManager : MonoBehaviour
{
    public static ASLSigningCharacterManager Instance { get; private set; }

    /// <summary>RenderTexture updated every frame with the signing character's view.</summary>
    public RenderTexture RT { get; private set; }

    /// <summary>True once the character, camera, bones and IK rig are all wired.</summary>
    public bool IsReady { get; private set; }

    /// <summary>Transform fed into RightArmIK. Move it to drive the right hand.</summary>
    public Transform RightHandTarget { get; private set; }

    /// <summary>Transform fed into LeftArmIK. Move it to drive the left hand.</summary>
    public Transform LeftHandTarget  { get; private set; }

    /// <summary>Intermediate parent of the hand targets. Its scale maps the
    /// REFERENCE-meter authoring space (ready/sign positions written for a
    /// life-size ~1.6 m-head avatar) onto THIS avatar's actual proportions, so
    /// the same numbers work regardless of avatarScale or RPM import size.</summary>
    Transform _ikRoot;

    /// <summary>The signing avatar's character-root transform (so the player can compute local positions).</summary>
    public Transform CharRoot => _charGO != null ? _charGO.transform : null;

    // ── RPM avatar configuration (v3) ─────────────────────────────────────────
    /// <summary>Resource path of the RPM signing prefab (Resources-relative). Override in Inspector for A/B.</summary>
    [Tooltip("Resources-relative path of the signing avatar prefab. Default = ASL/RPM_SigningAvatar (v3, with facial blendshapes). Set to ASL/SigningAvatar for v2 Mixamo A/B comparison.")]
    [SerializeField] string overridePrefabPath = "ASL/RPM_SigningAvatar";

    /// <summary>The Renderer_Head SkinnedMeshRenderer that holds facial blendshapes.
    /// Null if the loaded avatar is Mixamo (no head blendshapes).</summary>
    public SkinnedMeshRenderer HeadRenderer => _headRenderer;

    SkinnedMeshRenderer _headRenderer;
    SkinnedMeshRenderer _eyeLeftRenderer, _eyeRightRenderer, _teethRenderer;

    /// <summary>Lower-case blendshape name → index, built at startup by iterating
    /// <c>sharedMesh.GetBlendShapeName(i)</c>. Empty if the avatar has no blendshapes.</summary>
    readonly Dictionary<string, int> _blendIdx = new();

    // ── Wave 2: Face & life (real palette only — mouthSmile/mouthOpen + eye gaze + head + breath) ──
    Transform  _eyeLBone, _eyeRBone, _headBone, _breathBone;
    Quaternion _eyeLRest = Quaternion.identity, _eyeRRest = Quaternion.identity;
    Vector3    _breathBaseScale = Vector3.one;
    FacePose   _curFace, _tgtFace;          // lerped facial pose
    Vector2    _saccade;                     // current eye micro-saccade (pitch, yaw) in deg
    Vector2    _saccadeTarget;
    float      _nextSaccadeT;
    bool       _faceReady;

    /// <summary>The ScriptableObject of all tunable values. Loaded from
    /// <c>Resources/ASL/ASLBubbleSettings.asset</c> at startup. If missing,
    /// the manager falls back to the SerializeField defaults below.</summary>
    public ASLBubbleSettings Settings { get; private set; }

    /// <summary>Cached reference to the procedural arm-IK driver on the spawned avatar.
    /// v3 Wave 1.5-PASS3 — switched from ASLHumanoidArmIK (Unity Humanoid IK, which
    /// refused to lower the RPM arms below their A-pose bind) to ASLProceduralArmIK,
    /// a purely geometric analytic 2-bone solver that ignores muscle space entirely.</summary>
    ASLProceduralArmIK _armIK;

    // ── Configuration ─────────────────────────────────────────────────────────
    const int   RT_W           = 480;     // bumped from 300 — wider arm motion needs more pixels
    const int   RT_H           = 640;
    const float MAX_CURL_DEG   = 80f;
    const float MAX_SPREAD_DEG = 18f;

    // "Ready position" — chest-relative pose both hands return to between signs.
    // NOTE: These are now MUTABLE (not readonly) so they can be live-tuned via the
    // Inspector's Live Tuning section below. ASLSignPlayer reads them via static accessor.
    public static Vector3 READY_RIGHT_POS = new Vector3( 0.20f, 1.20f, 0.30f);
    public static Vector3 READY_LEFT_POS  = new Vector3(-0.20f, 1.20f, 0.30f);

    // Rest WRIST orientation (Euler°, target-local) — the relaxed palm pose hands hold
    // BETWEEN signs. Separate from the sign keyframes (which set palm orientation while
    // signing) so idle hands relax (palms inward) instead of facing the viewer. Live-tuned
    // via ASLBubbleSettings; ASLSignPlayer reads these for its return-to-ready target.
    public static Vector3 READY_WRIST_EULER_R = new Vector3(0f, -90f, 0f);
    public static Vector3 READY_WRIST_EULER_L = new Vector3(0f,  90f, 0f);

    // ── Live tuning (works in Play mode — find this GameObject in DontDestroyOnLoad) ──
    [Header("Live Tuning (Play-mode editable)")]
    [Tooltip("Right hand ready position. Chest-relative. (X=out from body, Y=height, Z=forward)")]
    [SerializeField] Vector3 _readyRight = new Vector3( 0.20f, 1.20f, 0.30f);
    [Tooltip("Left hand ready position. Auto-mirrors readyRight if leftMirrorRight is on.")]
    [SerializeField] Vector3 _readyLeft  = new Vector3(-0.20f, 1.20f, 0.30f);
    [SerializeField] bool _leftMirrorRight = true;

    [Header("Camera Tuning")]
    [Range(0.5f, 3.0f)] [SerializeField] float _camDistance      = 1.70f;
    [Range(-0.5f, 1.0f)][SerializeField] float _camYAboveHead    = 0.05f;
    [Range(-1.0f, 0.5f)][SerializeField] float _lookYBelowHead   = -0.30f;
    [Range(20f, 80f)]   [SerializeField] float _camFOV           = 42f;
    [Range(-0.5f, 0.5f)][SerializeField] float _camXOffset       = 0.10f;

    // Finger curl / spread axes in Mixamo Humanoid LOCAL bone space.
    static readonly Vector3 CURL_AXIS   = new Vector3(1f, 0f, 0f);   // +X = curl
    static readonly Vector3 SPREAD_AXIS = new Vector3(0f, 0f, 1f);   // +Z = spread

    // ── Private state ─────────────────────────────────────────────────────────
    GameObject _charGO;
    Camera     _cam;
    Animator   _anim;

    // Right + left hand finger bones — [f]=finger 0..4 (Index/Mid/Ring/Pinky/Thumb)
    // Inner index — [j]=0..2 (proximal/intermediate/distal)
    readonly Transform[][]  _bonesR    = new Transform[5][];
    readonly Quaternion[][] _restRotsR = new Quaternion[5][];
    readonly Transform[][]  _bonesL    = new Transform[5][];
    readonly Quaternion[][] _restRotsL = new Quaternion[5][];

    struct FingerPose { public float c0, c1, c2, spread; }
    readonly FingerPose[] _poseR = new FingerPose[5];
    readonly FingerPose[] _poseL = new FingerPose[5];

    // (Wave 1.5-CORRECTED) — IK is driven by ASLHumanoidArmIK component on the avatar,
    // not by Animation Rigging. See SetupIKRig().

    // ── Bootstrap ─────────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("ASLSigningCharacterManager");
        DontDestroyOnLoad(go);
        go.AddComponent<ASLSigningCharacterManager>();
        go.AddComponent<ASLSignPlayer>();   // word-level playback engine
        Debug.Log("[ASL-Char] Bootstrap invoked.");
    }

    /// <summary>The ASLSignPlayer attached to this GameObject (or null if missing).</summary>
    public ASLSignPlayer Player => GetComponent<ASLSignPlayer>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Try to load the editable settings asset. If missing, the SerializeField
        // defaults below will be used and the user gets a console hint.
        Settings = Resources.Load<ASLBubbleSettings>("ASL/ASLBubbleSettings");
        if (Settings != null)
        {
            // Pull initial values from the SO (the inspector fields stay in sync)
            _readyRight       = Settings.readyRight;
            _readyLeft        = Settings.readyLeft;
            _leftMirrorRight  = Settings.leftMirrorRight;
            _camDistance      = Settings.cameraDistance;
            _camYAboveHead    = Settings.cameraYAboveHead;
            _lookYBelowHead   = Settings.lookYBelowHead;
            _camFOV           = Settings.cameraFOV;
            _camXOffset       = Settings.cameraXOffset;
            Debug.Log("[ASL-Char] Settings loaded from Resources/ASL/ASLBubbleSettings.asset.");
        }
        else
        {
            Debug.LogWarning("[ASL-Char] No ASLBubbleSettings.asset found. " +
                             "Run Tools/ASL/Create Bubble Settings to make one. " +
                             "Falling back to SerializeField defaults.");
        }

        // Guard against zero defaults — Unity sometimes zeros SerializeFields
        // when a new field is added to an already-existing runtime component.
        if (_readyRight == Vector3.zero) _readyRight = new Vector3( 0.20f, 1.20f, 0.30f);
        if (_readyLeft  == Vector3.zero) _readyLeft  = new Vector3(-0.20f, 1.20f, 0.30f);
    }

    IEnumerator Start()
    {
        yield return null;
        yield return StartCoroutine(Setup());
    }

    // ── Setup ─────────────────────────────────────────────────────────────────
    IEnumerator Setup()
    {
        // 1 — load the configured prefab (RPM v3 by default; v2 Mixamo via override).
        //     Fall back to legacy v2 path, then AvatarCatalog, if the configured prefab is missing.
        string path = string.IsNullOrEmpty(overridePrefabPath) ? "ASL/RPM_SigningAvatar" : overridePrefabPath;
        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[ASL-Char] {path} not in Resources — falling back to ASL/SigningAvatar (v2 Mixamo).");
            prefab = Resources.Load<GameObject>("ASL/SigningAvatar");
        }
        if (prefab == null)
        {
            Debug.Log("[ASL-Char] No stripped prefab found — trying AvatarCatalog.");
            var catalog = Resources.Load<AvatarCatalog>("AvatarCatalog");
            if (catalog != null && catalog.Count > 0) prefab = catalog.GetPrefab(0);
        }
        if (prefab == null)
        {
            Debug.LogWarning("[ASL-Char] No signing avatar found. " +
                             "Run Tools/ASL/Build RPM Signing Prefab first.");
            yield break;
        }

        // 2 — spawn far below the scene. Position + scale come from ASLBubbleSettings.
        var spawnPos    = Settings != null ? Settings.spawnPosition : new Vector3(0f, -499.6f, 0f);
        float avScale   = Settings != null ? Settings.avatarScale   : 2f;
        _charGO = Instantiate(prefab, spawnPos, Quaternion.identity);
        _charGO.name = "ASL_SigningCharacter";
        DontDestroyOnLoad(_charGO);
        _charGO.transform.rotation   = Quaternion.identity;
        _charGO.transform.localScale = new Vector3(avScale, avScale, avScale);

        // 3 — silence networking / physics / XR components
        DisableNetworking(_charGO);
        DisablePhysics(_charGO);
        DisableXRComponents(_charGO);

        // 4 — Configure Animator.
        //     v2 Mixamo (Humanoid + Avatar)  — keep controller, idle plays.
        //     v3 RPM (Generic, no Avatar)    — keep the (empty Idle) controller so
        //                                       Animation Rigging's PlayableGraph ticks
        //                                       and drives the IK arms. Empty Idle = no
        //                                       retargeting = no "Avatar is null" exception.
        //     Wave 3 will add procedural breathing/sway in either case.
        _anim = _charGO.GetComponentInChildren<Animator>(true);
        if (_anim != null)
        {
            _anim.enabled         = true;
            _anim.applyRootMotion = false;
            Debug.Log($"[ASL-Char] Animator: avatar={(_anim.avatar != null ? _anim.avatar.name : "NULL/Generic")}, controller={(_anim.runtimeAnimatorController != null ? _anim.runtimeAnimatorController.name : "NULL")}");
        }

        yield return null;

        // 5 — cache finger bones for right + left hands
        CacheBones();
        if (_bonesR[0] == null || _bonesR[0][0] == null)
        {
            Debug.LogWarning("[ASL-Char] Right-hand finger bones not found. " +
                             "Ensure avatar's bone names are RPM-style (RightHandIndex1) " +
                             "or Mixamo-style (mixamorig7:RightHandIndex1).");
            yield break;
        }

        // 5b — Discover facial renderers + blendshape index map (v3 only; safe no-op on Mixamo).
        CacheFacialRenderersAndBlendshapes();

        // 6 — RT (bumped resolution + AA for cleaner arm motion)
        RT = new RenderTexture(RT_W, RT_H, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4,
            filterMode   = FilterMode.Bilinear
        };
        RT.Create();

        // 7 — discover existing RigBuilder + arm constraints, create our hand targets,
        //     re-parent the wrist sockets onto them.
        SetupIKRig();

        // 8 — secondary camera + key light
        SetupCamera(spawnPos);

        // 9 — neutral rest pose
        SetRest();
        IsReady = true;

        Debug.Log($"[ASL-Char] Ready — prefab=\"{path}\", RT={RT_W}x{RT_H}, fingers cached, " +
                  $"Animator={(_anim!=null && _anim.enabled ? "PLAYING" : "OFF")}, " +
                  $"isHuman={(_anim!=null && _anim.avatar != null && _anim.avatar.isHuman)}, " +
                  $"Head={(_headRenderer!=null ? "FOUND" : "NONE")}, " +
                  $"Blendshapes={_blendIdx.Count}.");
    }

    // ── IK rig wiring ─────────────────────────────────────────────────────────
    // v3 Wave 1.5-PASS3 — Uses a PURELY GEOMETRIC analytic 2-bone IK
    // (ASLProceduralArmIK) that writes shoulder+elbow local rotations directly.
    // This replaces both Animation Rigging's TwoBoneIKConstraint (unreliable on
    // Generic avatars) AND Unity's built-in Humanoid IK (which refused to lower
    // the runtime-built RPM arms below their imported A-pose bind). The procedural
    // solver lands the hand exactly at the world target regardless of bind pose,
    // and does NOT require a Humanoid Avatar at all.
    void SetupIKRig()
    {
        // 0 — Disable any stale Humanoid-IK driver baked into an older prefab so
        //     its OnAnimatorIK doesn't fight our procedural LateUpdate solve.
        var legacy = _charGO.GetComponentInChildren<ASLHumanoidArmIK>(true);
        if (legacy != null)
        {
            legacy.enabled = false;
            Debug.Log("[ASL-IK] Disabled legacy ASLHumanoidArmIK (superseded by ASLProceduralArmIK).");
        }

        // 1 — Find the procedural IK driver; ADD it at runtime if the prefab
        //     wasn't rebuilt yet (so the fix works even on the old prefab).
        var armIK = _charGO.GetComponentInChildren<ASLProceduralArmIK>(true);
        if (armIK == null)
        {
            // Must live on the Animator GameObject (where the bones resolve from).
            var host = _anim != null ? _anim.gameObject : _charGO;
            armIK = host.AddComponent<ASLProceduralArmIK>();
            Debug.Log("[ASL-IK] No ASLProceduralArmIK on prefab — added one at runtime.");
        }
        _armIK = armIK;
        armIK.enabled = true;
        Debug.Log($"[ASL-IK] ASLProceduralArmIK active on \"{armIK.gameObject.name}\".");

        // 2 — Resolve the 6 arm bones now (Humanoid map first, name fallback).
        armIK.ResolveBones();

        // 3 — Create an intermediate "IK target root" that maps REFERENCE-meters
        //     (the units all ready/sign positions are authored in, for a life-size
        //     ~1.6 m-head avatar) onto THIS avatar's ACTUAL size.
        //
        //     Two bugs this fixes:
        //     (a) A child Transform's OWN localScale does NOT cancel its parent's
        //         scale for its OWN localPosition — only an intermediate PARENT can.
        //         The old `target.localScale = 1/avatarScale` therefore did nothing,
        //         so a ready Y of 1.20 became 1.20 × avatarScale in world.
        //     (b) RPM imports tiny — head measured at ~0.71 m here, not 1.6 m — so
        //         even a "world-accurate" 1.20 would sit ABOVE the head. We measure
        //         the real head height and rescale the whole target space to match,
        //         so chest-height ready maps to chest height on THIS body.
        const float REFERENCE_HEAD_Y = 1.60f;
        float headWorldY = REFERENCE_HEAD_Y;          // fallback = no rescale
        if (_anim != null && _anim.avatar != null && _anim.avatar.isHuman)
        {
            var head = _anim.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) headWorldY = head.position.y - _charGO.transform.position.y;
        }
        float bodyScale = Mathf.Max(0.05f, headWorldY / REFERENCE_HEAD_Y);
        var charScale = _charGO.transform.localScale;

        var ikRootGO = new GameObject("ASL_IKRoot");
        ikRootGO.transform.SetParent(_charGO.transform, false);
        ikRootGO.transform.localPosition = Vector3.zero;
        ikRootGO.transform.localRotation = Quaternion.identity;
        // Cancel the avatar scale, then apply bodyScale: a child's localPosition in
        // REFERENCE meters lands at (bodyScale × that) in world space above the root.
        ikRootGO.transform.localScale = new Vector3(
            bodyScale / Mathf.Max(1e-4f, charScale.x),
            bodyScale / Mathf.Max(1e-4f, charScale.y),
            bodyScale / Mathf.Max(1e-4f, charScale.z));
        _ikRoot = ikRootGO.transform;

        var rGO = new GameObject("ASL_RightHandTarget");
        var lGO = new GameObject("ASL_LeftHandTarget");
        rGO.transform.SetParent(_ikRoot, false);
        lGO.transform.SetParent(_ikRoot, false);
        RightHandTarget = rGO.transform;
        LeftHandTarget  = lGO.transform;
        RightHandTarget.localPosition = READY_RIGHT_POS;
        LeftHandTarget.localPosition  = READY_LEFT_POS;
        RightHandTarget.localRotation = Quaternion.identity;
        LeftHandTarget.localRotation  = Quaternion.identity;
        Debug.Log($"[ASL-IK] Target space: headWorldY={headWorldY:F2}m, bodyScale={bodyScale:F3} — " +
                  $"ready {READY_RIGHT_POS} maps to ~{(READY_RIGHT_POS * bodyScale).y:F2}m above root (chest).");

        // 4 — Hand them to the IK driver. ASLProceduralArmIK reads these every
        //     LateUpdate and solves shoulder+elbow rotations so the hand lands
        //     exactly at the target (purely geometric — no muscle space).
        armIK.rightHandTarget = RightHandTarget;
        armIK.leftHandTarget  = LeftHandTarget;
        armIK.weight          = 1f;
        if (Settings != null)
        {
            armIK.rightElbowPole         = Settings.rightElbowHintOffset;
            armIK.rightHandRotationOffset = Settings.handRotationOffsetRight;
            armIK.leftHandRotationOffset  = Settings.handRotationOffsetLeft;
            armIK.forearmFollow           = Settings.forearmFollow;
            armIK.elbowHeightFollow       = Settings.elbowHeightFollow;
            armIK.elbowAbductFollow       = Settings.elbowAbductFollow;
        }

        Debug.Log($"[ASL-IK] Procedural IK wired — targets at chest-local ready position. " +
                  $"Sanity test: drag ASL_RightHandTarget in scene view, arm should follow.");
    }

    /// <summary>Format a Transform's path in its hierarchy (for diagnostics).</summary>
    static string GetPath(Transform t)
    {
        if (t == null) return "NULL";
        var sb = new System.Text.StringBuilder(t.name);
        while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
        return sb.ToString();
    }

    // ── Camera setup ─────────────────────────────────────────────────────────
    void SetupCamera(Vector3 charRootPos)
    {
        var camGO = new GameObject("ASL_SigningCam");
        camGO.transform.SetParent(transform);

        // Auto-frame using the actual Head bone position. RPM and Mixamo have
        // different heights, and AvatarBuilder can shift the body, so we measure
        // instead of guessing. Fall back to a safe Mixamo-style frame if head
        // can't be resolved (e.g. for v2 Mixamo prefab).
        float headLocalY = 1.55f;  // fallback (safe for both RPM and Mixamo)
        if (_anim != null && _anim.avatar != null && _anim.avatar.isHuman)
        {
            var head = _anim.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                // Convert to local-to-charRoot Y
                headLocalY = head.position.y - charRootPos.y;
                Debug.Log($"[ASL-Cam] Head bone detected at local Y={headLocalY:F2} — auto-framing.");
            }
        }

        // Frame the UPPER BODY (head + chest + arms). Camera at head height,
        // ~1.7 m forward (proportional to a 1.65 m-tall avatar). Looks at chest.
        float camY     = headLocalY + 0.05f;        // slightly above head top
        float lookY    = headLocalY - 0.30f;        // chest level
        var camPos     = charRootPos + new Vector3(0.10f, camY,  1.70f);
        var lookTarget = charRootPos + new Vector3(0.00f, lookY, 0.00f);
        camGO.transform.position = camPos;
        camGO.transform.LookAt(lookTarget);

        _cam = camGO.AddComponent<Camera>();
        _cam.targetTexture   = RT;
        _cam.clearFlags      = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.024f, 0.106f, 0.133f, 1f); // dark teal — matches bubble
        _cam.fieldOfView     = 42f;   // upper-body framing with shoulders fully visible
        _cam.nearClipPlane   = 0.05f;
        _cam.farClipPlane    = 6f;    // increased so taller scaled avatar isn't clipped
        _cam.depth           = -20f;
        _cam.stereoTargetEye = StereoTargetEyeMask.None;       // render once
        _cam.allowMSAA       = true;

        // Single directional key light parented to the camera — keeps the avatar
        // lit consistently regardless of whatever scene lighting happens to be active.
        var lightGO = new GameObject("ASL_KeyLight");
        lightGO.transform.SetParent(camGO.transform, false);
        lightGO.transform.localRotation = Quaternion.Euler(20f, -15f, 0f);
        var l = lightGO.AddComponent<Light>();
        l.type      = LightType.Directional;
        l.intensity = 1.1f;
        l.color     = new Color(1f, 0.98f, 0.94f);
    }

    // ── Bone Caching ──────────────────────────────────────────────────────────
    void CacheBones()
    {
        // Only tick the animator if it has a valid Humanoid Avatar — on Generic
        // RPM avatars, calling Update with avatar=null throws "Avatar is null".
        if (_anim != null && _anim.enabled && _anim.avatar != null && _anim.runtimeAnimatorController != null)
        {
            // Tick the animator once so bones have a valid pose to read rest rotations from.
            try { _anim.Update(0f); }
            catch (System.InvalidOperationException) { /* avatar mismatch — bind pose is fine */ }

            // RIGHT hand
            _bonesR[0] = Trio(_anim, HumanBodyBones.RightIndexProximal,  HumanBodyBones.RightIndexIntermediate,  HumanBodyBones.RightIndexDistal);
            _bonesR[1] = Trio(_anim, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal);
            _bonesR[2] = Trio(_anim, HumanBodyBones.RightRingProximal,   HumanBodyBones.RightRingIntermediate,   HumanBodyBones.RightRingDistal);
            _bonesR[3] = Trio(_anim, HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal);
            _bonesR[4] = Trio(_anim, HumanBodyBones.RightThumbProximal,  HumanBodyBones.RightThumbIntermediate,  HumanBodyBones.RightThumbDistal);

            // LEFT hand
            _bonesL[0] = Trio(_anim, HumanBodyBones.LeftIndexProximal,  HumanBodyBones.LeftIndexIntermediate,  HumanBodyBones.LeftIndexDistal);
            _bonesL[1] = Trio(_anim, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal);
            _bonesL[2] = Trio(_anim, HumanBodyBones.LeftRingProximal,   HumanBodyBones.LeftRingIntermediate,   HumanBodyBones.LeftRingDistal);
            _bonesL[3] = Trio(_anim, HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal);
            _bonesL[4] = Trio(_anim, HumanBodyBones.LeftThumbProximal,  HumanBodyBones.LeftThumbIntermediate,  HumanBodyBones.LeftThumbDistal);
        }

        // Fallback: name search for Generic rigs (mixamorig7 naming)
        if (_bonesR[0] == null || _bonesR[0][0] == null)
            CacheBonesByName();

        // Record rest local rotations
        for (int f = 0; f < 5; f++)
        {
            if (_bonesR[f] != null)
            {
                _restRotsR[f] = new Quaternion[3];
                for (int j = 0; j < 3; j++)
                    if (_bonesR[f][j] != null) _restRotsR[f][j] = _bonesR[f][j].localRotation;
            }
            if (_bonesL[f] != null)
            {
                _restRotsL[f] = new Quaternion[3];
                for (int j = 0; j < 3; j++)
                    if (_bonesL[f][j] != null) _restRotsL[f][j] = _bonesL[f][j].localRotation;
            }
        }
    }

    static Transform[] Trio(Animator a, HumanBodyBones p, HumanBodyBones i, HumanBodyBones d)
        => new[] { a.GetBoneTransform(p), a.GetBoneTransform(i), a.GetBoneTransform(d) };

    void CacheBonesByName()
    {
        // Build a name → Transform dictionary (case-insensitive, prefix-tolerant).
        // We index both "RightHandIndex1" (RPM) and "mixamorig7:RightHandIndex1" (Mixamo Generic).
        var dict = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var t in _charGO.GetComponentsInChildren<Transform>(true))
        {
            if (!dict.ContainsKey(t.name)) dict[t.name] = t;
            // Strip "prefix:" so a search for "RightHand" finds "mixamorig7:RightHand" too.
            int colon = t.name.IndexOf(':');
            if (colon > 0)
            {
                string bare = t.name.Substring(colon + 1);
                if (!dict.ContainsKey(bare)) dict[bare] = t;
            }
        }

        // Try the bare name first (RPM), then with prefix (Mixamo). Returns first match.
        Transform B(string bare) => dict.TryGetValue(bare, out var t) ? t
                                   : dict.TryGetValue("mixamorig7:" + bare, out var t2) ? t2
                                   : null;

        _bonesR[0] = new[] { B("RightHandIndex1"),  B("RightHandIndex2"),  B("RightHandIndex3")  };
        _bonesR[1] = new[] { B("RightHandMiddle1"), B("RightHandMiddle2"), B("RightHandMiddle3") };
        _bonesR[2] = new[] { B("RightHandRing1"),   B("RightHandRing2"),   B("RightHandRing3")   };
        _bonesR[3] = new[] { B("RightHandPinky1"),  B("RightHandPinky2"),  B("RightHandPinky3")  };
        _bonesR[4] = new[] { B("RightHandThumb1"),  B("RightHandThumb2"),  B("RightHandThumb3")  };

        _bonesL[0] = new[] { B("LeftHandIndex1"),  B("LeftHandIndex2"),  B("LeftHandIndex3")  };
        _bonesL[1] = new[] { B("LeftHandMiddle1"), B("LeftHandMiddle2"), B("LeftHandMiddle3") };
        _bonesL[2] = new[] { B("LeftHandRing1"),   B("LeftHandRing2"),   B("LeftHandRing3")   };
        _bonesL[3] = new[] { B("LeftHandPinky1"),  B("LeftHandPinky2"),  B("LeftHandPinky3")  };
        _bonesL[4] = new[] { B("LeftHandThumb1"),  B("LeftHandThumb2"),  B("LeftHandThumb3")  };
    }

    // ── Facial renderers + blendshape discovery (v3) ──────────────────────────
    /// <summary>
    /// Discovers <c>Renderer_Head</c>, <c>Renderer_EyeLeft/Right</c>, <c>Renderer_Teeth</c>
    /// on the loaded avatar and indexes all blendshape names on the head mesh
    /// (case-insensitive). No-op on Mixamo (no matching renderers, empty dict).
    /// </summary>
    void CacheFacialRenderersAndBlendshapes()
    {
        _blendIdx.Clear();
        _headRenderer = null;
        _eyeLeftRenderer = null;
        _eyeRightRenderer = null;
        _teethRenderer = null;

        foreach (var smr in _charGO.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string n = smr.name;
            if (string.Equals(n, "Renderer_Head",     System.StringComparison.OrdinalIgnoreCase)) _headRenderer    = smr;
            if (string.Equals(n, "Renderer_EyeLeft",  System.StringComparison.OrdinalIgnoreCase)) _eyeLeftRenderer = smr;
            if (string.Equals(n, "Renderer_EyeRight", System.StringComparison.OrdinalIgnoreCase)) _eyeRightRenderer= smr;
            if (string.Equals(n, "Renderer_Teeth",    System.StringComparison.OrdinalIgnoreCase)) _teethRenderer   = smr;
        }

        if (_headRenderer != null && _headRenderer.sharedMesh != null)
        {
            var mesh = _headRenderer.sharedMesh;
            int count = mesh.blendShapeCount;
            for (int i = 0; i < count; i++)
            {
                string raw = mesh.GetBlendShapeName(i);
                if (string.IsNullOrEmpty(raw)) continue;
                // Some glTF imports prefix with "blendShape." or mesh node names — strip a leading '.'-segment.
                string clean = raw;
                int dot = raw.LastIndexOf('.');
                if (dot >= 0 && dot < raw.Length - 1) clean = raw.Substring(dot + 1);
                string key = clean.ToLowerInvariant();
                if (!_blendIdx.ContainsKey(key)) _blendIdx[key] = i;
            }
            Debug.Log($"[ASL-Char] Discovered {count} head blendshapes. " +
                      $"Has mouthSmile={HasBlendshape("mouthSmile")}, mouthOpen={HasBlendshape("mouthOpen")} " +
                      $"(blink/brows NOT present on this avatar).");
        }

        // ── Wave 2: cache facial BONES (eye gaze + head tilt + breathing) ──
        _eyeLBone = _eyeRBone = _headBone = _breathBone = null;
        if (_anim != null && _anim.avatar != null && _anim.avatar.isHuman)
        {
            _eyeLBone   = _anim.GetBoneTransform(HumanBodyBones.LeftEye);
            _eyeRBone   = _anim.GetBoneTransform(HumanBodyBones.RightEye);
            _headBone   = _anim.GetBoneTransform(HumanBodyBones.Head);
            // Breathing rides the highest available chest bone.
            _breathBone = _anim.GetBoneTransform(HumanBodyBones.UpperChest)
                       ?? _anim.GetBoneTransform(HumanBodyBones.Chest)
                       ?? _anim.GetBoneTransform(HumanBodyBones.Spine);
        }
        if (_eyeLBone != null) _eyeLRest = _eyeLBone.localRotation;
        if (_eyeRBone != null) _eyeRRest = _eyeRBone.localRotation;
        if (_breathBone != null) _breathBaseScale = _breathBone.localScale;
        _curFace = _tgtFace = default;
        _faceReady = true;
        Debug.Log($"[ASL-Char] Face bones — eyes={( _eyeLBone!=null && _eyeRBone!=null ? "OK":"NONE")}, " +
                  $"head={(_headBone!=null?"OK":"NONE")}, breath={(_breathBone!=null?_breathBone.name:"NONE")}.");
    }

    /// <summary>Case-insensitive lookup. Returns true if any of the candidate names
    /// matches a discovered blendshape; out param receives the mesh index.</summary>
    public bool TryGetBlendshapeIndex(string[] candidates, out int idx)
    {
        foreach (var name in candidates)
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (_blendIdx.TryGetValue(name.ToLowerInvariant(), out idx)) return true;
        }
        idx = -1;
        return false;
    }

    /// <summary>Convenience: true if at least one of these names is a known blendshape.</summary>
    public bool HasBlendshape(params string[] candidates) => TryGetBlendshapeIndex(candidates, out _);

    /// <summary>
    /// Write a blendshape weight (0..100) by name. Tries case-insensitive lookup +
    /// common RPM variants ("eyeBlinkLeft", "eyeBlink_L", "EyeBlinkLeft"). Silently
    /// skips unknown names — caller should check <see cref="HasBlendshape"/> first.
    /// </summary>
    public void SetBlendshapeByName(string canonicalName, float weight)
    {
        if (_headRenderer == null) return;
        // Try the canonical name plus an underscore variant
        // (e.g. "eyeBlinkLeft" → also try "eyeBlink_L", "EyeBlinkLeft")
        var variants = BlendshapeNameVariants(canonicalName);
        if (TryGetBlendshapeIndex(variants, out int idx))
            _headRenderer.SetBlendShapeWeight(idx, weight);
    }

    /// <summary>Write a MOUTH blendshape (mouthSmile/mouthOpen) to the Head AND Teeth
    /// renderers so the deformation reads correctly. Deliberately skips the eye meshes —
    /// they carry the same morph but driving it there distorts the eyes.</summary>
    public void SetMouthBlendshape(string canonicalName, float weight)
    {
        if (!TryGetBlendshapeIndex(BlendshapeNameVariants(canonicalName), out int idx)) return;
        if (_headRenderer != null && idx < _headRenderer.sharedMesh.blendShapeCount)
            _headRenderer.SetBlendShapeWeight(idx, weight);
        if (_teethRenderer != null && _teethRenderer.sharedMesh != null
            && idx < _teethRenderer.sharedMesh.blendShapeCount)
            _teethRenderer.SetBlendShapeWeight(idx, weight);
    }

    // ── Wave 2: facial expression API ─────────────────────────────────────────
    /// <summary>Set the target facial expression for a sign (lerped in over a few frames).
    /// Uses only the avatar's real channels: smile, mouth-open, head tilt, gaze.</summary>
    public void SetFaceEmotion(SignEmotion emotion, float intensity = 1f)
    {
        _tgtFace = FaceExpressionPresets.For(emotion).Scaled(Mathf.Clamp01(intensity));
    }

    /// <summary>Relax the face back to neutral (lerped). Called when a sign ends.</summary>
    public void ClearFace() => _tgtFace = default;

    /// <summary>Builds the alternate-name list for a canonical ARKit blendshape name.
    /// Handles RPM's historic naming drift (Left/Right vs _L/_R, camelCase vs PascalCase).</summary>
    static string[] BlendshapeNameVariants(string canonical)
    {
        // Replace trailing "Left"/"Right" with "_L"/"_R" and add a Pascal variant
        string lower = canonical;
        string underL = canonical.EndsWith("Left",  System.StringComparison.Ordinal) ? canonical.Substring(0, canonical.Length - 4) + "_L" : null;
        string underR = canonical.EndsWith("Right", System.StringComparison.Ordinal) ? canonical.Substring(0, canonical.Length - 5) + "_R" : null;
        string pascal = char.ToUpperInvariant(canonical[0]) + canonical.Substring(1);
        var list = new List<string> { lower, pascal };
        if (underL != null) list.Add(underL);
        if (underR != null) list.Add(underR);
        return list.ToArray();
    }

    // ── Pose Application ──────────────────────────────────────────────────────
    // Runs AFTER the Animation Rigging update (rig writes the hand bone; we write the
    // hand's FINGER children in local space — they survive parent rotation).
    // Diagnostic: log on every CHANGE of target localPosition (rate-limited to once per 0.1s
    // when actually changing), and log the tip-follows-target relation.
    Vector3 _lastLoggedTargetLocal = new Vector3(-9999f, -9999f, -9999f);
    float   _ikDiagNextChangeT     = 0f;

    void LateUpdate()
    {
        if (!IsReady) return;
        ApplyLiveTuning();
        ApplyFingers(_bonesR, _restRotsR, _poseR);
        ApplyFingers(_bonesL, _restRotsL, _poseL);
        ApplyFaceAndLife();

        // Change-driven diagnostic — fires only when target actually moves (any axis > 1 cm).
        // Now reports the actual hand bone position via Humanoid mapping.
        if (RightHandTarget != null && _anim != null && _anim.isHuman)
        {
            var p = RightHandTarget.localPosition;
            if (Vector3.Distance(p, _lastLoggedTargetLocal) > 0.01f
                && Time.unscaledTime >= _ikDiagNextChangeT)
            {
                _lastLoggedTargetLocal = p;
                _ikDiagNextChangeT     = Time.unscaledTime + 0.1f;
                var handBone = _anim.GetBoneTransform(HumanBodyBones.RightHand);
                Debug.Log($"[ASL-IK] target CHANGED → local={p:F3} world={RightHandTarget.position:F3} | " +
                          $"RightHand bone world={(handBone != null ? handBone.position : Vector3.zero):F3}");
            }
        }
    }

    // ── Wave 2: face & life driver (runs every LateUpdate, after the Animator) ────
    // Drives ONLY the channels this RPM avatar supports: mouthSmile + mouthOpen
    // blendshapes, eye-bone gaze, head-bone tilt, and a subtle chest-scale breath.
    // No blink / brows / jaw exist on this avatar.
    void ApplyFaceAndLife()
    {
        if (!_faceReady) return;

        // Tunables (Settings overrides, else sane defaults)
        float lerpSpeed = Settings != null ? Settings.faceLerpSpeed   : 8f;
        float smileMax  = Settings != null ? Settings.smileMax        : 100f;
        float mouthMax  = Settings != null ? Settings.mouthOpenMax    : 100f;
        float gazeStr   = Settings != null ? Settings.gazeStrength    : 1f;
        float saccAmt   = Settings != null ? Settings.saccadeAmount   : 4f;
        float headInfl  = Settings != null ? Settings.headTiltInfluence : 1f;
        float breathAmp = Settings != null ? Settings.breathAmplitude : 0.012f;
        float breathHz  = Settings != null ? Settings.breathHz        : 0.25f;

        // 1 — Lerp current → target facial pose.
        _curFace = FacePose.Lerp(_curFace, _tgtFace, 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime));

        // 2 — Mouth blendshapes (Head + Teeth only).
        SetMouthBlendshape("mouthSmile", Mathf.Clamp(_curFace.smile,     0f, smileMax));
        SetMouthBlendshape("mouthOpen",  Mathf.Clamp(_curFace.mouthOpen, 0f, mouthMax));

        // 3 — Head tilt (pitch/nod about the avatar's right axis — axis-correct regardless
        //     of the head bone's local convention). Additive on top of the animated pose.
        if (_headBone != null && _charGO != null && Mathf.Abs(_curFace.headTiltDeg) > 0.01f && headInfl > 0f)
            _headBone.rotation = Quaternion.AngleAxis(_curFace.headTiltDeg * headInfl,
                                                      _charGO.transform.right) * _headBone.rotation;

        // 4 — Eye gaze. During a sign: lock to the viewer (rest = forward). Between signs:
        //     eased micro-saccades so the eyes feel alive. Plus any per-emotion yaw bias.
        bool signing = Player != null && Player.IsPlaying;
        if (!signing)
        {
            if (Time.unscaledTime >= _nextSaccadeT)
            {
                _nextSaccadeT  = Time.unscaledTime + UnityEngine.Random.Range(1.2f, 3.0f);
                _saccadeTarget = new Vector2(UnityEngine.Random.Range(-saccAmt, saccAmt) * 0.6f,
                                             UnityEngine.Random.Range(-saccAmt, saccAmt));
            }
        }
        else _saccadeTarget = Vector2.zero;                 // hold gaze on the viewer while signing
        _saccade = Vector2.Lerp(_saccade, _saccadeTarget, 1f - Mathf.Exp(-10f * Time.deltaTime));

        float gazePitch = _saccade.x * gazeStr;
        float gazeYaw   = (_saccade.y + _curFace.gazeYawBias) * gazeStr;
        if (_eyeLBone != null) _eyeLBone.localRotation = _eyeLRest * Quaternion.Euler(gazePitch, gazeYaw, 0f);
        if (_eyeRBone != null) _eyeRBone.localRotation = _eyeRRest * Quaternion.Euler(gazePitch, gazeYaw, 0f);

        // 5 — Breathing: gentle chest scale (inhale/exhale). Subtle by default.
        if (_breathBone != null && breathAmp > 0.0001f)
        {
            float breath = Mathf.Sin(Time.unscaledTime * breathHz * 2f * Mathf.PI) * breathAmp;
            _breathBone.localScale = _breathBaseScale * (1f + breath);
        }
    }

    /// <summary>Re-apply all live-tuned values from the Inspector each frame.
    /// Lets the user adjust READY position and camera framing while in Play mode.
    /// If a Settings asset is assigned, its values take precedence and are read
    /// every frame — so editing the asset in the Project window updates the bubble live.</summary>
    void ApplyLiveTuning()
    {
        // Source of truth: prefer the ScriptableObject if present, else SerializeFields.
        Vector3 readyR = Settings != null ? Settings.readyRight : _readyRight;
        Vector3 readyL = Settings != null ? Settings.GetReadyLeft()
                                          : (_leftMirrorRight ? new Vector3(-_readyRight.x, _readyRight.y, _readyRight.z) : _readyLeft);
        float   camD   = Settings != null ? Settings.cameraDistance    : _camDistance;
        float   camYU  = Settings != null ? Settings.cameraYAboveHead  : _camYAboveHead;
        float   lookY  = Settings != null ? Settings.lookYBelowHead    : _lookYBelowHead;
        float   fov    = Settings != null ? Settings.cameraFOV         : _camFOV;
        float   camX   = Settings != null ? Settings.cameraXOffset     : _camXOffset;

        // 1 — Ready positions + rest wrist orientation (the Player reads these statics)
        READY_RIGHT_POS = readyR;
        READY_LEFT_POS  = readyL;
        if (Settings != null)
        {
            READY_WRIST_EULER_R = Settings.readyWristEulerRight;
            READY_WRIST_EULER_L = Settings.readyWristEulerLeft;
        }
        // Mirror the Inspector view of the left field for visual feedback
        if (_leftMirrorRight && Settings == null) _readyLeft = READY_LEFT_POS;

        // 1b — Push the ready position AND relaxed rest wrist orientation directly to
        //      the IK targets ONLY when no sign is actively being played. Otherwise the
        //      Player coroutine owns the targets (it drives the per-sign wrist rotation).
        var player = Player;
        bool playerActive = player != null && player.IsPlaying;
        if (!playerActive)
        {
            if (RightHandTarget != null)
            {
                RightHandTarget.localPosition = readyR;
                RightHandTarget.localRotation = Quaternion.Euler(READY_WRIST_EULER_R);
            }
            if (LeftHandTarget != null)
            {
                LeftHandTarget.localPosition = readyL;
                LeftHandTarget.localRotation = Quaternion.Euler(READY_WRIST_EULER_L);
            }
        }

        // 2 — Camera framing — re-compute every frame based on current head bone
        //     position (so scale changes also retake effect).
        if (_cam != null && _charGO != null && _anim != null
            && _anim.avatar != null && _anim.avatar.isHuman)
        {
            var head = _anim.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                var charRoot = _charGO.transform.position;
                float headLocalY = head.position.y - charRoot.y;
                float camY  = headLocalY + camYU;
                float lookYW = headLocalY + lookY;
                _cam.transform.position = charRoot + new Vector3(camX, camY, camD);
                _cam.transform.LookAt(charRoot + new Vector3(0f, lookYW, 0f));
                _cam.fieldOfView = fov;
            }
        }

        // 3 — Avatar scale live-tunable (SO only)
        if (Settings != null && _charGO != null)
        {
            float wantScale = Mathf.Max(0.05f, Settings.avatarScale);
            if (Mathf.Abs(_charGO.transform.localScale.x - wantScale) > 0.001f)
                _charGO.transform.localScale = new Vector3(wantScale, wantScale, wantScale);
        }

        // 4 — Push the elbow pole hint + wrist orientation to the procedural IK
        //     driver every frame (live-tunable from the Settings asset). Weight
        //     stays at 1 — the hands MUST reach the target; the hint only steers
        //     elbow direction, the offsets only steer palm orientation.
        if (Settings != null && _armIK != null)
        {
            _armIK.rightElbowPole          = Settings.rightElbowHintOffset;
            _armIK.rightHandRotationOffset = Settings.handRotationOffsetRight;
            _armIK.leftHandRotationOffset  = Settings.handRotationOffsetLeft;
            _armIK.forearmFollow           = Settings.forearmFollow;
            _armIK.elbowHeightFollow       = Settings.elbowHeightFollow;
            _armIK.elbowAbductFollow       = Settings.elbowAbductFollow;
            _armIK.weight                  = 1f;
        }
    }

    static void ApplyFingers(Transform[][] bones, Quaternion[][] rests, FingerPose[] pose)
    {
        for (int f = 0; f < 5; f++)
        {
            if (bones[f] == null || rests[f] == null) continue;
            float[] curls  = { pose[f].c0, pose[f].c1, pose[f].c2 };
            float   spread = pose[f].spread;
            for (int j = 0; j < 3; j++)
            {
                if (bones[f][j] == null) continue;
                float curlDeg   = curls[j] * MAX_CURL_DEG;
                float spreadDeg = (j == 0) ? spread * MAX_SPREAD_DEG : 0f;
                bones[f][j].localRotation =
                    rests[f][j]
                    * Quaternion.AngleAxis(curlDeg,   CURL_AXIS)
                    * Quaternion.AngleAxis(spreadDeg, SPREAD_AXIS);
            }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Drive the right hand into the ASL handshape for letter c (fingerspelling).
    /// The left hand is reset to neutral. The hand TARGET position is not changed —
    /// the caller is expected to place the right hand at signing height.</summary>
    public void SetLetter(char c)
    {
        if (!IsReady) return;
        BuildPose(char.ToUpperInvariant(c));
        for (int f = 0; f < 5; f++) _poseL[f] = default;
    }

    /// <summary>Return BOTH hands to neutral / ready position
    /// (fingers relaxed + targets at chest height).</summary>
    public void SetRest()
    {
        if (!IsReady) return;
        ClearFace();   // Wave 2: relax facial expression to neutral on rest.
        for (int f = 0; f < 5; f++) { _poseR[f] = default; _poseL[f] = default; }
        if (RightHandTarget != null)
        {
            RightHandTarget.localPosition = READY_RIGHT_POS;
            RightHandTarget.localRotation = Quaternion.Euler(READY_WRIST_EULER_R);
        }
        if (LeftHandTarget != null)
        {
            LeftHandTarget.localPosition = READY_LEFT_POS;
            LeftHandTarget.localRotation = Quaternion.Euler(READY_WRIST_EULER_L);
        }
    }

    /// <summary>
    /// Set all five finger curls on one hand directly (0..1 per finger).
    /// Used by ASLSignPlayer to lerp through SignKeyframes.
    /// </summary>
    public void SetFingerCurls(bool rightHand,
        float idx, float mid, float ring, float pinky, float thumb)
    {
        var pose = rightHand ? _poseR : _poseL;
        pose[0] = new FingerPose { c0 = idx,   c1 = idx,   c2 = idx,   spread = 0 };
        pose[1] = new FingerPose { c0 = mid,   c1 = mid,   c2 = mid,   spread = 0 };
        pose[2] = new FingerPose { c0 = ring,  c1 = ring,  c2 = ring,  spread = 0 };
        pose[3] = new FingerPose { c0 = pinky, c1 = pinky, c2 = pinky, spread = 0 };
        pose[4] = new FingerPose { c0 = thumb, c1 = thumb, c2 = thumb, spread = 0 };
    }

    // ── ASL Handshape Table (fingerspelling fallback) ─────────────────────────
    // Finger order:  f=0 Index, f=1 Middle, f=2 Ring, f=3 Pinky, f=4 Thumb
    // Per bone:      c0=proximal, c1=intermediate, c2=distal  (0=straight, 1=fully curled)
    // Spread:        +1 = full abduction, -1 = full adduction / pinch toward thumb
    void BuildPose(char c)
    {
        switch (c)
        {
            // ── FIST-BASED ────────────────────────────────────────────────
            case 'A':
                P(0, .80f,.80f,.80f, .00f); P(1, .80f,.80f,.80f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .40f,.30f,.10f,-.30f); break;
            case 'E':
                P(0, .60f,.70f,.70f, .00f); P(1, .60f,.70f,.70f, .00f);
                P(2, .60f,.70f,.70f, .00f); P(3, .60f,.70f,.70f, .00f);
                P(4, .50f,.40f,.20f,-.50f); break;
            case 'M':
                P(0, .70f,.70f,.70f, .00f); P(1, .70f,.70f,.70f, .00f);
                P(2, .70f,.70f,.70f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .45f,.30f,.10f,-.30f); break;
            case 'N':
                P(0, .70f,.70f,.70f, .00f); P(1, .70f,.70f,.70f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .45f,.30f,.10f,-.30f); break;
            case 'S':
                P(0, .90f,.90f,.90f, .00f); P(1, .90f,.90f,.90f, .00f);
                P(2, .90f,.90f,.90f, .00f); P(3, .90f,.90f,.90f, .00f);
                P(4, .60f,.30f,.00f,-.40f); break;
            case 'T':
                P(0, .80f,.80f,.80f, .00f); P(1, .80f,.80f,.80f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .55f,.30f,.00f,-.20f); break;
            // ── OPEN / FLAT HAND ──────────────────────────────────────────
            case 'B':
                P(0, .00f,.00f,.00f, .00f); P(1, .00f,.00f,.00f, .00f);
                P(2, .00f,.00f,.00f, .00f); P(3, .00f,.00f,.00f, .00f);
                P(4, .60f,.50f,.40f,-.80f); break;
            case 'W':
                P(0, .00f,.00f,.00f, .30f); P(1, .00f,.00f,.00f, .00f);
                P(2, .00f,.00f,.00f,-.30f); P(3, .80f,.80f,.80f, .00f);
                P(4, .30f,.20f,.00f,-.40f); break;
            // ── ONE FINGER POINTING ───────────────────────────────────────
            case 'D':
                P(0, .00f,.00f,.00f, .00f); P(1, .80f,.80f,.80f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .50f,.30f,.10f, .30f); break;
            case 'G':
                P(0, .00f,.00f,.00f, .00f); P(1, .80f,.80f,.80f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .20f,.10f,.00f, .20f); break;
            case 'Z':
                P(0, .00f,.00f,.00f, .00f); P(1, .80f,.80f,.80f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .35f,.25f,.10f,-.30f); break;
            // ── TWO FINGERS ───────────────────────────────────────────────
            case 'U':
                P(0, .00f,.00f,.00f, .10f); P(1, .00f,.00f,.00f,-.10f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .50f,.40f,.20f,-.50f); break;
            case 'H':
                P(0, .00f,.00f,.00f, .10f); P(1, .00f,.00f,.00f,-.10f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .50f,.40f,.20f,-.50f); break;
            case 'V':
                P(0, .00f,.00f,.00f, .55f); P(1, .00f,.00f,.00f,-.55f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .50f,.40f,.20f,-.50f); break;
            case 'K':
                P(0, .00f,.00f,.00f, .00f); P(1, .45f,.60f,.60f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .30f,.20f,.00f, .30f); break;
            case 'P':
                P(0, .00f,.00f,.00f, .00f); P(1, .45f,.60f,.60f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .30f,.20f,.00f, .30f); break;
            case 'R':
                P(0, .00f,.00f,.00f,-.60f); P(1, .00f,.00f,.00f, .40f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .50f,.40f,.20f,-.50f); break;
            // ── CURVED / BENT ─────────────────────────────────────────────
            case 'C':
                P(0, .40f,.30f,.20f, .10f); P(1, .40f,.30f,.20f, .00f);
                P(2, .40f,.30f,.20f, .00f); P(3, .40f,.30f,.20f, .00f);
                P(4, .30f,.20f,.10f, .50f); break;
            case 'O':
                P(0, .55f,.50f,.40f,-.10f); P(1, .55f,.50f,.40f, .00f);
                P(2, .55f,.50f,.40f, .00f); P(3, .55f,.50f,.40f, .00f);
                P(4, .50f,.35f,.20f, .60f); break;
            case 'F':
                P(0, .50f,.50f,.40f, .00f); P(1, .00f,.00f,.00f, .20f);
                P(2, .00f,.00f,.00f, .00f); P(3, .00f,.00f,.00f, .00f);
                P(4, .50f,.35f,.20f, .50f); break;
            case 'Q':
                P(0, .00f,.00f,.00f, .00f); P(1, .80f,.80f,.80f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .20f,.10f,.00f, .30f); break;
            // ── THUMB-PROMINENT ───────────────────────────────────────────
            case 'L':
                P(0, .00f,.00f,.00f, .00f); P(1, .80f,.80f,.80f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .00f,.00f,.00f,1.00f); break;
            case 'Y':
                P(0, .80f,.80f,.80f, .00f); P(1, .80f,.80f,.80f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .00f,.00f,.00f, .00f);
                P(4, .00f,.00f,.00f, .90f); break;
            // ── PINKY ─────────────────────────────────────────────────────
            case 'I':
                P(0, .80f,.80f,.80f, .00f); P(1, .80f,.80f,.80f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .00f,.00f,.00f, .00f);
                P(4, .50f,.30f,.10f,-.30f); break;
            case 'J':
                P(0, .80f,.80f,.80f, .00f); P(1, .80f,.80f,.80f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .00f,.00f,.00f, .00f);
                P(4, .50f,.30f,.10f,-.30f); break;
            // ── HOOKED ────────────────────────────────────────────────────
            case 'X':
                P(0, .40f,.70f,.45f, .00f); P(1, .80f,.80f,.80f, .00f);
                P(2, .80f,.80f,.80f, .00f); P(3, .80f,.80f,.80f, .00f);
                P(4, .50f,.30f,.10f,-.30f); break;

            default: SetRest(); return;
        }
    }

    void P(int f, float c0, float c1, float c2, float spread)
        => _poseR[f] = new FingerPose { c0 = c0, c1 = c1, c2 = c2, spread = spread };

    // ── Component Disablers ───────────────────────────────────────────────────
    static void DisableNetworking(GameObject go)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string fn = mb.GetType().FullName ?? "";
            if (fn.Contains("Network"))
            {
                try { mb.enabled = false; }
                catch { /* suppress NetworkObject startup errors */ }
            }
        }
    }

    static void DisablePhysics(GameObject go)
    {
        foreach (var cc in go.GetComponentsInChildren<CharacterController>(true))
            cc.enabled = false;
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic      = true;
            rb.detectCollisions = false;
        }
    }

    /// <summary>
    /// Exact-name match (NOT substring) so keyword "Hand" doesn't accidentally
    /// disable HandsController / similar harmless components. Plan-agent flagged.
    /// </summary>
    static void DisableXRComponents(GameObject go)
    {
        var exactDisable = new HashSet<string> {
            // Coupled to network sync / VR input — would fight our overrides or NRE
            "JointBasedHand", "XRHandCurler", "HandCurler",
            "XRHandPoseReplicator", "XRHandFingerPoseReplicator",
            "FingerPoseReplicator",
            // Drive root/head from XR rig — irrelevant for this hidden avatar
            "AvatarHeadFollower", "AvatarRoomscaleAndYawFollower",
            "AvatarModalityIKBinder", "AvatarSpawnController",
            "WristMenuController",
            // Visual / IK that targets VR anchors
            "XRAvatarVisuals", "XRAvatarIK", "XRAvatarIK_RPM", "XRAvatarVisuals_RPM",
            "NetworkPlayerPositionSync",
            // Ready Player Me Core components — irrelevant for offline RT use,
            // some throw on missing internet/account at startup
            "AvatarData", "AvatarLoader", "AvatarObjectLoader",
            "ReadyPlayerMeAvatar", "RpmAvatarConfig"
        };
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            if (exactDisable.Contains(mb.GetType().Name))
                mb.enabled = false;
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────
    void OnDestroy()
    {
        if (RT != null) { RT.Release(); Destroy(RT); }
        if (_charGO != null) Destroy(_charGO);
    }
}
