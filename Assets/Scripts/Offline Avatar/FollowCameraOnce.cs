using UnityEngine;

public class FollowMainCamera : MonoBehaviour
{
    [SerializeField] Transform cameraTransform;
    [SerializeField] Vector3 positionOffset = new Vector3(0f, -1.7f, 0f);

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        transform.position = cameraTransform.position + positionOffset;
        transform.rotation = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
    }
}
