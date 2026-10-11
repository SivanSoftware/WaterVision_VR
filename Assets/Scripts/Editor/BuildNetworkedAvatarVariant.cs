using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Builds a NETWORKED player-avatar prefab variant for a different Mixamo body, reusing the
/// fully-wired base prefab (Mixamo_Network_Player_Avatar) as the template.
///
/// MVP-1 scope (this pass): body + walk/idle animation + arms that reach the synced hand
/// anchors (Animation-Rigging TwoBoneIK). Finger-curl networking is intentionally deferred
/// to a second pass — it lives on nested stock-hand prefab instances under the hand anchors,
/// which survive the body swap untouched.
///
/// What it does, given a new Mixamo FBX:
///   1. Loads the base prefab's contents.
///   2. Instantiates the new body, copies the old body's transform + layer + sibling index.
///   3. Rewires the body-dependent references away from the old body onto the new one:
///        - Animator (controller copied, avatar set from the new FBX),
///        - NetworkAnimator.m_Animator,
///        - XRAvatarVisuals.m_headRend / m_HeadRends,
///        - XRAvatarIK.m_Neck / m_TorsoParentTransform / m_HeadVisualsRoot (only the refs
///          that pointed INTO the old body are remapped, by bone-suffix; anchor refs are left).
///   4. Rebuilds RigBuilder + Rig + 2× TwoBoneIKConstraint on the new body, prefix-agnostic
///      (works for any mixamorigN: namespace), targeting wrist-offset empties under the synced
///      "Left Hand"/"Right Hand" anchors.
///   5. Deletes the old body and saves a new prefab.
///
/// Bone lookups are PREFIX-AGNOSTIC: the 6 Mixamo avatars use different namespaces
/// (mixamorig:, mixamorig2:, mixamorig6/7/8:), so we match by the suffix after ':'.
/// </summary>
public static class BuildNetworkedAvatarVariant
{
    const string k_BasePrefab = "Assets/Mixamo/Mixamo_Network_Player_Avatar.prefab";

    // Wrist axis offsets (copied from the working offline AvatarModalityIKBinder / Ch08 setup).
    static readonly Vector3 k_LeftWristEuler  = new Vector3(0f, 90f, 90f);
    static readonly Vector3 k_RightWristEuler = new Vector3(0f, -90f, -90f);

    // The 5 imported Mixamo avatars (Ch08 stays the base/original prefab).
    static readonly string[] k_AvatarNames = { "Ch07", "Ch22", "Ch23", "Ch33", "Ch37" };

    [MenuItem("Tools/WaterVision/Build Networked Avatar Variant (Ch07)")]
    public static void BuildCh07()
    {
        BuildVariant(
            fbxPath: "Assets/Mixamo/Avatars/Ch07_nonPBR.fbx",
            outPath: "Assets/Mixamo/Avatars/Ch07_Network_Player_Avatar.prefab");
    }

    [MenuItem("Tools/WaterVision/Build ALL Networked Avatar Variants")]
    public static void BuildAll()
    {
        foreach (var n in k_AvatarNames)
            BuildVariant(
                fbxPath: $"Assets/Mixamo/Avatars/{n}_nonPBR.fbx",
                outPath: $"Assets/Mixamo/Avatars/{n}_Network_Player_Avatar.prefab");
    }

