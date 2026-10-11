using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// One-click build of `Assets/Resources/ASL/RPM_SigningAvatar.prefab`. Takes a
/// Ready Player Me source avatar (clean Humanoid, zero networking), and wires:
///
///   1.  A new <c>SigningInterpreterAnimator.controller</c> with a single Idle state
///       sourced from the Ch08 Mixamo idle clip via Humanoid retargeting, masked
///       to UPPER BODY ONLY excluding fingers (so our finger-curl API has full
///       authority over the hands).
///   2.  <c>RigBuilder</c> on the prefab ROOT (the Animator's GameObject — Plan-agent
///       flagged that putting it on a child silently breaks).
///   3.  Two <c>TwoBoneIKConstraint</c>s under a <c>Rig 1</c> child:
///         RightArmIK : RightShoulder → RightArm → RightHand
///         LeftArmIK  : LeftShoulder  → LeftArm  → LeftHand
///       Targets = wrist offset GameObjects whose parent <see cref="Transform"/> is
///       what <c>ASLSigningCharacterManager.RightHandTarget</c>/<c>LeftHandTarget</c>
///       drives at runtime.
///       Hints = elbow hint GameObjects positioned ~30 cm below+behind each elbow.
///   4.  Asserts post-build that <c>Animator.GetBoneTransform(RightHand) == tip</c>
///       so Humanoid mapping is correct (Plan-agent risk #8).
///
/// Output is a STANDALONE prefab (not a variant) — Plan-agent risk #7: variants
/// get nuked when the RPM base prefab is re-imported.
///
/// Run from menu: Tools/ASL/Build RPM Signing Prefab.
/// Reads source via the const at top of the file — edit if you want a different RPM avatar.
/// </summary>
public static class ASL_BuildRPMSigningPrefab
{
    // ── Configuration ────────────────────────────────────────────────────────
    const string MENU      = "Tools/ASL/Build RPM Signing Prefab";
    const string SRC_PATH  = "Assets/Ready Player Me/Avatars/69354c76347390125d4a0cc7/2fac66e374c947c41bc74325c6e3d934/69354c76347390125d4a0cc7.prefab";
    const string OUT_DIR   = "Assets/Resources/ASL";
    const string OUT_PATH  = "Assets/Resources/ASL/RPM_SigningAvatar.prefab";
    const string CTRL_PATH   = "Assets/Resources/ASL/SigningInterpreterAnimator.controller";
    const string MASK_PATH   = "Assets/Resources/ASL/SigningInterpreterMask.mask";
    const string IDLE_FBX    = "Assets/Mixamo/Character/Animations/Ch08_nonPBR@Idle.fbx";
    const string AVATAR_PATH = "Assets/Resources/ASL/RPMSigningAvatar_Humanoid.asset";

