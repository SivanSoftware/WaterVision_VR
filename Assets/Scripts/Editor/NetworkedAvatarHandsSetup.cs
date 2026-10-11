using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// One-click setup that brings the offline avatar's high-quality Mixamo hand system
/// to the NETWORKED player prefab so OTHER players see real Mixamo arms (and later
/// fingers) instead of the floating stock "purple" sample hands.
///
/// PHASE 1 (this menu): on Mixamo_Network_Player_Avatar.prefab —
///   * Add Animation Rigging (RigBuilder + a Rig + 2 TwoBoneIKConstraints) to the
///     Ch08_nonPBR Mixamo body so its arms bend to reach the wrists.
///   * Drive the IK from the already-synced "Left Hand"/"Right Hand" anchors
///     (ClientNetworkTransform) via small offset child empties — so the arms reach
///     correctly on EVERY client (owner AND remote) with NO extra networking.
///   * Hide the stock sample hand meshes (disable their renderers only — the anchor,
///     its ClientNetworkTransform and JointBasedHand stay intact so XRHandPoseReplicator
///     does not NullReference).
///
/// Idempotent: re-running updates existing objects instead of duplicating.
///
/// Usage: Tools ▸ WaterVision ▸ Setup Networked Hands (Phase 1: Arms + Hide Stock Hands)
/// Then save is automatic (writes the prefab asset). Enter Play to verify.
/// </summary>
public static class NetworkedAvatarHandsSetup
{
    const string k_PrefabPath = "Assets/Mixamo/Mixamo_Network_Player_Avatar.prefab";

    // Wrist axis offsets copied from the working offline AvatarModalityIKBinder.
    static readonly Vector3 k_LeftWristEuler  = new Vector3(0f, 90f, 90f);
    static readonly Vector3 k_RightWristEuler = new Vector3(0f, -90f, -90f);

    [MenuItem("Tools/WaterVision/Setup Networked Hands (Phase 1: Arms + Hide Stock Hands)")]
    public static void SetupPhase1()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(k_PrefabPath);
        if (root == null)
        {
            Debug.LogError($"[NetworkedAvatarHandsSetup] Could not load prefab: {k_PrefabPath}");
            return;
        }

