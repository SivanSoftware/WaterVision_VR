using Unity.Netcode;
using UnityEngine;

public class QuickNetTest : MonoBehaviour
{
    void Start()
    {
        NetworkManager.Singleton.StartHost();
    }
}
