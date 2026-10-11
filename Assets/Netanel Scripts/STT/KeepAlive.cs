using UnityEngine;

public class KeepAlive : MonoBehaviour
{
    private void Awake()
    {
        // This tells Unity: "Do not destroy me when the scene changes"
        DontDestroyOnLoad(gameObject);
    }
}