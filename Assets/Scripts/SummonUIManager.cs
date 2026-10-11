using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SummonUIManager : MonoBehaviour
{
    public static SummonUIManager Instance;

    [Header("--- 1. Summoner Confirmation ---")]
    public GameObject summonerConfirmCanvas;
    public Button btnConfirmSummon; // The "YES" button
    public Button btnCancelSummon;  // The "NO" button

    [Header("--- 2. Receiver Request ---")]
    public GameObject receiverRequestCanvas;
    public Button btnAcceptInvite;
    public Button btnDenyInvite;
    public TextMeshProUGUI txtInviterName;

    // We need to save the "Accept" action for the receiver side
    private Action _onReceiverAccepted;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // Hide canvases
        if(summonerConfirmCanvas) summonerConfirmCanvas.SetActive(false);
        if(receiverRequestCanvas) receiverRequestCanvas.SetActive(false);

        // --- SETUP CONFIRMATION (Local) ---
        // When clicking "YES", we find the Local Player and trigger the network call
        if(btnConfirmSummon) 
        {
            btnConfirmSummon.onClick.AddListener(() => {
                Debug.Log("[UI] User clicked YES.");
                
                if (SummonNetworkPlayer.LocalInstance != null)
                {
                    SummonNetworkPlayer.LocalInstance.ExecuteSummon(); // <--- CALLING THE PLAYER
                }
                else
                {
                    Debug.LogError("[UI] Error: Local Player not found!");
                }
                HideAll();
            });
        }

        if(btnCancelSummon) btnCancelSummon.onClick.AddListener(HideAll);

        // --- SETUP RECEIVER (Remote) ---
        if(btnAcceptInvite) 
        {
            btnAcceptInvite.onClick.AddListener(() => {
                _onReceiverAccepted?.Invoke(); 
                HideAll();
            });
        }

        if(btnDenyInvite) btnDenyInvite.onClick.AddListener(HideAll);
    }

    // --- LINK YOUR MENU BUTTON TO THIS FUNCTION ---
    public void OpenConfirmationPanel()
    {
        Debug.Log("[UI] Opening Confirmation Panel...");
        summonerConfirmCanvas.SetActive(true);
        receiverRequestCanvas.SetActive(false);
    }

    // Called by Network Player when an invite arrives
    public void OpenReceiverRequest(string inviterName, Action onAcceptCallback)
    {
        _onReceiverAccepted = onAcceptCallback;
        if(txtInviterName) txtInviterName.text = inviterName + " is summoning you!";
        
        receiverRequestCanvas.SetActive(true);
        summonerConfirmCanvas.SetActive(false);
    }

    public void HideAll()
    {
        summonerConfirmCanvas.SetActive(false);
        receiverRequestCanvas.SetActive(false);
    }
}