    /// <summary>
    /// The variant prefabs were saved from the BASE prefab's loaded contents, so they all inherited
    /// the base's NetworkObject GlobalObjectIdHash (2237070320). NGO keys network prefabs by that hash,
    /// so duplicates make avatar selection resolve every variant to one prefab. This forces each
    /// variant's NetworkObject to recompute its hash from its OWN asset GUID, then re-saves it.
    /// Run once, then verify the 6 prefabs have 6 distinct GlobalObjectIdHash values.
    /// </summary>
    [MenuItem("Tools/WaterVision/Regenerate Avatar Prefab Hashes")]
    public static void RegenerateHashes()
    {
        const System.Reflection.BindingFlags k_NP = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var paths = new System.Collections.Generic.List<string>();
        foreach (var n in k_AvatarNames)
        {
            string path = $"Assets/Mixamo/Avatars/{n}_Network_Player_Avatar.prefab";
            paths.Add(path);
            // Load the REAL asset (asset context) — NOT LoadPrefabContents (preview-scene context),
            // which produced wrong, non-deterministic hashes that disagree between Editor and build.
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) { Debug.LogWarning($"[RegenerateHashes] missing: {path}"); continue; }
            var netObj = GetComp(go, "NetworkObject");
            if (netObj == null) { Debug.LogWarning($"[RegenerateHashes] no NetworkObject: {path}"); continue; }
            // OnValidate recomputes GlobalObjectIdHash from the asset's GlobalObjectId (GUID-derived,
            // deterministic, identical in a build) and SetDirty's it.
            var mi = netObj.GetType().GetMethod("OnValidate", k_NP);
            if (mi != null) mi.Invoke(netObj, null);
            EditorUtility.SetDirty(netObj);
        }
        AssetDatabase.SaveAssets();
        // Reserialize so the persisted value matches a clean import (== what the build bakes).
        AssetDatabase.ForceReserializeAssets(paths);
        AssetDatabase.SaveAssets();
        Debug.Log("[RegenerateHashes] Done (asset-context). Verify the 6 prefabs now have distinct GlobalObjectIdHash.");
    }

    public static void BuildVariant(string fbxPath, string outPath)
    {
        GameObject newModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (newModel == null) { Fail($"FBX not found: {fbxPath}"); return; }

        // The humanoid Avatar sub-asset of the imported FBX (drives Animator retargeting).
        Avatar newAvatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
        if (newAvatar == null)
            Debug.LogWarning($"[BuildNetworkedAvatarVariant] No Avatar sub-asset in {fbxPath} — is it imported as Humanoid?");

        GameObject root = PrefabUtility.LoadPrefabContents(k_BasePrefab);
        if (root == null) { Fail($"Could not load base prefab: {k_BasePrefab}"); return; }

        try
        {
            // --- Locate the OLD body via its Animator (the NetworkAnimator's target). ---
            Animator oldAnimator = root.GetComponentInChildren<Animator>(true);
            if (oldAnimator == null) { Fail("No Animator found under the base prefab (old body)."); return; }
            Transform oldBody = oldAnimator.transform;

            // --- Instantiate the NEW body as a plain copy (not a nested prefab). ---
            GameObject newBodyGo = Object.Instantiate(newModel);
            newBodyGo.name = newModel.name; // strip "(Clone)"
            Transform newBody = newBodyGo.transform;
            newBody.SetParent(oldBody.parent, false);
            newBody.SetSiblingIndex(oldBody.GetSiblingIndex());
            newBody.localPosition = oldBody.localPosition;
            newBody.localRotation = oldBody.localRotation;
            newBody.localScale = oldBody.localScale;
            SetLayerRecursive(newBodyGo, oldBody.gameObject.layer);

            // --- New Animator: copy controller from old, set the new humanoid avatar. ---
            Animator newAnimator = newBodyGo.GetComponent<Animator>();
            if (newAnimator == null) newAnimator = newBodyGo.AddComponent<Animator>();
            newAnimator.runtimeAnimatorController = oldAnimator.runtimeAnimatorController;
            if (newAvatar != null) newAnimator.avatar = newAvatar;
            newAnimator.applyRootMotion = oldAnimator.applyRootMotion;
            newAnimator.updateMode = oldAnimator.updateMode;
            newAnimator.cullingMode = oldAnimator.cullingMode;

            // --- Preserve NetworkAnimator: it lives on the OLD body GO (next to its Animator),
            // so deleting the old body would drop it. Copy it onto the new body, repoint m_Animator. ---
            var oldNetAnim = FindCompInChildren(root, "NetworkAnimator");
            if (oldNetAnim != null)
            {
                UnityEditorInternal.ComponentUtility.CopyComponent(oldNetAnim);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(newBodyGo);
                var newNetAnim = FindCompInChildren(newBodyGo, "NetworkAnimator");
                if (newNetAnim != null) SetObjectField(newNetAnim, "m_Animator", newAnimator);
            }
            else Debug.LogWarning("[BuildNetworkedAvatarVariant] NetworkAnimator not found — animation sync will be missing.");

            // --- Rewire XRAvatarVisuals renderers → new body. ---
            var visuals = GetComp(root, "XRAvatarVisuals");
            var newSmrs = newBodyGo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (visuals != null)
            {
                // m_headRend: the body skin renderer (drives mouth blendshape). Prefer one named "*Body".
                SkinnedMeshRenderer bodyRend =
                    newSmrs.FirstOrDefault(r => r.name.ToLower().Contains("body")) ??
                    newSmrs.FirstOrDefault();
                SetObjectField(visuals, "m_headRend", bodyRend);
                SetObjectArray(visuals, "m_HeadRends", newSmrs);
            }

            // --- Rewire XRAvatarIK body-dependent refs (only those pointing into the old body). ---
            var ik = GetComp(root, "XRAvatarIK");
            if (ik != null)
            {
                RemapIfUnderOldBody(ik, "m_Neck", oldBody, newBody);
                RemapIfUnderOldBody(ik, "m_TorsoParentTransform", oldBody, newBody);
                RemapIfUnderOldBody(ik, "m_HeadVisualsRoot", oldBody, newBody);
            }

            // --- Rebuild Animation Rigging arm IK on the new body. ---
            Transform leftAnchor = root.transform.Find("Left Hand");
            Transform rightAnchor = root.transform.Find("Right Hand");
            int leftOk = 0, rightOk = 0;
            if (leftAnchor == null || rightAnchor == null)
            {
                Debug.LogWarning("[BuildNetworkedAvatarVariant] Hand anchors not found — skipping arm IK.");
            }
            else
            {
                var rigBuilder = newBodyGo.GetComponent<RigBuilder>() ?? newBodyGo.AddComponent<RigBuilder>();
                Transform rigT = newBody.Find("Rig 1");
                Rig rig;
                if (rigT == null)
                {
                    var rigGo = new GameObject("Rig 1");
                    rigGo.transform.SetParent(newBody, false);
                    rig = rigGo.AddComponent<Rig>();
                    rigT = rigGo.transform;
                }
                else
                {
                    rig = rigT.GetComponent<Rig>() ?? rigT.gameObject.AddComponent<Rig>();
                }
                rig.weight = 1f;

                rigBuilder.layers.Clear();
                rigBuilder.layers.Add(new RigLayer(rig, true));

                leftOk = SetupArm(rigT, newBody, leftAnchor, "Left", k_LeftWristEuler);
                rightOk = SetupArm(rigT, newBody, rightAnchor, "Right", k_RightWristEuler);
            }

            // --- Remove the old body. ---
            Object.DestroyImmediate(oldBody.gameObject);

            // --- Save the new prefab. ---
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, outPath, out bool ok);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BuildNetworkedAvatarVariant] {(ok ? "OK" : "FAILED")} → {outPath} | " +
                      $"body={newModel.name}, avatar={(newAvatar != null)}, " +
                      $"renderers={newSmrs.Length}, leftArm={leftOk}/3, rightArm={rightOk}/3. " +
                      $"(Fingers deferred to pass 2.)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Prefix-agnostic TwoBoneIK setup for one arm (shoulder/arm/hand by bone suffix).</summary>
    static int SetupArm(Transform rigT, Transform body, Transform anchor, string side, Vector3 wristEuler)
    {
        Transform shoulder = FindBoneBySuffix(body, $"{side}Shoulder");
        Transform arm = FindBoneBySuffix(body, $"{side}Arm");      // upper arm (NOT ForeArm)
        Transform hand = FindBoneBySuffix(body, $"{side}Hand");

        int found = (shoulder != null ? 1 : 0) + (arm != null ? 1 : 0) + (hand != null ? 1 : 0);
        if (found < 3)
            Debug.LogWarning($"[BuildNetworkedAvatarVariant] {side}: bones resolved {found}/3 " +
                             $"(Shoulder={shoulder != null}, Arm={arm != null}, Hand={hand != null}).");

        Transform target = anchor.Find($"{side}WristOffset");
        if (target == null)
        {
            var go = new GameObject($"{side}WristOffset");
            go.transform.SetParent(anchor, false);
            target = go.transform;
        }
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.Euler(wristEuler);

        Transform hint = rigT.Find($"{side}ElbowHint");
        if (hint == null)
        {
            var go = new GameObject($"{side}ElbowHint");
            go.transform.SetParent(rigT, false);
            hint = go.transform;
        }
        if (arm != null) hint.position = arm.position + Vector3.down * 0.3f;

        Transform cT = rigT.Find($"{side}ArmIK");
        if (cT == null)
        {
            var go = new GameObject($"{side}ArmIK");
            go.transform.SetParent(rigT, false);
            cT = go.transform;
        }
        var c = cT.GetComponent<TwoBoneIKConstraint>() ?? cT.gameObject.AddComponent<TwoBoneIKConstraint>();

        var d = c.data;
        d.root = shoulder;
        d.mid = arm;
        d.tip = hand;
        d.target = target;
        d.hint = hint;
        d.targetPositionWeight = 1f;
        d.targetRotationWeight = 1f;
        d.hintWeight = 0.2f;
        c.data = d;
        c.weight = 1f;

        return found;
    }

    // ---- Bone lookup: match by the part after ':' (or exact), so any mixamorigN: prefix works. ----
    static Transform FindBoneBySuffix(Transform root, string suffix)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            int colon = n.IndexOf(':');
            string bare = colon >= 0 ? n.Substring(colon + 1) : n;
            if (bare == suffix) return t;
        }
        return null;
    }

    // ---- Component access by type name (avoids assembly-reference coupling). ----
    static Component GetComp(GameObject go, string typeName)
    {
        foreach (var c in go.GetComponents<Component>())
            if (c != null && c.GetType().Name == typeName) return c;
        return null;
    }

    static Component FindCompInChildren(GameObject go, string typeName)
    {
        foreach (var c in go.GetComponentsInChildren<Component>(true))
            if (c != null && c.GetType().Name == typeName) return c;
        return null;
    }

    static void SetObjectField(Object component, string prop, Object value)
    {
        var so = new SerializedObject(component);
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[BuildNetworkedAvatarVariant] {component.GetType().Name}.{prop} not found."); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetObjectArray(Object component, string prop, Object[] values)
    {
        var so = new SerializedObject(component);
        var p = so.FindProperty(prop);
        if (p == null || !p.isArray) { Debug.LogWarning($"[BuildNetworkedAvatarVariant] {component.GetType().Name}.{prop} not an array."); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>If the named Transform ref points into the old body, remap it to the matching
    /// bone (by suffix) in the new body. Refs that live elsewhere (e.g. under an anchor) are kept.</summary>
    static void RemapIfUnderOldBody(Object component, string prop, Transform oldBody, Transform newBody)
    {
        var so = new SerializedObject(component);
        var p = so.FindProperty(prop);
        if (p == null) return;
        var cur = p.objectReferenceValue as Transform;
        if (cur == null) return;
        if (cur == oldBody) { p.objectReferenceValue = newBody; so.ApplyModifiedPropertiesWithoutUndo(); return; } // body root → new body root
        if (!cur.IsChildOf(oldBody)) return; // not body-dependent — leave it

        string n = cur.name;
        int colon = n.IndexOf(':');
        string suffix = colon >= 0 ? n.Substring(colon + 1) : n;
        Transform repl = FindBoneBySuffix(newBody, suffix);
        if (repl != null)
        {
            p.objectReferenceValue = repl;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning($"[BuildNetworkedAvatarVariant] {prop}: no '{suffix}' bone in new body — left unset.");
        }
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
    }

    static void Fail(string reason) => Debug.LogError("[BuildNetworkedAvatarVariant] FAILED: " + reason);
}
