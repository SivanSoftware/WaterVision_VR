using UnityEngine;
using Unity.Netcode;

/// <summary>
/// This script manages the 'Menu' button visibility on the player's hand.
/// It ensures the button only appears when connected to a session.
/// </summary>
public class MenuController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The button object on the controller's hand.")]
    [SerializeField] private GameObject menuButton;
    
    [Tooltip("The VR_MainMenu prefab to spawn.")]
    [SerializeField] private GameObject menuPrefab;

    [Header("Menu Positioning")]
    [Tooltip("How far in front of the player the menu appears (meters).")]
    [SerializeField] private float menuDistance = 1.5f;
    
    [Tooltip("Vertical offset from camera height (negative = lower).")]
    [SerializeField] private float menuHeightOffset = -0.2f;

    private GameObject _instantiatedMenu;

    void Update()
    {
        bool hasInternet = Application.internetReachability != NetworkReachability.NotReachable;
        bool isConnectedToSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;

        if (menuButton != null)
        {
            menuButton.SetActive(hasInternet && isConnectedToSession);
        }
    }

    public void ToggleMenu()
    {
        if (_instantiatedMenu == null)
        {
            _instantiatedMenu = Instantiate(menuPrefab);
            PositionMenuInFrontOfPlayer();
            _instantiatedMenu.SetActive(true);
            Debug.Log($"[MenuController] Created menu at: {_instantiatedMenu.transform.position}");
        }
        else
        {
            bool nextState = !_instantiatedMenu.activeSelf;
            if (nextState)
            {
                // Re-position every time we re-open (in case player moved)
                PositionMenuInFrontOfPlayer();
            }
            _instantiatedMenu.SetActive(nextState);
        }
    }

    /// <summary>
    /// Positions the menu in front of the player's camera and rotates it to face them.
    /// </summary>
    private void PositionMenuInFrontOfPlayer()
    {
        if (_instantiatedMenu == null) return;

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[MenuController] Main Camera not found! Tag your VR camera as 'MainCamera'.");
            return;
        }

        // Get camera's forward direction, but flatten it on Y axis so menu stays upright
        Vector3 forward = mainCam.transform.forward;
        forward.y = 0;
        forward.Normalize();

        // Position the menu in front of the camera
        Vector3 targetPosition = mainCam.transform.position + forward * menuDistance;
        targetPosition.y += menuHeightOffset;
        _instantiatedMenu.transform.position = targetPosition;

        // Rotate the menu to face the camera
        _instantiatedMenu.transform.rotation = Quaternion.LookRotation(forward);
    }
}