// HandsWinControllerSuppressor — kills the "ray from the feet".
//
// In hand-tracking the visible feet-ray is the always-on CONTROLLER NearFarInteractor: it casts from its own
// UNDRIVEN transform parked at the rig root (~ the player's feet). The XRInputModalityManager does NOT deactivate
// the controllers, because Quest multimodal keeps idle controllers "tracked" — the same stuck behaviour
// AvatarModalityIKBinder already works around for the IK. (Confirmed by [HandDiag]: the hand wrist resolves fine,
// so the HAND ray is correct but hidden by SmartHandRayVisibility unless hovering; the controller's ungated line
// visual is all the user sees.)
//
// FIX (hands-win): each frame, when a hand is tracked (XRHandSubsystem), force that side's CONTROLLER GameObject
// — XRInputModalityManager.leftController / rightController, the very object the manager itself toggles — INACTIVE,
// so its NearFarInteractor + line visual stop and the feet-ray disappears. Restore it when the hand isn't tracked
// so picking a controller back up still works. Auto-spawns at runtime; no rig/scene edit needed.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

public class HandsWinControllerSuppressor : MonoBehaviour
{
    [SerializeField] private bool m_DebugLog = true;

    private static readonly List<XRHandSubsystem> s_Subs = new List<XRHandSubsystem>();
    private XRHandSubsystem m_Hands;
    private XRInputModalityManager m_Modality;
    private bool? m_LeftDesired;   // last applied controller-active state (null = not yet applied)
    private bool? m_RightDesired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        var go = new GameObject("__HandsWinControllerSuppressor");
        DontDestroyOnLoad(go);
        go.AddComponent<HandsWinControllerSuppressor>();
    }

    private void Update()
    {
        if (m_Modality == null)
        {
            m_Modality = FindFirstObjectByType<XRInputModalityManager>();
            if (m_Modality == null) return;
        }

        if (m_Hands == null || !m_Hands.running)
        {
            m_Hands = null;
            SubsystemManager.GetSubsystems(s_Subs);
            for (int i = 0; i < s_Subs.Count; i++)
                if (s_Subs[i] != null && s_Subs[i].running) { m_Hands = s_Subs[i]; break; }
        }

        bool subsysOk = m_Hands != null && m_Hands.running;
        bool leftHandTracked  = subsysOk && m_Hands.leftHand.isTracked;
        bool rightHandTracked = subsysOk && m_Hands.rightHand.isTracked;

        // Controller OFF while that hand is tracked (kills the feet-ray); ON otherwise (controllers usable).
        Apply(m_Modality.leftController,  !leftHandTracked,  ref m_LeftDesired,  "Left");
        Apply(m_Modality.rightController, !rightHandTracked, ref m_RightDesired, "Right");
    }

    private void Apply(GameObject controller, bool desiredActive, ref bool? lastApplied, string side)
    {
        if (controller == null) return;
        // Re-assert if our intent changed OR the modality manager drifted the state away from our intent.
        if (lastApplied == desiredActive && controller.activeSelf == desiredActive) return;
        controller.SetActive(desiredActive);
        lastApplied = desiredActive;
        if (m_DebugLog)
            Debug.Log($"[HandsWinControllerSuppressor] {side} controller -> " +
                      $"{(desiredActive ? "ON" : "OFF (hand tracked → feet-ray suppressed)")}.");
    }
}
