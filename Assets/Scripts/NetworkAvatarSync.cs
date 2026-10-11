using UnityEngine;

public class NetworkAvatarSync : MonoBehaviour
{
    [Header("Avatar Bones")]
    public Transform avatarHead;
    public Transform avatarLeftHand;
    public Transform avatarRightHand;

    [Header("Networked Targets")]
    public Transform netHead;
    public Transform netLeftHand;
    public Transform netRightHand;

    void LateUpdate()
    {
        avatarHead.position = netHead.position;
        avatarHead.rotation = netHead.rotation;

        avatarLeftHand.position = netLeftHand.position;
        avatarLeftHand.rotation = netLeftHand.rotation;

        avatarRightHand.position = netRightHand.position;
        avatarRightHand.rotation = netRightHand.rotation;
    }
}