    // ── Entry point ──────────────────────────────────────────────────────────
    [MenuItem(MENU)]
    public static void Build()
    {
        try
        {
            EnsureFolder(OUT_DIR);

            // 1 — Validate source prefab.
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(SRC_PATH);
            if (src == null)
            {
                Debug.LogError($"[ASL-RPM] Source RPM prefab not found at {SRC_PATH}");
                return;
            }

            // 2 — Build avatar mask (upper body, fingers excluded).
            var mask = BuildUpperBodyMask();

            // 3 — Build animator controller (single Idle state, Mixamo clip retargeted).
            var idleClip = LoadIdleClip();
            var ctrl     = BuildIdleController(idleClip, mask);
            if (ctrl == null) { Debug.LogError("[ASL-RPM] Failed to build animator controller."); return; }

            // 4 — Load source prefab into edit mode.
            var root = PrefabUtility.LoadPrefabContents(SRC_PATH);
            try
            {
                // 4a — Configure Animator. RPM avatars may be imported as Generic OR Humanoid
                //      depending on the SDK version + glTFast settings. We support both:
                //      if Humanoid, GetBoneTransform works; if Generic, fall back to name search.
                var anim = root.GetComponent<Animator>();
                if (anim == null) anim = root.AddComponent<Animator>();
                bool isHuman = anim.avatar != null && anim.avatar.isHuman;
                Debug.Log($"[ASL-RPM] Source Animator: avatar={(anim.avatar != null ? anim.avatar.name : "NULL")}, isHuman={isHuman}");
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion           = false;
                anim.cullingMode               = AnimatorCullingMode.AlwaysAnimate; // hidden-at-y=-500

                // 4b — Resolve arm bones. Try Humanoid first, fall back to name-based.
                //      v1.5: Tip is FOREARM (matches working v2 Mixamo geometry — the IK
                //      pulls the forearm transform to the target; hand dangles from wrist).
                Transform rShoulder = null, rUpper = null, rForeArm = null;
                Transform lShoulder = null, lUpper = null, lForeArm = null;
                if (isHuman)
                {
                    rShoulder = anim.GetBoneTransform(HumanBodyBones.RightShoulder);
                    rUpper    = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    rForeArm  = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
                    lShoulder = anim.GetBoneTransform(HumanBodyBones.LeftShoulder);
                    lUpper    = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    lForeArm  = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                }
                if (rShoulder == null) rShoulder = FindByName(root.transform, "RightShoulder");
                if (rUpper    == null) rUpper    = FindByName(root.transform, "RightArm");
                if (rForeArm  == null) rForeArm  = FindByName(root.transform, "RightForeArm");
                if (lShoulder == null) lShoulder = FindByName(root.transform, "LeftShoulder");
                if (lUpper    == null) lUpper    = FindByName(root.transform, "LeftArm");
                if (lForeArm  == null) lForeArm  = FindByName(root.transform, "LeftForeArm");
                if (rShoulder == null || rUpper == null || rForeArm == null ||
                    lShoulder == null || lUpper == null || lForeArm == null)
                {
                    Debug.LogError($"[ASL-RPM] Could not resolve arm bones. " +
                                   $"R={(rShoulder!=null)}/{(rUpper!=null)}/{(rForeArm!=null)} " +
                                   $"L={(lShoulder!=null)}/{(lUpper!=null)}/{(lForeArm!=null)}");
                    return;
                }
                Debug.Log($"[ASL-RPM] Bones resolved: RShoulder={rShoulder.name}, RArm={rUpper.name}, RForeArm={rForeArm.name}");

                // 4b.5 — CRITICAL: Pose the skeleton into a clean T-pose BEFORE building
                //        the Humanoid Avatar. Unity's Humanoid IK uses the bone rotations
                //        captured at build time as the muscle-space "zero". RPM imports in
                //        A-pose (arms angled down ~130°); if we don't fix this, the IK
                //        solver thinks "arms down" is muscle zero and refuses to lower the
                //        arm below that point — resulting in hands stuck up at face level.
                //        After build, we DON'T need to restore — the mesh follows the bones
                //        and the T-pose is invisible (avatar spawned at y=-500).
                PoseIntoTPose(rShoulder, rUpper, rForeArm, +1f);
                PoseIntoTPose(lShoulder, lUpper, lForeArm, -1f);
                Debug.Log($"[ASL-RPM] ✓ T-pose applied to arm chain before BuildHumanAvatar.");

                // 4c — Build a HUMANOID Avatar asset and assign it to the Animator.
                //      RPM imports as Generic via glTFast; Unity's built-in IK
                //      (Animator.SetIKPosition) requires Humanoid. We construct a
                //      HumanDescription via AvatarBuilder.BuildHumanAvatar() and
                //      save the result as an asset for the prefab to reference.
                var humanAvatar = BuildHumanoidAvatar(root);
                if (humanAvatar == null || !humanAvatar.isValid || !humanAvatar.isHuman)
                {
                    Debug.LogError($"[ASL-RPM] Humanoid Avatar build FAILED. " +
                                   $"isValid={humanAvatar?.isValid}, isHuman={humanAvatar?.isHuman}. " +
                                   $"Check that RPM bone names match expected (RightArm, LeftHand, etc.).");
                    return;
                }
                anim.avatar = humanAvatar;
                Debug.Log($"[ASL-RPM] ✓ Humanoid Avatar built — saved to {AVATAR_PATH}, " +
                          $"isValid=True, isHuman=True");

                // 4d — Add the PROCEDURAL arm-IK driver (analytic 2-bone solver).
                //      ASLSigningCharacterManager.SetupIKRig finds this at runtime
                //      and assigns rightHandTarget/leftHandTarget. This replaces the
                //      old Humanoid-IK (Animator.SetIKPosition) driver, which refused
                //      to lower the runtime-built RPM arms below their A-pose bind.
                //      The procedural solver is purely geometric — the hand lands
                //      exactly at the world target regardless of bind pose.
                var procIK = root.GetComponent<ASLProceduralArmIK>();
                if (procIK == null) procIK = root.AddComponent<ASLProceduralArmIK>();
                procIK.weight = 1f;
                procIK.rightElbowPole   = new Vector3(0.30f, -0.40f, -0.30f);
                procIK.leftMirrorsRight = true;

                // Remove the stale Humanoid-IK driver if a previous build added one,
                // so its OnAnimatorIK doesn't fight the procedural LateUpdate solve.
                var legacyIK = root.GetComponent<ASLHumanoidArmIK>();
                if (legacyIK != null) Object.DestroyImmediate(legacyIK);

                // 4e — Strip any missing scripts (RPM SDK references that aren't installed).
                int strippedMissing = 0;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    strippedMissing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                if (strippedMissing > 0)
                    Debug.Log($"[ASL-RPM] Stripped {strippedMissing} missing-script components.");

                // 4f — Save as STANDALONE prefab (not variant).
                var saved = PrefabUtility.SaveAsPrefabAsset(root, OUT_PATH);
                if (saved == null)
                {
                    Debug.LogError($"[ASL-RPM] SaveAsPrefabAsset returned null for {OUT_PATH}");
                    return;
                }

                Debug.Log(
                    $"[ASL-RPM] ✓ Built {OUT_PATH}\n" +
                    $"          src:    {SRC_PATH}\n" +
                    $"          ctrl:   {CTRL_PATH} (IK Pass enabled)\n" +
                    $"          mask:   {MASK_PATH}\n" +
                    $"          avatar: {AVATAR_PATH} (Humanoid built via AvatarBuilder)\n" +
                    $"          IK:     ASLProceduralArmIK (analytic 2-bone, bind-pose independent)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ASL-RPM] Build failed: {e}");
        }
    }

    [MenuItem(MENU, validate = true)]
    static bool Validate() => !EditorApplication.isPlaying;

    // ── Helpers ──────────────────────────────────────────────────────────────

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        // Recursively create
        var parts  = folder.Split('/');
        var built  = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = built + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(built, parts[i]);
            built = next;
        }
    }

    /// <summary>Builds (or reuses) an Avatar Mask covering upper body but
    /// EXCLUDING fingers — so our procedural finger curls own the hands.</summary>
    static AvatarMask BuildUpperBodyMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MASK_PATH);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, MASK_PATH);
        }
        // Index → Humanoid body-part:
        //   0 root, 1 body, 2 head, 3 left leg, 4 right leg,
        //   5 left arm, 6 right arm, 7 left fingers, 8 right fingers,
        //   9 left IK foot, 10 right IK foot, 11 left IK hand, 12 right IK hand
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root,        false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body,        true);   // spine breathing
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head,        true);   // head motion
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg,     false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg,    false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm,     true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm,    true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, false);  // OUR fingers
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers,false);  // OUR fingers
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK,  false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK,  false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);
        EditorUtility.SetDirty(mask);
        return mask;
    }

    /// <summary>Find Ch08 idle clip inside its FBX import. Returns null on RPM avatars
    /// because Ch08 clip bone-paths use "mixamorig7:" prefix which won't match RPM
    /// (Generic, clean names). The empty Idle state leaves the bind pose intact;
    /// Wave 3 adds procedural breathing/sway that drives the body alive.</summary>
    static AnimationClip LoadIdleClip()
    {
        // INTENTIONALLY return null for v3 RPM build — see comment above.
        // Wave 3 (autonomous life) handles breathing/sway procedurally.
        return null;

        // Legacy v2 path (Mixamo Ch08): the clip's curves use prefixed paths so this
        // worked because the Ch08 prefab had those exact bone names. Kept here as
        // reference, currently dead code.
        // var all = AssetDatabase.LoadAllAssetsAtPath(IDLE_FBX);
        // foreach (var a in all)
        //     if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
        //         return clip;
        // return null;
    }

    /// <summary>Build a 1-state Animator Controller with the mask on its base layer.</summary>
    static AnimatorController BuildIdleController(AnimationClip idleClip, AvatarMask mask)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL_PATH);
        if (ctrl == null)
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(CTRL_PATH);

        if (ctrl.layers.Length == 0)
        {
            ctrl.AddLayer("Base");
        }

        var layers = ctrl.layers;
        layers[0].avatarMask    = mask;
        layers[0].defaultWeight = 1f;
        layers[0].blendingMode  = AnimatorLayerBlendingMode.Override;
        layers[0].iKPass        = true;   // CRITICAL: OnAnimatorIK only fires when IK Pass is on
        ctrl.layers = layers;             // write back (Unity requires reassigning the array)

        var sm = ctrl.layers[0].stateMachine;
        // Clear any pre-existing states (idempotent re-runs)
        foreach (var s in sm.states)
            sm.RemoveState(s.state);

        var idleState = sm.AddState("Idle");
        if (idleClip != null) idleState.motion = idleClip;
        sm.defaultState = idleState;

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    /// <summary>Recursively search the hierarchy under root for a Transform whose name
    /// matches (case-insensitive). Returns null if not found.</summary>
    static Transform FindByName(Transform root, string name)
    {
        if (string.Equals(root.name, name, System.StringComparison.OrdinalIgnoreCase))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var t = FindByName(root.GetChild(i), name);
            if (t != null) return t;
        }
        return null;
    }

    static GameObject FindOrCreateChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>
    /// Rotate the 3 arm bones so the chain points straight along ±X in WORLD space
    /// (T-pose). Operates outside-in: shoulder first, then upperArm, then foreArm.
    /// Each pass realigns the bone's "tail direction" (toward its next child in the
    /// arm chain) with the desired world axis using <see cref="Quaternion.FromToRotation"/>.
    ///
    /// <paramref name="sign"/> = +1 for right arm (chain points +X), -1 for left (-X).
    /// </summary>
    static void PoseIntoTPose(Transform shoulder, Transform upperArm, Transform foreArm, float sign)
    {
        if (shoulder == null || upperArm == null || foreArm == null) return;

        Vector3 targetDir = new Vector3(sign, 0f, 0f); // world +X or -X

        AlignBoneToward(shoulder, upperArm.position, targetDir);
        AlignBoneToward(upperArm, foreArm.position,   targetDir);

        // For forearm, "tail" is the wrist/hand. Find first child to use as tail.
        if (foreArm.childCount > 0)
        {
            var hand = foreArm.GetChild(0);
            AlignBoneToward(foreArm, hand.position, targetDir);
        }
    }

    /// <summary>Rotate <paramref name="bone"/> so the vector from its position to
    /// <paramref name="tailWorldPos"/> aligns with <paramref name="targetWorldDir"/>.</summary>
    static void AlignBoneToward(Transform bone, Vector3 tailWorldPos, Vector3 targetWorldDir)
    {
        Vector3 currentDir = (tailWorldPos - bone.position).normalized;
        if (currentDir.sqrMagnitude < 0.0001f) return;
        Quaternion delta = Quaternion.FromToRotation(currentDir, targetWorldDir.normalized);
        bone.rotation = delta * bone.rotation;
    }

    // ── Humanoid Avatar builder (Wave 1.5-CORRECTED) ─────────────────────────
    /// <summary>
    /// Build a Humanoid Avatar asset from the RPM avatar's bone hierarchy.
    /// Required because RPM imports via glTFast as Generic (Animator.avatar = null),
    /// but Unity's built-in IK (Animator.SetIKPosition) needs Humanoid.
    ///
    /// Saves the result to <see cref="AVATAR_PATH"/> and returns the Avatar asset.
    /// Returns null if the hierarchy doesn't have the required Humanoid bones.
    /// </summary>
    static Avatar BuildHumanoidAvatar(GameObject root)
    {
        // Index every transform by name, prefer exact match, fall back to bare suffix
        // (handles any "prefix:" naming style — RPM has none, but harmless).
        var lookup = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!lookup.ContainsKey(t.name)) lookup[t.name] = t;
            int colon = t.name.IndexOf(':');
            if (colon > 0)
            {
                string bare = t.name.Substring(colon + 1);
                if (!lookup.ContainsKey(bare)) lookup[bare] = t;
            }
        }

        // Map (HumanBodyBones enum NAME) → (transform NAME in RPM hierarchy).
        // Source: previous explore-agent verified the RPM avatar's bone names.
        var humanMap = new Dictionary<string, string>
        {
            { "Hips",            "Hips" },
            { "Spine",           "Spine" },
            { "Chest",           "Spine1" },
            { "UpperChest",      "Spine2" },
            { "Neck",            "Neck" },
            { "Head",            "Head" },
            { "LeftEye",         "LeftEye" },
            { "RightEye",        "RightEye" },

            { "LeftUpperLeg",    "LeftUpLeg" },
            { "LeftLowerLeg",    "LeftLeg" },
            { "LeftFoot",        "LeftFoot" },
            { "LeftToes",        "LeftToeBase" },

            { "RightUpperLeg",   "RightUpLeg" },
            { "RightLowerLeg",   "RightLeg" },
            { "RightFoot",       "RightFoot" },
            { "RightToes",       "RightToeBase" },

            { "LeftShoulder",    "LeftShoulder" },
            { "LeftUpperArm",    "LeftArm" },
            { "LeftLowerArm",    "LeftForeArm" },
            { "LeftHand",        "LeftHand" },

            { "RightShoulder",   "RightShoulder" },
            { "RightUpperArm",   "RightArm" },
            { "RightLowerArm",   "RightForeArm" },
            { "RightHand",       "RightHand" },

            // Right hand fingers
            { "RightThumbProximal",        "RightHandThumb1" },
            { "RightThumbIntermediate",    "RightHandThumb2" },
            { "RightThumbDistal",          "RightHandThumb3" },
            { "RightIndexProximal",        "RightHandIndex1" },
            { "RightIndexIntermediate",    "RightHandIndex2" },
            { "RightIndexDistal",          "RightHandIndex3" },
            { "RightMiddleProximal",       "RightHandMiddle1" },
            { "RightMiddleIntermediate",   "RightHandMiddle2" },
            { "RightMiddleDistal",         "RightHandMiddle3" },
            { "RightRingProximal",         "RightHandRing1" },
            { "RightRingIntermediate",     "RightHandRing2" },
            { "RightRingDistal",           "RightHandRing3" },
            { "RightLittleProximal",       "RightHandPinky1" },
            { "RightLittleIntermediate",   "RightHandPinky2" },
            { "RightLittleDistal",         "RightHandPinky3" },

            // Left hand fingers
            { "LeftThumbProximal",         "LeftHandThumb1" },
            { "LeftThumbIntermediate",     "LeftHandThumb2" },
            { "LeftThumbDistal",           "LeftHandThumb3" },
            { "LeftIndexProximal",         "LeftHandIndex1" },
            { "LeftIndexIntermediate",     "LeftHandIndex2" },
            { "LeftIndexDistal",           "LeftHandIndex3" },
            { "LeftMiddleProximal",        "LeftHandMiddle1" },
            { "LeftMiddleIntermediate",    "LeftHandMiddle2" },
            { "LeftMiddleDistal",          "LeftHandMiddle3" },
            { "LeftRingProximal",          "LeftHandRing1" },
            { "LeftRingIntermediate",      "LeftHandRing2" },
            { "LeftRingDistal",            "LeftHandRing3" },
            { "LeftLittleProximal",        "LeftHandPinky1" },
            { "LeftLittleIntermediate",    "LeftHandPinky2" },
            { "LeftLittleDistal",          "LeftHandPinky3" },
        };

        var humanBones = new List<HumanBone>();
        var skeleton   = new List<SkeletonBone>();

        // SkeletonBone[0] MUST describe the root GameObject itself.
        skeleton.Add(new SkeletonBone {
            name = root.name,
            position = root.transform.localPosition,
            rotation = root.transform.localRotation,
            scale    = root.transform.localScale,
        });

        // Walk every transform and add to skeleton.
        int missingBones = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root.transform) continue;
            skeleton.Add(new SkeletonBone {
                name = t.name,
                position = t.localPosition,
                rotation = t.localRotation,
                scale    = t.localScale,
            });
        }

        // Build the human bones mapping.
        foreach (var pair in humanMap)
        {
            string enumName  = pair.Key;
            string boneName  = pair.Value;
            if (!lookup.TryGetValue(boneName, out var t))
            {
                missingBones++;
                Debug.LogWarning($"[ASL-RPM] Humanoid mapping: required bone '{boneName}' not found (for {enumName}).");
                continue;
            }
            humanBones.Add(new HumanBone {
                humanName    = enumName,
                boneName     = t.name,
                limit        = new HumanLimit { useDefaultValues = true }
            });
        }

        if (missingBones > 5)
        {
            Debug.LogError($"[ASL-RPM] Too many Humanoid bones missing ({missingBones}). Cannot build avatar.");
            return null;
        }

        var hd = new HumanDescription {
            human    = humanBones.ToArray(),
            skeleton = skeleton.ToArray(),
            // Reasonable defaults (Unity reference rig)
            upperArmTwist   = 0.5f,
            lowerArmTwist   = 0.5f,
            upperLegTwist   = 0.5f,
            lowerLegTwist   = 0.5f,
            armStretch      = 0.05f,
            legStretch      = 0.05f,
            feetSpacing     = 0f,
            hasTranslationDoF = false,
        };

        var avatar = AvatarBuilder.BuildHumanAvatar(root, hd);
        if (avatar == null || !avatar.isValid)
        {
            Debug.LogError("[ASL-RPM] AvatarBuilder.BuildHumanAvatar returned invalid Avatar.");
            return null;
        }
        avatar.name = "RPMSigningAvatar_Humanoid";

        // Save the avatar as an asset so the prefab can reference it persistently.
        var existing = AssetDatabase.LoadAssetAtPath<Avatar>(AVATAR_PATH);
        if (existing != null) AssetDatabase.DeleteAsset(AVATAR_PATH);
        AssetDatabase.CreateAsset(avatar, AVATAR_PATH);
        return avatar;
    }
}
