using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace XRMultiplayer
{
    /// <summary>
    /// Networked singleton for the Beit Shean wastewater plant simulation.
    /// Owns the 5 stations, exposes ActivateStation / ActivateAll / StopAll,
    /// and syncs the active-station index so late joiners see the current state.
    /// Mirror of the WaterProcessSim coroutine pattern, adapted for multiplayer.
    /// </summary>
    public class WastewaterPlantManager : NetworkBehaviour
    {
        public static WastewaterPlantManager Instance { get; private set; }

        [Header("Stations (Stage A → E)")]
        public List<WastewaterStation> stations = new List<WastewaterStation>();

        [Header("Player Rig")]
        [Tooltip("Optional. If null, resolved via Camera.main.transform.root each activation.")]
        [SerializeField] private GameObject playerRig;

        [Header("Sequence Options")]
        [Tooltip("Between-station pause when running ActivateAll.")]
        [SerializeField] private float interStationPause = 1.5f;

        // -1 means idle; 0..N-1 means single-station tour; N means ActivateAll sequence in progress (use isPlayingAll).
        private readonly NetworkVariable<int> currentActiveStation =
            new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> isPlayingAll =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int CurrentActiveStation => currentActiveStation.Value;
        public bool IsPlayingAll => isPlayingAll.Value;

        Coroutine _activeRoutine;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            currentActiveStation.OnValueChanged += OnCurrentStationChanged;
            // Re-apply current state for late joiners.
            if (currentActiveStation.Value >= 0 && currentActiveStation.Value < stations.Count)
                ApplyVisualsForStation(currentActiveStation.Value, true);
        }

        public override void OnNetworkDespawn()
        {
            currentActiveStation.OnValueChanged -= OnCurrentStationChanged;
        }

        // -------- Public API (called from buttons) --------

        public void ActivateStation(int index)
        {
            if (index < 0 || index >= stations.Count) return;
            RequestActivateStationRpc(index);
        }

        public void ActivateAll()
        {
            RequestActivateAllRpc();
        }

        public void StopAll()
        {
            RequestStopAllRpc();
        }

        // -------- RPCs (anyone -> server) --------

        [Rpc(SendTo.Server)]
        void RequestActivateStationRpc(int index)
        {
            if (isPlayingAll.Value) return;
            currentActiveStation.Value = index;
        }

        [Rpc(SendTo.Server)]
        void RequestActivateAllRpc()
        {
            if (isPlayingAll.Value) return;
            isPlayingAll.Value = true;
            StartCoroutine(ActivateAllRoutineServer());
        }

        [Rpc(SendTo.Server)]
        void RequestStopAllRpc()
        {
            StopAllRoutineServer();
        }

        IEnumerator ActivateAllRoutineServer()
        {
            for (int i = 0; i < stations.Count; i++)
            {
                currentActiveStation.Value = i;
                // Wait long enough for the tour to play out on clients: travel + dwell across waypoints, plus padding.
                var s = stations[i];
                int wpCount = (s.tourWaypoints != null && s.tourWaypoints.Length > 0) ? s.tourWaypoints.Length : 1;
                float tourDuration = wpCount * (s.dwellPerWaypoint + s.travelTimeBetweenWaypoints) + 2f;
                yield return new WaitForSeconds(tourDuration + interStationPause);
            }
            currentActiveStation.Value = -1;
            isPlayingAll.Value = false;
        }

        void StopAllRoutineServer()
        {
            StopAllCoroutines();
            isPlayingAll.Value = false;
            currentActiveStation.Value = -1;
        }

        // -------- Local visual / tour execution (each client) --------

        void OnCurrentStationChanged(int previous, int next)
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }

            // Stop visuals on the previous station.
            if (previous >= 0 && previous < stations.Count)
                ApplyVisualsForStation(previous, false);

            if (next >= 0 && next < stations.Count)
            {
                ApplyVisualsForStation(next, true);
                _activeRoutine = StartCoroutine(GuidedTourLocal(stations[next]));
            }
        }

        void ApplyVisualsForStation(int index, bool active)
        {
            if (index < 0 || index >= stations.Count) return;
            var s = stations[index];

            if (s.activationParticles != null)
            {
                foreach (var ps in s.activationParticles)
                {
                    if (ps == null) continue;
                    if (active) ps.Play(true);
                    else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (s.stationAnimators != null && !string.IsNullOrEmpty(s.activationTriggerName))
            {
                foreach (var a in s.stationAnimators)
                {
                    if (a == null) continue;
                    if (active) a.SetTrigger(s.activationTriggerName);
                }
            }
        }

        IEnumerator GuidedTourLocal(WastewaterStation s)
        {
            GameObject rig = ResolvePlayerRig();
            if (rig == null) yield break;

            // Build the waypoint list: if explicit waypoints are configured, use them.
            // Otherwise fall back to a single computed viewpoint slightly offset from the
            // station root so the player at least gets teleported to *see* the station.
            Transform[] waypoints = s.tourWaypoints;
            Vector3[] fallbackPositions = null;
            Quaternion[] fallbackRotations = null;
            if (waypoints == null || waypoints.Length == 0)
            {
                if (s.stationRoot == null) yield break;
                // Look at the station from 15m back and 5m up. Cheap default.
                Vector3 target = s.stationRoot.position;
                Vector3 viewer = target + new Vector3(0, 5f, -15f);
                fallbackPositions = new[] { viewer };
                fallbackRotations = new[] { Quaternion.LookRotation((target - viewer).normalized, Vector3.up) };
            }

            CharacterController cc = rig.GetComponent<CharacterController>();
            XRBodyTransformer body = rig.GetComponentInChildren<XRBodyTransformer>();

            // Disable rig locomotion during the tour (pattern from WaterProcessSim).
            if (body != null) body.enabled = false;
            if (cc != null) cc.enabled = false;

            Vector3 startPos = rig.transform.position;
            Quaternion startRot = rig.transform.rotation;
            Vector3 originalScale = rig.transform.localScale;

            try
            {
                // Phase 1 — if microscale is enabled, shrink the player rig before the waypoint loop.
                // Mirrors WaterProcessSim.TeleportAndShrink (Assets/Scripts/Simulation/WaterProcessSim.cs:322-367).
                if (s.useMicroscale)
                {
                    Vector3 microTarget = originalScale * Mathf.Max(0.001f, s.microScale);
                    float dur = Mathf.Max(0.1f, s.scaleTransitionDuration);
                    float t = 0f;
                    while (t < 1f)
                    {
                        t += Time.deltaTime / dur;
                        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                        rig.transform.localScale = Vector3.Lerp(originalScale, microTarget, k);
                        yield return null;
                    }
                    rig.transform.localScale = microTarget;
                }
                int count = waypoints != null && waypoints.Length > 0 ? waypoints.Length : fallbackPositions.Length;
                for (int i = 0; i < count; i++)
                {
                    Vector3 toPos;
                    Quaternion toRot;
                    if (waypoints != null && waypoints.Length > 0)
                    {
                        var wp = waypoints[i];
                        if (wp == null) continue;
                        toPos = wp.position;
                        toRot = wp.rotation;
                    }
                    else
                    {
                        toPos = fallbackPositions[i];
                        toRot = fallbackRotations[i];
                    }

                    Vector3 from = rig.transform.position;
                    Quaternion fromRot = rig.transform.rotation;
                    float t = 0f;
                    while (t < 1f)
                    {
                        t += Time.deltaTime / Mathf.Max(0.01f, s.travelTimeBetweenWaypoints);
                        float k = Mathf.SmoothStep(0f, 1f, t);
                        rig.transform.position = Vector3.Lerp(from, toPos, k);
                        rig.transform.rotation = Quaternion.Slerp(fromRot, toRot, k);
                        yield return null;
                    }
                    yield return new WaitForSeconds(s.dwellPerWaypoint);
                }

                // Phase 3 — if microscale was used, restore the player rig to its original scale BEFORE returning.
                // (Doing this here, not in the finally block, because we can't yield inside finally.)
                if (s.useMicroscale)
                {
                    Vector3 currentScale = rig.transform.localScale;
                    float dur = Mathf.Max(0.1f, s.scaleTransitionDuration);
                    float t = 0f;
                    while (t < 1f)
                    {
                        t += Time.deltaTime / dur;
                        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                        rig.transform.localScale = Vector3.Lerp(currentScale, originalScale, k);
                        yield return null;
                    }
                }
            }
            finally
            {
                // Restore rig — position, rotation, scale (safety net), and locomotion.
                rig.transform.position = startPos;
                rig.transform.rotation = startRot;
                rig.transform.localScale = originalScale;
                Physics.SyncTransforms();
                if (cc != null) cc.enabled = true;
                if (body != null) body.enabled = true;
            }
        }

        GameObject ResolvePlayerRig()
        {
            if (playerRig != null) return playerRig;
            if (Camera.main == null) return null;
            return Camera.main.transform.root.gameObject;
        }
    }
}
