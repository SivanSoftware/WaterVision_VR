using Unity.Netcode;
using UnityEngine;
using XRMultiplayer;

namespace WaterVision
{
    /// <summary>
    /// Makes each client spawn the avatar it picked in the lobby.
    ///
    /// Under Netcode Distributed Authority, connection-approval (and its per-client
    /// PlayerPrefabHash override) is NOT supported. The DA-native mechanism is
    /// <see cref="NetworkManager.OnFetchLocalPlayerPrefabToSpawn"/> — a delegate NGO calls to
    /// fetch the LOCAL client's player prefab when auto-spawning it. We return the prefab that
    /// matches <see cref="XRINetworkGameManager.LocalPlayerAvatarIndex"/> from the AvatarCatalog;
    /// NGO then auto-spawns it and replicates it to everyone by GlobalObjectIdHash (so every
    /// variant must be registered in a NetworkPrefabsList, and must have a UNIQUE hash).
    ///
    /// This lives in Assembly-CSharp (not the stock VRMP assembly) so it can reference both the
    /// AvatarCatalog and XRINetworkGameManager. Drop one instance in the scene. Purely additive:
    /// remove it (and the delegate is cleared) to revert to the default PlayerPrefab spawn.
    ///
    /// Requires NetworkManager's AutoSpawnPlayerPrefabClientSide = true (already on in this project).
    /// </summary>
    public class AvatarSpawnController : MonoBehaviour
    {
        [Tooltip("Optional. If left empty, loads Resources/AvatarCatalog.")]
        [SerializeField] AvatarCatalog m_Catalog;

        [Tooltip("Log which prefab is chosen each time NGO asks (helpful for the first 2-client test).")]
        [SerializeField] bool m_DebugLog = true;

        [Tooltip("TEST OVERRIDE — remove before shipping. -1 = use the lobby selection " +
                 "(LocalPlayerAvatarIndex); 0..5 = always spawn that catalog index. Lets us verify " +
                 "the spawn mechanism before the picker UI exists.")]
        [SerializeField] int m_DebugForceIndex = -1;

        void Awake()
        {
            if (m_Catalog == null)
                m_Catalog = Resources.Load<AvatarCatalog>("AvatarCatalog");
        }

        // Assign in Start (not Awake/OnEnable) so NetworkManager.Singleton is guaranteed set
        // regardless of script execution order. The local player only spawns after the session
        // connects (well after Start), so the delegate is always in place in time.
        void Start()
        {
            Debug.Log($"[NPPS-DIAG] AvatarSpawnController.Start — catalog={(m_Catalog != null ? $"OK count={m_Catalog.Count}" : "NULL")} " +
                      $"NM={(NetworkManager.Singleton != null ? "OK" : "NULL")} " +
                      $"LocalPlayerAvatarIndex={XRINetworkGameManager.LocalPlayerAvatarIndex.Value}");
            if (NetworkManager.Singleton == null)
            {
                Debug.LogWarning("[AvatarSpawnController] No NetworkManager.Singleton at Start — avatar selection inactive.");
                return;
            }
            RegisterVariantPrefabs();
            NetworkManager.Singleton.OnFetchLocalPlayerPrefabToSpawn = FetchLocalPlayerPrefab;
        }

        // Register the variant prefabs at RUNTIME (before the session starts) so every client can
        // instantiate any chosen avatar by hash. Done in code instead of a NetworkPrefabsList asset /
        // scene NetworkConfig override — that override serialized under a conflicting property path
        // (NetworkConfig vs m_NetworkConfig) and never actually registered the variants.
        void RegisterVariantPrefabs()
        {
            if (m_Catalog == null) return;
            var nm = NetworkManager.Singleton;
            var basePrefab = nm.NetworkConfig != null ? nm.NetworkConfig.PlayerPrefab : null;
            int added = 0;
            for (int i = 0; i < m_Catalog.Count; i++)
            {
                var prefab = m_Catalog.GetPrefab(i);
                if (prefab == null || prefab == basePrefab) continue; // base is already the PlayerPrefab
                try { nm.AddNetworkPrefab(prefab); added++; }
                catch (System.Exception e)
                {
                    if (m_DebugLog) Debug.LogWarning($"[AvatarSpawnController] AddNetworkPrefab({prefab.name}) skipped: {e.Message}");
                }
            }
            if (m_DebugLog) Debug.Log($"[AvatarSpawnController] Registered {added} variant prefab(s) at runtime.");
        }

        void OnDestroy()
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.OnFetchLocalPlayerPrefabToSpawn == FetchLocalPlayerPrefab)
                NetworkManager.Singleton.OnFetchLocalPlayerPrefabToSpawn = null;
        }

        GameObject FetchLocalPlayerPrefab()
        {
            // Fallback to the NetworkManager-configured PlayerPrefab (the base Ch08) on any problem,
            // so a missing catalog can never break the spawn.
            GameObject fallback = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.NetworkConfig.PlayerPrefab
                : null;

            if (m_Catalog == null || m_Catalog.Count == 0)
            {
                Debug.Log($"[NPPS-DIAG] FetchLocalPlayerPrefab — NO CATALOG, returning fallback={(fallback != null ? fallback.name : "NULL")}");
                return fallback;
            }

            int requested = m_DebugForceIndex >= 0 ? m_DebugForceIndex : XRINetworkGameManager.LocalPlayerAvatarIndex.Value;
            int idx = Mathf.Clamp(requested, 0, m_Catalog.Count - 1);
            GameObject prefab = m_Catalog.GetPrefab(idx);

            Debug.Log($"[NPPS-DIAG] FetchLocalPlayerPrefab — requested={requested} idx={idx} " +
                      $"prefab={(prefab != null ? prefab.name : "null→fallback")} " +
                      $"(catalog name @idx='{m_Catalog.GetName(idx)}')");

            return prefab != null ? prefab : fallback;
        }
    }
}
