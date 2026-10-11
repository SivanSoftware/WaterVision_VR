using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

/// <summary>
/// Temporary keyboard + mouse camera control for testing without VR headset.
/// Disable this component when using a real headset or Meta XR Simulator.
/// 
/// Place this on the XR Origin (the parent of the Camera Offset).
/// </summary>
public class KeyboardCameraRig : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Main Camera under XR Origin > Camera Offset")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.0f;
    [SerializeField] private float fastMoveMultiplier = 3.0f;
    [SerializeField] private float verticalSpeed = 1.5f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private bool invertY = false;

    [Header("Behavior")]
    [Tooltip("Hold right mouse button to rotate camera")]
    [SerializeField] private bool requireRightMouseToLook = true;

    private float _pitch = 0f;   // up/down rotation (camera local X)
    private float _yaw = 0f;     // left/right rotation (rig Y)

    private TrackedPoseDriver _trackedPoseDriver;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform == null)
        {
            Debug.LogError("[KeyboardCameraRig] No camera assigned and no Camera.main found!");
            enabled = false;
            return;
        }

        // Disable TrackedPoseDriver so it doesn't override our manual control
        _trackedPoseDriver = cameraTransform.GetComponent<TrackedPoseDriver>();
        if (_trackedPoseDriver != null)
        {
            _trackedPoseDriver.enabled = false;
            Debug.Log("[KeyboardCameraRig] TrackedPoseDriver disabled for manual control.");
        }

        // Initialize yaw from current rotation
        _yaw = transform.eulerAngles.y;

        // Set a reasonable starting eye height if camera is at origin
        if (cameraTransform.localPosition == Vector3.zero)
            cameraTransform.localPosition = new Vector3(0, 1.6f, 0);
    }

    private void OnDisable()
    {
        // Re-enable TrackedPoseDriver if we disable this component (back to real VR)
        if (_trackedPoseDriver != null)
            _trackedPoseDriver.enabled = true;
    }

    private void Update()
    {
        HandleMovement();
        HandleLook();
    }

    private void HandleMovement()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector3 input = Vector3.zero;

        if (keyboard.wKey.isPressed) input.z += 1;
        if (keyboard.sKey.isPressed) input.z -= 1;
        if (keyboard.dKey.isPressed) input.x += 1;
        if (keyboard.aKey.isPressed) input.x -= 1;
        if (keyboard.spaceKey.isPressed) input.y += 1;
        if (keyboard.cKey.isPressed) input.y -= 1;

        float speed = moveSpeed;
        if (keyboard.leftShiftKey.isPressed)
            speed *= fastMoveMultiplier;

        // Move horizontally relative to camera yaw (so W goes where you look)
        Vector3 horizontal = transform.right * input.x + transform.forward * input.z;
        Vector3 vertical = Vector3.up * input.y * verticalSpeed;

        transform.position += (horizontal * speed + vertical) * Time.deltaTime;
    }

    private void HandleLook()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (requireRightMouseToLook && !mouse.rightButton.isPressed)
            return;

        Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity * 0.1f;

        _yaw += delta.x;
        _pitch += invertY ? delta.y : -delta.y;
        _pitch = Mathf.Clamp(_pitch, -89f, 89f);

        // Yaw on the rig (so movement direction follows look direction)
        transform.rotation = Quaternion.Euler(0, _yaw, 0);

        // Pitch on the camera only (head tilt)
        cameraTransform.localRotation = Quaternion.Euler(_pitch, 0, 0);
    }
}