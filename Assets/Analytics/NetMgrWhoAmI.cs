using Unity.Netcode;
using UnityEngine;

public class NetMgrWhoAmI : MonoBehaviour
{
    void Start()
    {
        Debug.Log($"[NET] Singleton = {(NetworkManager.Singleton ? NetworkManager.Singleton.gameObject.name : "NULL")}");
    }
}
