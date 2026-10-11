using UnityEngine;

public class VRPlayerHeightFix : MonoBehaviour
{
    public float playerHeight = 1.6f;
    public Transform cameraOffset;
    
    void Start()
    {
        if (cameraOffset == null)
        {
            cameraOffset = transform.Find("Camera Offset");
        }
    }
    
    void LateUpdate()
    {
        if (cameraOffset != null)
        {
            Vector3 pos = cameraOffset.localPosition;
            pos.y = playerHeight;
            cameraOffset.localPosition = pos;
        }
    }
}