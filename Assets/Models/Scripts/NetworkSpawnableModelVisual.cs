using Unity.Netcode;
using UnityEngine;

public class NetworkSpawnableModelVisual : NetworkBehaviour
{
    [Header("Available Models (PREFABS!)")]
    [Tooltip("Assign full model prefabs here (PipeSet_1, PipeSet_2, ...)")]
    public GameObject[] availableModels;

    [Header("Visual Target (THIS object shows the model)")]
    public MeshFilter visualMeshFilter;
    public MeshRenderer visualRenderer;

    private NetworkVariable<int> modelIndex =
        new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public override void OnNetworkSpawn()
    {
        modelIndex.OnValueChanged += OnModelIndexChanged;

        Debug.Log($"OnNetworkSpawn | modelIndex = {modelIndex.Value}");

        // 🔥 חובה — תמיד להחיל את המודל הקיים
        ApplyModel(modelIndex.Value);
    }


    public override void OnNetworkDespawn()
    {
        modelIndex.OnValueChanged -= OnModelIndexChanged;
    }

    private void OnModelIndexChanged(int oldIndex, int newIndex)
    {
        ApplyModel(newIndex);
    }

    private void ApplyModel(int index)
    {
        Debug.Log($"ApplyModel CALLED with index {index}");
        
        if (index < 0 || index >= availableModels.Length)
        {
            Debug.LogWarning($"Invalid model index: {index}");
            return;
        }

        GameObject sourcePrefab = availableModels[index];

        if (sourcePrefab == null)
        {
            Debug.LogError($"Model prefab at index {index} is NULL");
            return;
        }

        MeshFilter sourceMeshFilter =
            sourcePrefab.GetComponentInChildren<MeshFilter>();

        MeshRenderer sourceRenderer =
            sourcePrefab.GetComponentInChildren<MeshRenderer>();

        if (sourceMeshFilter == null || sourceRenderer == null)
        {
            Debug.LogError(
                $"Prefab '{sourcePrefab.name}' missing MeshFilter or MeshRenderer"
            );
            return;
        }

        visualMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
        visualRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

        visualRenderer.enabled = true;
        visualMeshFilter.gameObject.SetActive(true);

        Debug.Log($"Model '{sourcePrefab.name}' applied successfully");
    }

    // CALLED ONLY BY SERVER
    [ServerRpc(RequireOwnership = false)]
    public void SetModelServerRpc(int index)
    {
        modelIndex.Value = index;
    }

    // CALLED ONLY ON SERVER, BEFORE Spawn()
    public void SetInitialModel(int index)
    {
        modelIndex.Value = index;
    }

    // ------------------------------------------------------------------
    // Networked deletion (Trash bin) — called locally by PipeTrashBin; the
    // method routes to ServerRpc on non-host clients so the despawn always
    // executes on the authoritative server and propagates to every client.
    // ------------------------------------------------------------------
    public void RequestDespawn()
    {
        if (NetworkObject == null || !NetworkObject.IsSpawned)
        {
            Debug.LogWarning($"[NetworkSpawnableModelVisual] RequestDespawn called on un-spawned/null NetworkObject '{name}'. Skipping.", this);
            return;
        }

        var mgr = NetworkManager.Singleton;
        bool isOwner  = IsOwner;
        bool isServer = mgr != null && mgr.IsServer;
        bool isHost   = mgr != null && mgr.IsHost;
        Debug.Log($"[NetworkSpawnableModelVisual] RequestDespawn '{name}' (id={NetworkObjectId}) — IsOwner={isOwner}, IsServer={isServer}, IsHost={isHost}.", this);

        // In Distributed Authority, the OWNER (whoever grabbed the pipe last) typically
        // has authority to despawn. Try that first. Fall back to ServerRpc → host.
        if (isOwner || isServer || isHost)
        {
            DoDespawn();
        }
        else
        {
            DespawnServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void DespawnServerRpc()
    {
        Debug.Log($"[NetworkSpawnableModelVisual] DespawnServerRpc executed on server for '{name}' (id={NetworkObjectId}).", this);
        DoDespawn();
    }

    private void DoDespawn()
    {
        if (NetworkObject == null || !NetworkObject.IsSpawned) return;
        try
        {
            NetworkObject.Despawn(destroy: true);
            Debug.Log($"[NetworkSpawnableModelVisual] Despawn dispatched for id={NetworkObjectId}.", this);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NetworkSpawnableModelVisual] Despawn threw: {ex.Message}\n{ex}", this);
        }
    }

}
