using Unity.Netcode;
using UnityEngine;

public class NetworkModelSpawner : MonoBehaviour
{
    public NetworkObject networkSpawnableModelPrefab;
    public Transform defaultSpawnPoint;
    public Transform playerCamera;
    public float spawnDistance = 1.3f;

    [Header("Spawn Height")]
    [Tooltip("Spawn height offset below the camera (typical chest level)")]
    public float spawnHeightOffset = -0.5f;

    [Tooltip("Use defaultSpawnPoint Y if available")]
    public bool useFixedHeight = false;

    private NetworkManager NetworkManager => NetworkManager.Singleton;
    private bool IsHost => NetworkManager != null && NetworkManager.IsHost;

    void Start()
    {
        // Auto-find camera if not assigned
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
            Debug.Log($"[NetworkModelSpawner] Auto-found camera: {playerCamera.name}");
        }
    }

    public void RequestSpawn(int modelIndex)
    {
        Debug.Log($"RequestSpawn called. IsHost={IsHost}, index={modelIndex}");

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        if (playerCamera == null)
        {
            Debug.LogError("[NetworkModelSpawner] No camera found!");
            return;
        }

        // Position in front of camera, at chest height (not eye level)
        Vector3 forward = playerCamera.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 pos = playerCamera.position + forward * spawnDistance;

        // Adjust height
        if (useFixedHeight && defaultSpawnPoint != null)
        {
            pos.y = defaultSpawnPoint.position.y;
        }
        else
        {
            pos.y = playerCamera.position.y + spawnHeightOffset;
        }

        if (AnalyticsManager.Instance != null)
        {
            string objectName = $"Pipe_Index_{modelIndex}";
            AnalyticsManager.Instance.LogEvent(
                "Summon", objectName, pos, "Player requested spawn"
            );
        }

        if (IsHost)
        {
            Spawn(modelIndex, NetworkManager.LocalClientId, pos, forward);
        }
        else
        {
            RequestSpawnServerRpc(modelIndex, pos, forward);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnServerRpc(
        int modelIndex,
        Vector3 spawnPos,
        Vector3 forward,
        ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        Spawn(modelIndex, senderClientId, spawnPos, forward);
    }

    private void Spawn(
        int modelIndex,
        ulong requestingClientId,
        Vector3 spawnPos,
        Vector3 forward)
    {
        Debug.Log($"Spawning model {modelIndex} for client {requestingClientId}");

        Quaternion rot = Quaternion.LookRotation(forward);

        var obj = Instantiate(
            networkSpawnableModelPrefab,
            spawnPos,
            rot
        );

        var visual = obj.GetComponent<NetworkSpawnableModelVisual>();
        visual.SetInitialModel(modelIndex);

        obj.Spawn();
    }
}