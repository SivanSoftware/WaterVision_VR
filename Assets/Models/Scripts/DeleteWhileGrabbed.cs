using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(NetworkDeletable))]
public class DeleteWhileGrabbed : MonoBehaviour
{
    public InputActionReference deleteAction;
    public float holdSeconds = 0.5f;

    XRGrabInteractable grab;
    NetworkDeletable deletable;

    float holdTimer;
    bool sent;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        deletable = GetComponent<NetworkDeletable>();
    }

    void OnEnable()
    {
        if (deleteAction != null) deleteAction.action.Enable();
    }

    void OnDisable()
    {
        if (deleteAction != null) deleteAction.action.Disable();
    }

    void Update()
    {
        if (deleteAction == null || grab == null || deletable == null)
            return;

        if (!deletable.IsOwner || !grab.isSelected)
        {
            holdTimer = 0f;
            sent = false;
            return;
        }

        bool pressed = deleteAction.action.ReadValue<float>() > 0.5f;

        if (!pressed)
        {
            holdTimer = 0f;
            sent = false;
            return;
        }

        if (!sent && holdTimer <= 0.01f)
            Debug.Log("DELETE press started while grabbed (owner)");

        if (sent) return;

        holdTimer += Time.deltaTime;
        if (holdTimer >= holdSeconds)
        {
            sent = true;
            deletable.RequestDeleteServerRpc();
            holdTimer = 0f;
        }
    }
}
