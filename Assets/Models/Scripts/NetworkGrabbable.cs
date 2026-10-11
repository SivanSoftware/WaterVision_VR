using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable))]
public class NetworkGrabbable : NetworkBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        interactable.selectEntered.AddListener(OnGrab);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        interactable.selectEntered.RemoveListener(OnGrab);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        // אם אני לא הבעלים, אני מבקש בעלות
        if (!IsOwner)
        {
            RequestOwnershipServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestOwnershipServerRpc(ServerRpcParams serverRpcParams = default)
    {
        ulong newOwnerId = serverRpcParams.Receive.SenderClientId;
        // העברת בעלות בשרת
        GetComponent<NetworkObject>().ChangeOwnership(newOwnerId);
    }
}