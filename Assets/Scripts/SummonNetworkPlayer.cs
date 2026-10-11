using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class SummonNetworkPlayer : NetworkBehaviour
{
    // Static reference so the UI can find "My Player" easily
    public static SummonNetworkPlayer LocalInstance;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            LocalInstance = this;
            Debug.Log("[Player] Local Instance Registered.");
        }
    }

    // --- CALL THIS FROM UI MANAGER ---
    public void ExecuteSummon()
    {
        if (!IsOwner) return; // Double check

        ulong myID = OwnerClientId;
        Debug.Log($"[Player] Sending Summon RPC...");

        // Record for replay — no-op when no session is active.
        WaterVision.Recording.WorldEventRecorder.Record(
            new WaterVision.Recording.Events.SummonEvent
            {
                SummonerClientId = myID,
                TargetPosition = transform.position,
            });

        RequestSummonServerRpc(transform.position, myID);
    }

    [ServerRpc]
    void RequestSummonServerRpc(Vector3 location, ulong senderID)
    {
        ShowPopupClientRpc(location, senderID);
    }

    [ClientRpc]
    void ShowPopupClientRpc(Vector3 targetLocation, ulong inviterID)
    {
        if (IsOwner) return; // Don't invite myself

        if (SummonUIManager.Instance != null)
        {
            string name = "Player " + inviterID;
            SummonUIManager.Instance.OpenReceiverRequest(name, () => 
            {
                StartCoroutine(TeleportSequence(targetLocation));
            });
        }
    }

    IEnumerator TeleportSequence(Vector3 targetPos)
    {
        // Simple teleport logic
        transform.position = targetPos;
        yield return null;
    }
}