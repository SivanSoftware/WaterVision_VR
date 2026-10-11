using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Controls a door based on network connection state.
/// When connected → door moves to OPEN position.
/// When disconnected → door returns to CLOSED position.
/// </summary>
public class DoorNetworkController : MonoBehaviour
{
    [Header("Door Transform")]
    [Tooltip("Door object (usually the door mesh or pivot)")]
    public Transform door;

    [Header("Closed State (No Network)")]
    public Vector3 closedPosition;
    public Vector3 closedRotation;

    [Header("Open State (Network Active)")]
    public Vector3 openPosition;
    public Vector3 openRotation;

    [Header("Movement")]
    [Tooltip("How fast the door moves")]
    public float moveSpeed = 3f;

    private Quaternion closedRot;
    private Quaternion openRot;

    void Start()
    {
        if (door == null)
            door = transform;

        closedRot = Quaternion.Euler(closedRotation);
        openRot   = Quaternion.Euler(openRotation);

        // Start in closed state by default
        door.localPosition = closedPosition;
        door.localRotation = closedRot;
    }

    void Update()
    {
        if (IsNetworkActive())
        {
            MoveToOpen();
        }
        else
        {
            MoveToClosed();
        }
    }

    /// <summary>
    /// Returns true if client or host is connected to a network session
    /// </summary>
    bool IsNetworkActive()
    {
        if (NetworkManager.Singleton == null) return false;
        // ✅ Check both connected AND a lobby is actually joined
        return NetworkManager.Singleton.IsConnectedClient || 
            (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.ConnectedClients.Count > 0);
    }

    void MoveToOpen()
    {
        door.localPosition = Vector3.Lerp(
            door.localPosition,
            openPosition,
            Time.deltaTime * moveSpeed
        );

        door.localRotation = Quaternion.Lerp(
            door.localRotation,
            openRot,
            Time.deltaTime * moveSpeed
        );
    }

    void MoveToClosed()
    {
        door.localPosition = Vector3.Lerp(
            door.localPosition,
            closedPosition,
            Time.deltaTime * moveSpeed
        );

        door.localRotation = Quaternion.Lerp(
            door.localRotation,
            closedRot,
            Time.deltaTime * moveSpeed
        );
    }


}
