using Unity.Netcode;
using UnityEngine;


[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class NetworkGrabOwnership : NetworkBehaviour
{
    UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;
    NetworkObject netObj;

    void Awake()
    {
        grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        netObj = GetComponent<NetworkObject>();
    }

    public override void OnNetworkSpawn()
    {
        grab.selectEntered.AddListener(_ =>
        {
            if (!IsOwner)
            {
                netObj.ChangeOwnership(NetworkManager.LocalClientId);
            }
        });
    }
}
