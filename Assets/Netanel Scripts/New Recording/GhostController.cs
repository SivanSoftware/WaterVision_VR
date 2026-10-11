using UnityEngine;

namespace WaterVision.Recording
{
    /// <summary>
    /// Drives a Ghost prefab instance during replay: positions the root, snaps head to
    /// the recorded world pose, drives the hands via 2-bone IK targets (so shoulders +
    /// elbows articulate naturally), and tints the body to match the recorded participant's color.
    ///
    /// Phase 5 (this version): hands are no longer snapped directly; they're driven by
    /// <see cref="ASLProceduralArmIK"/> — Daniel Holden analytic 2-bone IK + forearm-roll
    /// wrist orientation — which writes shoulder/elbow/wrist rotations so the arm bends
    /// like a real arm. Runs in LateUpdate AFTER this script (DefaultExecutionOrder = -100).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GhostController : MonoBehaviour
    {
        private Transform headBone;
        private Renderer[] renderers;

        // IK rig (replaces the old direct hand-bone snapping).
        private ASLProceduralArmIK armIK;
        private Transform leftHandTarget;
        private Transform rightHandTarget;

        private Vector3 pendingHeadPos, pendingLPos, pendingRPos;
        private Quaternion pendingHeadRot, pendingLRot, pendingRRot;
        private bool hasPose;

        private bool initialized;

        [Tooltip("Vertical offset applied to the ghost only when running inside the Unity Editor " +
                 "so the desktop-simulator camera doesn't end up INSIDE the ghost body. Zero in " +
                 "player builds — VR headsets get the recorded position 1:1.")]
        public float editorHeightOffset = 2.0f;

        /// <summary>
        /// Called by ReplayManager right after Instantiate. Idempotent.
        /// </summary>
        public void Initialize()
        {
            if (initialized) return;

            var animator = GetComponentInChildren<Animator>(includeInactive: true);
            if (animator != null && animator.isHuman)
            {
                // Ensure Animator is enabled so its avatar bone map is queryable. The
                // Animator Controller is optional — bones are driven directly (head) or via IK (hands).
                animator.enabled = true;
                headBone = animator.GetBoneTransform(HumanBodyBones.Head);

                // Build the IK rig on the Animator's GameObject so its transform.rotation
                // (the body root for body-local pole computation) matches the avatar's body.
                SetupArmIK(animator);
            }

            renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            initialized = true;

            if (headBone == null)
                Debug.LogWarning($"[GhostController] No humanoid Head bone resolved on '{name}'. " +
                    "The ghost will stay in its bind pose during replay — verify the Ghost prefab's " +
                    "Animator has a Humanoid avatar configured.", this);
        }

        /// <summary>
        /// Add ASLProceduralArmIK to the avatar root if not already there, create child
        /// Transforms to act as hand IK targets, and wire them up. Idempotent — re-running
        /// on an already-rigged ghost is a no-op.
        /// </summary>
        private void SetupArmIK(Animator animator)
        {
            GameObject rigRoot = animator.gameObject;

            armIK = rigRoot.GetComponent<ASLProceduralArmIK>();
            if (armIK == null) armIK = rigRoot.AddComponent<ASLProceduralArmIK>();

            // Find or create the two hand targets as children of the rig root.
            leftHandTarget = FindOrCreateChild(rigRoot.transform, "Ghost_LeftHandTarget");
            rightHandTarget = FindOrCreateChild(rigRoot.transform, "Ghost_RightHandTarget");

            armIK.leftHandTarget = leftHandTarget;
            armIK.rightHandTarget = rightHandTarget;
            armIK.weight = 1f;
            armIK.driveHandRotation = true;
            armIK.debugLog = false;

            // Resolve the 6 arm bones from the Humanoid avatar map (or by name).
            armIK.ResolveBones();
        }

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null) return existing;
            var go = new GameObject(childName);
            go.transform.SetParent(parent, worldPositionStays: false);
            return go.transform;
        }

        public void ApplyPose(
            Vector3 headPos, Quaternion headRot,
            Vector3 lHandPos, Quaternion lHandRot,
            Vector3 rHandPos, Quaternion rHandRot)
        {
            if (!initialized) Initialize();

            // Editor lift: when running in the Editor (desktop simulator), the user's
            // desktop camera sits at the same world position where the recorded head was,
            // so without an offset the replay ghost ends up wrapped around the camera and
            // fills the whole view with body geometry. Lifting the whole ghost by ~2 m
            // pushes the body above head height — only the color-tinted name marker
            // remains visible. Zero in player builds so VR headsets get the raw pose.
            Vector3 lift = Vector3.zero;
#if UNITY_EDITOR
            lift.y = editorHeightOffset;
#endif

            // Root: stand the avatar's feet at the world floor under the recorded head.
            // (Per-recording floor offset will arrive in a later phase.)
            Vector3 rootPos = headPos;
            rootPos.y = 0f;
            rootPos += lift;
            transform.position = rootPos;

            // Rotate root to match head yaw — keeps the body facing the same direction
            // the head was looking.
            Vector3 fwd = headRot * Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

            // Apply the same lift to the head + hand targets so the whole rig stays
            // consistent (head bone snap in LateUpdate + IK hand targets).
            pendingHeadPos = headPos + lift; pendingHeadRot = headRot;
            pendingLPos = lHandPos + lift;   pendingLRot = lHandRot;
            pendingRPos = rHandPos + lift;   pendingRRot = rHandRot;
            hasPose = true;

            // Move IK targets immediately — ASLProceduralArmIK reads their world pose
            // each LateUpdate to solve shoulder/elbow/wrist rotations.
            if (leftHandTarget != null)  { leftHandTarget.position = pendingLPos;   leftHandTarget.rotation = lHandRot; }
            if (rightHandTarget != null) { rightHandTarget.position = pendingRPos;  rightHandTarget.rotation = rHandRot; }
        }

        // Head is snapped in LateUpdate so the Animator (if any) can't overwrite us in the same
        // frame. Hands are NOT snapped here — they're driven by ASLProceduralArmIK's own LateUpdate
        // (which runs AFTER this one because of [DefaultExecutionOrder(-100)] on this script).
        private void LateUpdate()
        {
            if (!hasPose) return;
            if (headBone != null) { headBone.position = pendingHeadPos; headBone.rotation = pendingHeadRot; }
        }

        public void SetVisible(bool visible)
        {
            if (!initialized) Initialize();
            if (renderers == null) return;
            foreach (var r in renderers) if (r != null) r.enabled = visible;
        }

        public void SetTint(Color color)
        {
            if (!initialized) Initialize();
            if (renderers == null) return;
            // Use sharedMaterial copy via .material (Unity instantiates on access) so
            // each ghost gets its own tint without affecting the source asset.
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mat = r.material;
                if (mat == null) continue;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            }
        }

        /// <summary>
        /// Replace every renderer's material(s) with the translucent "ghost" material and tint it
        /// by <paramref name="tint"/> at ~0.8 alpha, so the ghost reads as a see-through recording
        /// rather than a live user. Uses the renderer's instanced material (r.material/r.materials)
        /// so Unity frees the instance when the ghost GameObject is destroyed (no leak), and the
        /// source asset is never modified. Falls back to a plain tint if no material is supplied.
        /// </summary>
        public void ApplyGhostMaterial(Material baseMat, Color tint)
        {
            if (!initialized) Initialize();
            if (renderers == null) return;
            if (baseMat == null) { SetTint(tint); return; }

            tint.a = 0.8f;

            foreach (var r in renderers)
            {
                if (r == null) continue;

                // Assign the ghost material as SHARED first (no clone), THEN read .material /
                // .materials — reading a shared material is Unity's documented clone path: it
                // makes a per-renderer instance the renderer OWNS (freed when the ghost is
                // destroyed -> no leak) without ever mutating the source asset. We then tint
                // that owned instance. (Avoids the ambiguous "set .material then get" behavior.)
                int slots = r.sharedMaterials != null ? r.sharedMaterials.Length : 0;
                if (slots <= 1)
                {
                    r.sharedMaterial = baseMat;
                    ApplyColor(r.material, tint);
                }
                else
                {
                    var shared = new Material[slots];
                    for (int i = 0; i < slots; i++) shared[i] = baseMat;
                    r.sharedMaterials = shared;
                    var insts = r.materials;       // clones each slot into renderer-owned instances
                    for (int i = 0; i < insts.Length; i++) ApplyColor(insts[i], tint);
                }

                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static void ApplyColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
    }
}
