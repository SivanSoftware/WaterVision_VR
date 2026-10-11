using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class GatherManager : NetworkBehaviour
{
    [Header("UI References")]
    public GameObject popupPanel;

    private Vector3 targetLocation;
    private UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider teleportProvider;

    void Start()
    {
        teleportProvider = FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>();
        if (popupPanel != null) popupPanel.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log(">>> NETWORK: GatheringSystem connected successfully! ID: " + NetworkObjectId);
    }

    [ContextMenu("Run Test Now")]
    public void CallEveryone()
    {

        Vector3 myPos = Camera.main.transform.position;
        myPos.y = 0;

        // Record for replay — no-op when no session is active.
        ulong callerId = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClientId
            : 0UL;
        WaterVision.Recording.WorldEventRecorder.Record(
            new WaterVision.Recording.Events.GatherEvent
            {
                CallerClientId = callerId,
                TargetPosition = myPos,
            });

        if (!IsSpawned)
        {
            Debug.LogError(">>> FORCE OPEN: Running local logic directly!");
            OpenPanelLocal(myPos);
            return;
        }
        StartGatheringServerRpc(myPos, NetworkManager.Singleton.LocalClientId);
    }


    [ServerRpc(RequireOwnership = false)]
    void StartGatheringServerRpc(Vector3 hostPos, ulong senderId)
    {
        ShowPopupClientRpc(hostPos, senderId);
    }


    [ClientRpc]
    void ShowPopupClientRpc(Vector3 hostPos, ulong senderId)
    {
        Debug.Log("1. Received message from server!");

        // Important: Keep this line commented out for testing so you can invite yourself!
        // if (NetworkManager.Singleton.LocalClientId == senderId) return;

        Debug.Log("2. Passed filter, trying to show panel...");

        targetLocation = hostPos;
        PositionPanelInFrontOfPlayer();

        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
            Debug.Log("3. Panel should be ACTIVE now!");
        }
        else
        {
            Debug.LogError("ERROR: Popup Panel is NULL! Check Inspector.");
        }
    }
    void OpenPanelLocal(Vector3 hostPos)
    {
        Debug.Log(">>> ACTION: Trying to open panel...");

        targetLocation = hostPos;
        PositionPanelInFrontOfPlayer();

        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
            Debug.Log(">>> SUCCESS: Panel is active! (Look at the screen)");
        }
        else
        {
            Debug.LogError(">>> ERROR: popupPanel is NULL inside the script!");
        }
    }

    void PositionPanelInFrontOfPlayer()
    {
        Transform cameraTransform = Camera.main.transform;


        Vector3 newPos = cameraTransform.position + (cameraTransform.forward * 1.5f);

        newPos.y = cameraTransform.position.y;

        popupPanel.transform.position = newPos;


        popupPanel.transform.LookAt(popupPanel.transform.position + cameraTransform.forward);
    }


    public void OnPressYes()
    {
        popupPanel.SetActive(false);


        if (teleportProvider != null)
        {
            UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportRequest request = new UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportRequest()
            {
                destinationPosition = targetLocation,
                destinationRotation = Quaternion.identity,
                matchOrientation = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.MatchOrientation.TargetUpAndForward
            };
            teleportProvider.QueueTeleportRequest(request);
        }
        else
        {

            Transform playerRig = Camera.main.transform.root;
            playerRig.position = targetLocation;
        }
    }
    private void Update()
    {

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Debug.Log("SPACEBAR pressed (New Input System)! Calling everyone...");
            CallEveryone();
        }
    }
    public void OnPressNo()
    {
        popupPanel.SetActive(false);
    }
}