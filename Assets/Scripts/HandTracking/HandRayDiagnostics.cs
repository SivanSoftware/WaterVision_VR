// HandRayDiagnostics — TEMPORARY instrumentation for the "hand ray originates from the feet" bug.
//
// Logs every layer of the ray stack every ~0.5s so we can see WHICH interactor/origin sits at the feet,
// instead of guessing. Auto-spawns at runtime (no scene edit needed). DELETE this file once the cause is found.
//
// What to read in the log (in hand-tracking, no controllers):
//   - modality.currentInputMode: TrackedHand (expected) vs MotionController (stuck).
//   - For each NearFarInteractor: activeInHierarchy + enabled + lineVisual ON/off + its world pos.
//       * CONTROLLER interactors cast from their OWN transform — if one is active + lineVisual ON + pos.y≈0
//         (feet) while you're hand-tracking, THAT is the feet-ray (a different interactor than the hand one).
//   - HandRayOriginFollower aim poses = the HAND ray origins — should be at hand height (y≈1.0-1.4).
//   - XRHandSubsystem palm (world) = the live hand truth; compare against the aim-pose y.

using System.Collections.Generic;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

public class HandRayDiagnostics : MonoBehaviour
{
    [SerializeField] private float m_Interval = 0.5f;
    private float m_NextTime;

    private static readonly List<XRHandSubsystem> s_Subs = new List<XRHandSubsystem>();
    private XRHandSubsystem m_Hands;
    private XROrigin m_Origin;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        var go = new GameObject("__HandRayDiagnostics");
        DontDestroyOnLoad(go);
        go.AddComponent<HandRayDiagnostics>();
    }

    private void Update()
    {
        if (Time.unscaledTime < m_NextTime) return;
        m_NextTime = Time.unscaledTime + m_Interval;
        Dump();
    }

    private void Dump()
    {
        var sb = new StringBuilder(640);
        sb.Append("[HandRayDiagnostics]\n");

        try { sb.Append($"  modality.currentInputMode = {XRInputModalityManager.currentInputMode.Value}\n"); }
        catch { sb.Append("  modality.currentInputMode = <unavailable>\n"); }

        var interactors = FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.Append($"  NearFarInteractors ({interactors.Length}):\n");
        foreach (var nf in interactors)
        {
            var lv = nf.GetComponent<XRInteractorLineVisual>();
            string lvState = lv != null ? (lv.enabled ? "lineVisual:ON" : "lineVisual:off") : "lineVisual:none";
            sb.Append($"    - '{Path(nf.transform)}' active={nf.gameObject.activeInHierarchy} enabled={nf.enabled} " +
                      $"{lvState} pos={Fmt(nf.transform.position)}\n");
        }

        var followers = FindObjectsByType<HandRayOriginFollower>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.Append($"  HandRayOriginFollower aim poses ({followers.Length}):\n");
        foreach (var f in followers)
            sb.Append($"    - '{Path(f.transform)}' active={f.gameObject.activeInHierarchy} enabled={f.enabled} " +
                      $"aimPos={Fmt(f.transform.position)}\n");

        sb.Append("  XRHandSubsystem palm (world):\n");
        sb.Append($"    - Left  {SubsystemPalm(true)}\n");
        sb.Append($"    - Right {SubsystemPalm(false)}\n");

        Debug.Log(sb.ToString(), this);
    }

    private string SubsystemPalm(bool left)
    {
        if (m_Hands == null || !m_Hands.running)
        {
            m_Hands = null;
            SubsystemManager.GetSubsystems(s_Subs);
            for (int i = 0; i < s_Subs.Count; i++)
                if (s_Subs[i] != null && s_Subs[i].running) { m_Hands = s_Subs[i]; break; }
            if (m_Hands == null) return "<no running XRHandSubsystem>";
        }

        XRHand hand = left ? m_Hands.leftHand : m_Hands.rightHand;
        if (!hand.isTracked) return "<not tracked>";
        if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose pose)) return "<no palm pose>";

        Transform track = Track();
        if (track == null) return "<no XROrigin>";

        Vector3 world = track.TransformPoint(pose.position);
        return $"world={Fmt(world)} (y={world.y:F2})";
    }

    private Transform Track()
    {
        if (m_Origin == null) m_Origin = FindFirstObjectByType<XROrigin>();
        if (m_Origin == null) return null;
        return m_Origin.CameraFloorOffsetObject != null ? m_Origin.CameraFloorOffsetObject.transform : m_Origin.transform;
    }

    private static string Fmt(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";

    private static string Path(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }
}
