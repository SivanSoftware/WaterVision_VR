using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simulates left and right hand controllers using mouse and keyboard.
/// Creates two visual cubes that represent hands floating in front of the camera.
/// 
/// Controls:
///   - Hold Q: control LEFT hand
///   - Hold E: control RIGHT hand
///   - Mouse: move active hand
///   - Mouse wheel: push hand forward/backward
///   - Left mouse: simulated trigger
///   - Right mouse: simulated grip
/// 
/// NOTE: These hands are NOT connected to the Meta Avatar - they're just visual
/// representations for testing interactions and grabbing in your scene.
/// </summary>
public class MouseHandSimulator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera the hands follow. Defaults to Camera.main.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Hand Setup")]
    [SerializeField] private Vector3 leftHandOffset = new Vector3(-0.25f, -0.3f, 0.5f);
    [SerializeField] private Vector3 rightHandOffset = new Vector3(0.25f, -0.3f, 0.5f);
    [SerializeField] private float handSize = 0.08f;

    [Header("Control")]
    [SerializeField] private float mouseSensitivity = 0.005f;
    [SerializeField] private float scrollSensitivity = 0.05f;

    [Header("Visual Feedback")]
    [SerializeField] private Color leftHandColor = new Color(0.3f, 0.6f, 1f);
    [SerializeField] private Color rightHandColor = new Color(1f, 0.5f, 0.3f);
    [SerializeField] private Color triggerColor = Color.green;
    [SerializeField] private Color gripColor = Color.yellow;

    // Public so other scripts can read state (e.g. interaction system)
    public Transform LeftHand { get; private set; }
    public Transform RightHand { get; private set; }
    public bool LeftTriggerPressed { get; private set; }
    public bool RightTriggerPressed { get; private set; }
    public bool LeftGripPressed { get; private set; }
    public bool RightGripPressed { get; private set; }

    private Vector3 _leftLocalOffset;
    private Vector3 _rightLocalOffset;

    private Renderer _leftRenderer;
    private Renderer _rightRenderer;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform == null)
        {
            Debug.LogError("[MouseHandSimulator] No camera found!");
            enabled = false;
            return;
        }

        _leftLocalOffset = leftHandOffset;
        _rightLocalOffset = rightHandOffset;

        LeftHand = CreateHand("SimulatedLeftHand", leftHandColor, out _leftRenderer);
        RightHand = CreateHand("SimulatedRightHand", rightHandColor, out _rightRenderer);
    }

    private Transform CreateHand(string name, Color color, out Renderer rend)
    {
        GameObject hand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hand.name = name;
        hand.transform.localScale = Vector3.one * handSize;

        // Remove collider so it doesn't physically interact with the world unintentionally
        Collider col = hand.GetComponent<Collider>();
        if (col != null) Destroy(col);

        rend = hand.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        rend.material.color = color;

        return hand.transform;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        bool controllingLeft = keyboard.qKey.isPressed;
        bool controllingRight = keyboard.eKey.isPressed;

        // Move the active hand with mouse
        if (controllingLeft || controllingRight)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue() * mouseSensitivity;
            float scroll = mouse.scroll.ReadValue().y * scrollSensitivity;

            Vector3 deltaOffset = new Vector3(mouseDelta.x, mouseDelta.y, scroll);

            if (controllingLeft) _leftLocalOffset += deltaOffset;
            if (controllingRight) _rightLocalOffset += deltaOffset;
        }

        // Read trigger/grip - applies to whichever hand is being controlled
        // If neither is held, both hands' inputs read as released
        LeftTriggerPressed = controllingLeft && mouse.leftButton.isPressed;
        RightTriggerPressed = controllingRight && mouse.leftButton.isPressed;
        LeftGripPressed = controllingLeft && mouse.rightButton.isPressed;
        RightGripPressed = controllingRight && mouse.rightButton.isPressed;

        // Position hands relative to camera
        UpdateHandTransform(LeftHand, _leftLocalOffset);
        UpdateHandTransform(RightHand, _rightLocalOffset);

        // Visual feedback for inputs
        UpdateHandColor(_leftRenderer, leftHandColor, LeftTriggerPressed, LeftGripPressed);
        UpdateHandColor(_rightRenderer, rightHandColor, RightTriggerPressed, RightGripPressed);
    }

    private void UpdateHandTransform(Transform hand, Vector3 localOffset)
    {
        // Hand position is camera position + offset rotated by camera
        hand.position = cameraTransform.position
            + cameraTransform.right * localOffset.x
            + cameraTransform.up * localOffset.y
            + cameraTransform.forward * localOffset.z;

        // Hand rotation matches camera (so it always faces forward)
        hand.rotation = cameraTransform.rotation;
    }

    private void UpdateHandColor(Renderer rend, Color baseColor, bool trigger, bool grip)
    {
        if (trigger) rend.material.color = triggerColor;
        else if (grip) rend.material.color = gripColor;
        else rend.material.color = baseColor;
    }

    private void OnDisable()
    {
        if (LeftHand != null) LeftHand.gameObject.SetActive(false);
        if (RightHand != null) RightHand.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (LeftHand != null) LeftHand.gameObject.SetActive(true);
        if (RightHand != null) RightHand.gameObject.SetActive(true);
    }
}