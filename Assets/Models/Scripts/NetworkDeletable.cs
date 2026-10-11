using Unity.Netcode;
using UnityEngine;

public class NetworkDeletable : NetworkBehaviour
{
    [ServerRpc(RequireOwnership = false)]
    public void RequestDeleteServerRpc(ServerRpcParams rpcParams = default)
    {
        if (NetworkObject == null || !NetworkObject.IsSpawned)
            return;

        ulong sender = rpcParams.Receive.SenderClientId;

        // הרשאה: מי שהוא ה-Owner כרגע (המחזיק), או ה-Server
        if (sender != NetworkObject.OwnerClientId && sender != NetworkManager.ServerClientId)
            return;

        NetworkObject.Despawn(true);
    }
}
