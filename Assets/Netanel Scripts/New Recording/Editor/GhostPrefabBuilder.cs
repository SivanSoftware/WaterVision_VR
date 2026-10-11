#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WaterVision;
using WaterVision.Recording;

namespace WaterVision.Recording.EditorTools
{
    /// <summary>
    /// Builds one lightweight Ghost prefab per entry in <see cref="AvatarCatalog"/>. The source
    /// avatar prefabs are full networked players (NetworkObject + XRAvatarIK + XRHandPoseReplicator
    /// + NetworkPlayerPositionSync + a stack of RequireComponent chains). Stripping those at
    /// runtime is fragile — components disable renderers in Awake, RequireComponent chains error
    /// mid-strip, and offline-body scripts assume a NetworkManager singleton that offline replay
    /// doesn't have. Do the strip once, at edit time, and save the result as a dedicated
    /// Ghost_&lt;idx&gt;.prefab that replay just instantiates cleanly.
    ///
    /// Output: Assets/Resources/Ghosts/Ghost_&lt;index&gt;.prefab (Resources so ReplayManager can
    /// Resources.Load them by index without a serialized reference).
    /// </summary>
    public static class GhostPrefabBuilder
    {
        const string OUT_DIR = "Assets/Resources/Ghosts";

        [MenuItem("WaterVision/UI/Rebuild Ghost Prefab Variants")]
        public static void Rebuild()
        {
            var catalog = Resources.Load<AvatarCatalog>("AvatarCatalog");
            if (catalog == null)
            {
                Debug.LogError("[GhostPrefabBuilder] AvatarCatalog not found at Resources/AvatarCatalog. Aborting.");
                return;
            }
            if (catalog.entries == null || catalog.entries.Length == 0)
            {
                Debug.LogError("[GhostPrefabBuilder] AvatarCatalog has no entries. Aborting.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(OUT_DIR))
            {
                Directory.CreateDirectory(OUT_DIR);
                AssetDatabase.Refresh();
            }

            int ok = 0;
            for (int i = 0; i < catalog.entries.Length; i++)
            {
                var entry = catalog.entries[i];
                if (entry == null || entry.prefab == null)
                {
                    Debug.LogWarning($"[GhostPrefabBuilder] Skipping catalog index {i}: no source prefab.");
                    continue;
                }

                var srcPath = AssetDatabase.GetAssetPath(entry.prefab);
                var contents = PrefabUtility.LoadPrefabContents(srcPath);
                try
                {
                    StripToGhost(contents);
                    EnsureGhostController(contents);
                    ForceEnableSkinnedMeshRenderers(contents);

                    var outPath = $"{OUT_DIR}/Ghost_{i}.prefab";
                    PrefabUtility.SaveAsPrefabAsset(contents, outPath);
                    Debug.Log($"[GhostPrefabBuilder] Wrote '{outPath}' from '{entry.displayName}'.");
                    ok++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GhostPrefabBuilder] Done. Built {ok}/{catalog.entries.Length} ghost variants under {OUT_DIR}.");
        }

        // ORDER MATTERS: destroy dependents BEFORE dependencies so RequireComponent chains
        // don't refuse the removal ("Can't remove X because Y depends on it").
        static readonly string[] STRIP_BY_NAME = new[]
        {
            // Dependents (things that RequireComponent(XRINetworkPlayer))
            "PlayerNameTag",
            "XRAvatarVisuals", "XRAvatarVisuals_RPM",
            "XRAvatarIK", "XRAvatarIK_RPM",
            "XRHandPoseReplicator", "FingerPoseReplicator",
            "NetworkPlayerPositionSync",
            "AvatarHeadFollower", "AvatarRoomscaleAndYawFollower",
            // Base classes AFTER their dependents
            "XRINetworkPlayer", "XRINetworkPlayer_RPM",
        };

        static void StripToGhost(GameObject root)
        {
            // 1) Named types in dependency order. DestroyImmediate is safe at edit time and lets
            //    us honor RequireComponent chains — the deferred runtime Destroy() couldn't.
            foreach (var typeName in STRIP_BY_NAME)
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    if (mb.GetType().Name == typeName) Object.DestroyImmediate(mb, true);
                }
            }

            // 2) Any remaining NetworkBehaviour (subclasses we didn't name).
            foreach (var nb in root.GetComponentsInChildren<Unity.Netcode.NetworkBehaviour>(true))
                if (nb != null) Object.DestroyImmediate(nb, true);

            // 3) NetworkObject itself.
            foreach (var no in root.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true))
                if (no != null) Object.DestroyImmediate(no, true);
        }

        static void EnsureGhostController(GameObject root)
        {
            if (root.GetComponent<GhostController>() == null)
                root.AddComponent<GhostController>();
        }

        static void ForceEnableSkinnedMeshRenderers(GameObject root)
        {
            // Some source prefabs ship with body SMRs disabled — a network reveal script would
            // normally turn them on at runtime, but that script is stripped. Enable everything so
            // the body renders in offline replay. If the source has debug hand SMRs (stock XRI
            // purple hands) they'll show too, but the editorHeightOffset on GhostController lifts
            // the whole rig above the Editor camera's field of view so they never enter the shot.
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr != null) smr.enabled = true;
        }
    }
}
#endif
