using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class NetworkXRGrabSync : NetworkBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;

    private NetworkVariable<Vector3> netPos =
        new NetworkVariable<Vector3>(
            writePerm: NetworkVariableWritePermission.Owner);

    private NetworkVariable<Quaternion> netRot =
        new NetworkVariable<Quaternion>(
            writePerm: NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
    }

    public override void OnNetworkSpawn()
    {
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    public override void OnNetworkDespawn()
    {
        ActivePipeRegistry.ClearIf(gameObject); // don't leave a stale "active pipe" after despawn

        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        ActivePipeRegistry.Set(gameObject); // remember this pipe for the AR toolbar "Delete"

        if (!IsOwner)
            RequestOwnershipServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        // לא צריך לעשות כלום בשחרור
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (IsOwner)
        {
            netPos.Value = transform.position;
            netRot.Value = transform.rotation;
        }
        else
        {
            transform.position = netPos.Value;
            transform.rotation = netRot.Value;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestOwnershipServerRpc(ulong clientId)
    {
        GetComponent<NetworkObject>().ChangeOwnership(clientId);
    }
}