        try
        {
            Transform ch08 = FindDeep(root.transform, "Ch08_nonPBR");
            if (ch08 == null) { Fail("Ch08_nonPBR not found under the prefab root."); return; }

            Transform leftAnchor  = root.transform.Find("Left Hand");
            Transform rightAnchor = root.transform.Find("Right Hand");
            if (leftAnchor == null || rightAnchor == null)
            {
                Fail($"Hand anchors not found (Left Hand={leftAnchor != null}, Right Hand={rightAnchor != null}).");
                return;
            }

            // --- RigBuilder on the Mixamo body (must share the GameObject with the Animator) ---
            var rigBuilder = ch08.GetComponent<RigBuilder>();
            if (rigBuilder == null) rigBuilder = ch08.gameObject.AddComponent<RigBuilder>();

            // --- "Rig 1" child holding the constraints ---
            Transform rigT = ch08.Find("Rig 1");
            Rig rig;
            if (rigT == null)
            {
                var rigGo = new GameObject("Rig 1");
                rigGo.transform.SetParent(ch08, false);
                rig = rigGo.AddComponent<Rig>();
                rigT = rigGo.transform;
            }
            else
            {
                rig = rigT.GetComponent<Rig>();
                if (rig == null) rig = rigT.gameObject.AddComponent<Rig>();
            }
            rig.weight = 1f;

            // Register the rig layer once.
            bool hasLayer = false;
            foreach (var layer in rigBuilder.layers)
                if (layer != null && layer.rig == rig) { hasLayer = true; layer.active = true; }
            if (!hasLayer) rigBuilder.layers.Add(new RigLayer(rig, true));

            int leftOk  = SetupArm(rigT, ch08, leftAnchor,  "Left",  k_LeftWristEuler);
            int rightOk = SetupArm(rigT, ch08, rightAnchor, "Right", k_RightWristEuler);

            int hiddenL = HideStockHandRenderers(leftAnchor);
            int hiddenR = HideStockHandRenderers(rightAnchor);

            PrefabUtility.SaveAsPrefabAsset(root, k_PrefabPath);
            AssetDatabase.SaveAssets();

            string msg =
                $"Phase 1 complete on {System.IO.Path.GetFileName(k_PrefabPath)}. " +
                $"RigBuilder + Rig: OK | " +
                $"Left arm IK bones resolved: {leftOk}/3 | " +
                $"Right arm IK bones resolved: {rightOk}/3 | " +
                $"Stock hand renderers hidden: L={hiddenL}, R={hiddenR} | " +
                (leftOk == 3 && rightOk == 3
                    ? "Enter Play (2 clients) to verify."
                    : "WARNING: some arm bones were not found — check the mixamorig7 bone names.");
            // NOTE: no EditorUtility.DisplayDialog here — a modal dialog blocks Unity and times
            // out MCP automation. Result is logged to the Console instead.
            Debug.Log("[NetworkedAvatarHandsSetup] " + msg);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Builds (or updates) one arm's TwoBoneIKConstraint + its wrist-offset target empty
    /// (parented under the synced hand anchor) + a soft elbow hint. Returns how many of the
    /// 3 chain bones (shoulder/arm/hand) were resolved.
    /// </summary>
    static int SetupArm(Transform rigT, Transform ch08, Transform anchor, string side, Vector3 wristEuler)
    {
        Transform shoulder = FindDeep(ch08, $"mixamorig7:{side}Shoulder");
        Transform arm      = FindDeep(ch08, $"mixamorig7:{side}Arm");
        Transform hand     = FindDeep(ch08, $"mixamorig7:{side}Hand");

        int found = (shoulder != null ? 1 : 0) + (arm != null ? 1 : 0) + (hand != null ? 1 : 0);
        if (found < 3)
            Debug.LogWarning($"[NetworkedAvatarHandsSetup] {side}: bones resolved {found}/3 " +
                             $"(Shoulder={shoulder != null}, Arm={arm != null}, Hand={hand != null}).");

        // Wrist-offset target empty, parented UNDER the synced anchor so it tracks the wrist
        // on every client. The local rotation applies the Mixamo-vs-controller wrist offset.
        Transform target = anchor.Find($"{side}WristOffset");
        if (target == null)
        {
            var go = new GameObject($"{side}WristOffset");
            go.transform.SetParent(anchor, false);
            target = go.transform;
        }
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.Euler(wristEuler);

        // Soft elbow hint (pole), parented under the Rig (NOT the skeleton — avoids feedback).
        Transform hint = rigT.Find($"{side}ElbowHint");
        if (hint == null)
        {
            var go = new GameObject($"{side}ElbowHint");
            go.transform.SetParent(rigT, false);
            hint = go.transform;
        }
        if (arm != null)
            hint.position = arm.position + Vector3.down * 0.3f; // bias the elbow downward (VR-natural)

        // The constraint itself, under the Rig.
        Transform cT = rigT.Find($"{side}ArmIK");
        if (cT == null)
        {
            var go = new GameObject($"{side}ArmIK");
            go.transform.SetParent(rigT, false);
            cT = go.transform;
        }
        var c = cT.GetComponent<TwoBoneIKConstraint>();
        if (c == null) c = cT.gameObject.AddComponent<TwoBoneIKConstraint>();

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

    /// <summary>
    /// Disables every (Skinned)MeshRenderer under the hand anchor so the stock "purple"
    /// sample hand mesh becomes invisible. Keeps the GameObjects, ClientNetworkTransform and
    /// JointBasedHand alive (XRHandPoseReplicator dereferences the JointBasedHand on spawn).
    /// Returns the number of renderers disabled.
    /// </summary>
    static int HideStockHandRenderers(Transform anchor)
    {
        int n = 0;
        foreach (var r in anchor.GetComponentsInChildren<Renderer>(true))
        {
            if (r.enabled) { r.enabled = false; n++; }
        }
        return n;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindDeep(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    static void Fail(string reason)
    {
        Debug.LogError("[NetworkedAvatarHandsSetup] FAILED: " + reason);
    }
}
