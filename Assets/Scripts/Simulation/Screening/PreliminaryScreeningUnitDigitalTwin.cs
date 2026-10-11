using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace XRMultiplayer
{
    /// <summary>
    /// Industrial preliminary-treatment digital twin component.
    /// Scope: two mechanically cleaned inlet screens, bypass channel, screenings discharge,
    /// hydraulic headloss, sensors, alarms, and operator telemetry.
    /// </summary>
    public class PreliminaryScreeningUnitDigitalTwin : MonoBehaviour
    {
        private const string GeneratedRootName = "__PreliminaryScreeningUnit_Generated";

        public enum UnitState
        {
            Offline,
            Ready,
            Running,
            RakeCycle,
            ScreeningsDischarge,
            HighLevelAlarm,
            JamFault,
            MaintenanceLockout
        }

        private enum ParticleState
        {
            Flowing,
            Captured,
            OnRake,
            Dropped
        }

        private enum PipeAxis
        {
            X,
            Y,
            Z
        }

        [Header("Build")]
        [SerializeField] private bool autoBuildOnAwake = true;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool keyboardControls = true;

        [Header("Geometry - metric assumptions")]
        [SerializeField] private int mechanicallyCleanedChannels = 2;
        [SerializeField] private bool includeBypassChannel = true;
        [SerializeField] private float channelLengthM = 8.0f;
        [SerializeField] private float channelWidthM = 0.8f;
        [SerializeField] private float channelDepthM = 2.0f;
        [SerializeField] private float channelWallThicknessM = 0.18f;
        [SerializeField] private float channelCenterSpacingM = 1.25f;
        [SerializeField] private float normalWaterDepthM = 0.55f;
        [SerializeField] private float screenSlotWidthM = 0.006f;
        [SerializeField] private float screenBarThicknessM = 0.006f;
        [SerializeField] private float screenAngleFromHorizontalDeg = 75f;
        [SerializeField] private float screenPackLengthM = 2.35f;

        [Header("Hydraulics")]
        [SerializeField] private float averagePlantFlowM3PerDay = 15000f;
        [SerializeField] private float peakFactor = 3.5f;
        [SerializeField] private float maxAllowableHeadlossM = 0.15f;
        [SerializeField] private float highLevelAlarmDepthM = 1.25f;
        [SerializeField] private float simulationTimeScale = 8f;

        [Header("Machine Behavior")]
        [SerializeField] private float rakeCycleIntervalSec = 45f;
        [SerializeField] private float rakeTravelSpeedMps = 0.18f;
        [SerializeField] private float rakeReturnSpeedMps = 0.42f;
        [SerializeField] private float capturedLoadToTriggerCleaningKg = 18f;
        [SerializeField] private float motorNominalCurrentA = 7.5f;
        [SerializeField] private float jamCurrentThresholdA = 18f;

        [Header("Visual particles")]
        [SerializeField] private int waterParticleCount = 90;
        [SerializeField] private int debrisParticleCount = 44;
        [SerializeField] private float debrisSpawnRatePerMinute = 28f;

        [Header("Training scenarios")]
        [SerializeField] private bool forceJamFault;
        [SerializeField] private bool forceHighInletLoad;
        [SerializeField] private bool maintenanceLockout;

        [Header("Colors")]
        [SerializeField] private Color rawWaterColor = new Color(0.36f, 0.25f, 0.14f, 1f);
        [SerializeField] private Color concreteColor = new Color(0.56f, 0.54f, 0.49f, 1f);
        [SerializeField] private Color stainlessColor = new Color(0.68f, 0.68f, 0.64f, 1f);
        [SerializeField] private Color screeningsColor = new Color(0.17f, 0.14f, 0.11f, 1f);
        [SerializeField] private Color alarmColor = new Color(1f, 0.1f, 0.05f, 1f);
        [SerializeField] private Color runningColor = new Color(0.2f, 1f, 0.32f, 1f);

        private readonly List<ScreenChannelRuntime> _channels = new List<ScreenChannelRuntime>();
        private readonly List<FlowParticle> _waterParticles = new List<FlowParticle>();
        private readonly List<DebrisParticle> _debrisParticles = new List<DebrisParticle>();
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();

        private Transform _generatedRoot;
        private Transform _waterParticleRoot;
        private Transform _debrisParticleRoot;
        private TextMeshPro _operatorText;
        private Renderer _alarmBeacon;
        private UnitState _state = UnitState.Offline;
        private float _processTime;
        private float _lastSpawnTime;
        private float _avgFlowM3s;
        private float _peakFlowM3s;
        private float _approachVelocityMps;
        private float _totalScreeningsKg;
        private float _dumpsterFill01;

        private void Awake()
        {
            if (autoBuildOnAwake)
            {
                Rebuild();
            }

            _state = playOnStart ? UnitState.Running : UnitState.Ready;
        }

        private void Update()
        {
            if (keyboardControls)
            {
                HandleKeyboardControls();
            }

            float scaledDt = Time.deltaTime * simulationTimeScale;
            _processTime += scaledDt;

            if (_generatedRoot == null || _channels.Count == 0)
            {
                return;
            }

            UpdateStateMachine(scaledDt);
            UpdateHydraulics();
            UpdateRakes(scaledDt);
            UpdateWaterParticles(scaledDt);
            UpdateDebrisParticles(scaledDt);
            UpdateTelemetry();
        }

        [ContextMenu("Rebuild Preliminary Screening Unit")]
        public void Rebuild()
        {
            ClearGenerated();
            ComputeHydraulicAssumptions();
            BuildUnit();
            InitializeParticles();
            UpdateHydraulics();
            UpdateTelemetry();
        }

        public void StartUnit()
        {
            if (maintenanceLockout)
            {
                _state = UnitState.MaintenanceLockout;
                return;
            }

            _state = UnitState.Running;
        }

        public void StopUnit()
        {
            _state = UnitState.Ready;
        }

        public void ManualRakeCycle()
        {
            if (_state == UnitState.MaintenanceLockout || _state == UnitState.JamFault)
            {
                return;
            }

            for (int i = 0; i < _channels.Count; i++)
            {
                StartRake(_channels[i]);
            }

            _state = UnitState.RakeCycle;
        }

        public void ResetFaults()
        {
            forceJamFault = false;
            forceHighInletLoad = false;
            maintenanceLockout = false;
            for (int i = 0; i < _channels.Count; i++)
            {
                _channels[i].jammed = false;
                _channels[i].motorCurrentA = motorNominalCurrentA;
            }

            _state = UnitState.Running;
        }

        public void ToggleJamFault()
        {
            forceJamFault = !forceJamFault;
        }

        public void ToggleHighLoad()
        {
            forceHighInletLoad = !forceHighInletLoad;
        }

        private void HandleKeyboardControls()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                if (_state == UnitState.Running || _state == UnitState.RakeCycle)
                {
                    StopUnit();
                }
                else
                {
                    StartUnit();
                }
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                ManualRakeCycle();
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                ToggleJamFault();
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                ToggleHighLoad();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetFaults();
            }
        }

        private void ComputeHydraulicAssumptions()
        {
            _avgFlowM3s = averagePlantFlowM3PerDay / 86400f;
            _peakFlowM3s = _avgFlowM3s * peakFactor;
            float activeArea = Mathf.Max(0.01f, mechanicallyCleanedChannels * channelWidthM * normalWaterDepthM);
            _approachVelocityMps = _peakFlowM3s / activeArea;
        }

        private void UpdateStateMachine(float dt)
        {
            if (maintenanceLockout)
            {
                _state = UnitState.MaintenanceLockout;
                return;
            }

            if (forceJamFault)
            {
                _state = UnitState.JamFault;
                for (int i = 0; i < _channels.Count; i++)
                {
                    _channels[i].jammed = true;
                    _channels[i].motorCurrentA = Mathf.Lerp(_channels[i].motorCurrentA, jamCurrentThresholdA + 3f, dt * 0.45f);
                }
                return;
            }

            bool anyHighLevel = false;
            bool anyRaking = false;
            bool anyDischarging = false;

            for (int i = 0; i < _channels.Count; i++)
            {
                ScreenChannelRuntime channel = _channels[i];
                anyHighLevel |= channel.upstreamDepthM > highLevelAlarmDepthM;
                anyRaking |= channel.rakeMode != RakeMode.Idle;
                anyDischarging |= channel.rakeMode == RakeMode.DischargePause;

                channel.cycleTimer += dt;
                if (_state == UnitState.Running &&
                    (channel.cycleTimer >= rakeCycleIntervalSec || channel.capturedLoadKg >= capturedLoadToTriggerCleaningKg))
                {
                    StartRake(channel);
                }
            }

            if (anyHighLevel)
            {
                _state = UnitState.HighLevelAlarm;
            }
            else if (anyDischarging)
            {
                _state = UnitState.ScreeningsDischarge;
            }
            else if (anyRaking)
            {
                _state = UnitState.RakeCycle;
            }
            else if (_state != UnitState.Ready && _state != UnitState.Offline)
            {
                _state = UnitState.Running;
            }
        }

        private void UpdateHydraulics()
        {
            for (int i = 0; i < _channels.Count; i++)
            {
                ScreenChannelRuntime channel = _channels[i];
                float load01 = Mathf.Clamp01(channel.capturedLoadKg / capturedLoadToTriggerCleaningKg);
                if (forceHighInletLoad)
                {
                    load01 = Mathf.Max(load01, 0.9f);
                }

                channel.headlossM = 0.025f + load01 * 0.23f;
                channel.upstreamDepthM = normalWaterDepthM + channel.headlossM;
                channel.downstreamDepthM = normalWaterDepthM - Mathf.Min(0.08f, channel.headlossM * 0.25f);

                if (channel.upstreamWater != null)
                {
                    Vector3 scale = channel.upstreamWater.localScale;
                    channel.upstreamWater.localScale = new Vector3(scale.x, 0.018f, scale.z);
                    channel.upstreamWater.localPosition = new Vector3(channel.upstreamWater.localPosition.x, channel.upstreamDepthM, channel.upstreamWater.localPosition.z);
                }

                if (channel.downstreamWater != null)
                {
                    Vector3 scale = channel.downstreamWater.localScale;
                    channel.downstreamWater.localScale = new Vector3(scale.x, 0.018f, scale.z);
                    channel.downstreamWater.localPosition = new Vector3(channel.downstreamWater.localPosition.x, channel.downstreamDepthM, channel.downstreamWater.localPosition.z);
                }
            }
        }

        private void StartRake(ScreenChannelRuntime channel)
        {
            if (channel.jammed)
            {
                return;
            }

            channel.rakeMode = RakeMode.TravelUp;
            channel.rakeLocalY = 0.12f;
            channel.cycleTimer = 0f;
            channel.motorCurrentA = motorNominalCurrentA + channel.capturedLoadKg * 0.08f;
        }

        private void UpdateRakes(float dt)
        {
            for (int i = 0; i < _channels.Count; i++)
            {
                ScreenChannelRuntime channel = _channels[i];
                if (channel.rake == null)
                {
                    continue;
                }

                if (channel.jammed)
                {
                    channel.motorCurrentA = Mathf.Lerp(channel.motorCurrentA, jamCurrentThresholdA + 3f, dt * 0.6f);
                    SetStatusLight(channel, alarmColor);
                    continue;
                }

                switch (channel.rakeMode)
                {
                    case RakeMode.Idle:
                        channel.rakeLocalY = Mathf.Lerp(channel.rakeLocalY, 0.12f, dt * 0.8f);
                        channel.motorCurrentA = Mathf.Lerp(channel.motorCurrentA, motorNominalCurrentA * 0.35f, dt * 0.5f);
                        break;

                    case RakeMode.TravelUp:
                        channel.rakeLocalY += rakeTravelSpeedMps * dt;
                        channel.motorCurrentA = motorNominalCurrentA + Mathf.Clamp(channel.capturedLoadKg * 0.12f, 0f, 7f);
                        LiftCapturedDebris(channel);
                        if (channel.rakeLocalY >= screenPackLengthM - 0.22f)
                        {
                            channel.rakeMode = RakeMode.DischargePause;
                            channel.dischargeTimer = 0f;
                            DropRakedDebris(channel);
                        }
                        break;

                    case RakeMode.DischargePause:
                        channel.dischargeTimer += dt;
                        channel.motorCurrentA = Mathf.Lerp(channel.motorCurrentA, motorNominalCurrentA * 0.65f, dt * 0.5f);
                        if (channel.dischargeTimer > 2.5f)
                        {
                            channel.rakeMode = RakeMode.ReturnDown;
                        }
                        break;

                    case RakeMode.ReturnDown:
                        channel.rakeLocalY -= rakeReturnSpeedMps * dt;
                        channel.motorCurrentA = Mathf.Lerp(channel.motorCurrentA, motorNominalCurrentA * 0.55f, dt * 0.8f);
                        if (channel.rakeLocalY <= 0.12f)
                        {
                            channel.rakeLocalY = 0.12f;
                            channel.rakeMode = RakeMode.Idle;
                            channel.capturedLoadKg *= 0.18f;
                        }
                        break;
                }

                channel.rake.localPosition = new Vector3(0f, channel.rakeLocalY, -0.035f);
                SetStatusLight(channel, channel.rakeMode == RakeMode.Idle ? runningColor : new Color(1f, 0.72f, 0.12f, 1f));
            }

            if (_alarmBeacon != null)
            {
                Color target = (_state == UnitState.HighLevelAlarm || _state == UnitState.JamFault || _state == UnitState.MaintenanceLockout) ? alarmColor : runningColor;
                SetMaterialColor(_alarmBeacon.sharedMaterial, target);
            }
        }

        private void LiftCapturedDebris(ScreenChannelRuntime channel)
        {
            for (int i = 0; i < _debrisParticles.Count; i++)
            {
                DebrisParticle debris = _debrisParticles[i];
                if (debris.channelIndex != channel.index || debris.state != ParticleState.Captured)
                {
                    continue;
                }

                if (debris.screenLocalY <= channel.rakeLocalY + 0.1f)
                {
                    debris.state = ParticleState.OnRake;
                    debris.rakeOffset = new Vector3(debris.localX, 0f, -0.09f);
                }
            }
        }

        private void DropRakedDebris(ScreenChannelRuntime channel)
        {
            for (int i = 0; i < _debrisParticles.Count; i++)
            {
                DebrisParticle debris = _debrisParticles[i];
                if (debris.channelIndex != channel.index || debris.state != ParticleState.OnRake)
                {
                    continue;
                }

                debris.state = ParticleState.Dropped;
                debris.droppedAt = _processTime;
                debris.transform.SetParent(_generatedRoot, true);
                debris.transform.position = channel.dischargeWorldPosition + new Vector3(Random.Range(-0.12f, 0.12f), 0.0f, Random.Range(-0.08f, 0.08f));
                Rigidbody rb = debris.rigidbody;
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.mass = Mathf.Max(0.03f, debris.massKg);
                rb.linearVelocity = new Vector3(Random.Range(-0.25f, 0.25f), -0.2f, Random.Range(-0.15f, 0.15f));
                _totalScreeningsKg += debris.massKg;
                _dumpsterFill01 = Mathf.Clamp01(_totalScreeningsKg / 350f);
            }
        }

        private void UpdateWaterParticles(float dt)
        {
            if (_state == UnitState.Offline || _state == UnitState.Ready || _state == UnitState.MaintenanceLockout)
            {
                return;
            }

            for (int i = 0; i < _waterParticles.Count; i++)
            {
                FlowParticle particle = _waterParticles[i];
                ScreenChannelRuntime channel = _channels[particle.channelIndex % _channels.Count];
                float velocity = Mathf.Max(0.08f, _approachVelocityMps);
                particle.progressM += velocity * dt;
                if (particle.progressM > channelLengthM)
                {
                    particle.progressM -= channelLengthM;
                    particle.laneOffset = Random.Range(-channelWidthM * 0.42f, channelWidthM * 0.42f);
                }

                float z = -channelLengthM * 0.5f + particle.progressM;
                bool upstream = z < channel.screenZ;
                float depth = upstream ? channel.upstreamDepthM : channel.downstreamDepthM;
                float wave = Mathf.Sin(_processTime * 2.4f + i * 0.63f) * 0.018f;
                particle.transform.localPosition = new Vector3(channel.centerX + particle.laneOffset, depth + 0.035f + wave, z);
            }
        }

        private void UpdateDebrisParticles(float dt)
        {
            if (_state == UnitState.Offline || _state == UnitState.Ready || _state == UnitState.MaintenanceLockout)
            {
                return;
            }

            float spawnInterval = 60f / Mathf.Max(1f, debrisSpawnRatePerMinute * (forceHighInletLoad ? 2.5f : 1f));
            if (_processTime - _lastSpawnTime > spawnInterval)
            {
                ResetDroppedOrPassedDebris();
                _lastSpawnTime = _processTime;
            }

            for (int i = 0; i < _debrisParticles.Count; i++)
            {
                DebrisParticle debris = _debrisParticles[i];
                if (debris.state == ParticleState.Flowing)
                {
                    UpdateFlowingDebris(debris, dt);
                }
                else if (debris.state == ParticleState.Captured)
                {
                    ScreenChannelRuntime channel = _channels[debris.channelIndex];
                    Vector3 screenWorld = channel.screenRoot.TransformPoint(new Vector3(debris.localX, debris.screenLocalY, -0.065f));
                    debris.transform.position = Vector3.Lerp(debris.transform.position, screenWorld, dt * 5f);
                }
                else if (debris.state == ParticleState.OnRake)
                {
                    ScreenChannelRuntime channel = _channels[debris.channelIndex];
                    debris.transform.position = channel.rake.TransformPoint(debris.rakeOffset);
                }
                else if (debris.state == ParticleState.Dropped && _processTime - debris.droppedAt > 18f)
                {
                    RespawnDebris(debris, false);
                }
            }
        }

        private void UpdateFlowingDebris(DebrisParticle debris, float dt)
        {
            ScreenChannelRuntime channel = _channels[debris.channelIndex];
            float velocity = Mathf.Max(0.06f, _approachVelocityMps * 0.72f);
            debris.progressM += velocity * dt;

            float z = -channelLengthM * 0.5f + debris.progressM;
            float y = channel.upstreamDepthM + 0.06f + Mathf.Sin(_processTime * 1.8f + debris.seed) * 0.025f;
            debris.transform.localPosition = new Vector3(channel.centerX + debris.localX, y, z);
            debris.transform.Rotate(debris.angularVelocityDegPerSec * dt, Space.Self);

            if (!debris.evaluatedAtScreen && z >= channel.screenZ)
            {
                debris.evaluatedAtScreen = true;
                bool captured = debris.sizeM > screenSlotWidthM || Random.value < 0.12f;
                if (captured)
                {
                    debris.state = ParticleState.Captured;
                    debris.screenLocalY = Random.Range(0.25f, 1.05f);
                    channel.capturedLoadKg += debris.massKg;
                    Rigidbody rb = debris.rigidbody;
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
            }

            if (debris.progressM > channelLengthM + 0.5f)
            {
                RespawnDebris(debris, false);
            }
        }

        private void ResetDroppedOrPassedDebris()
        {
            for (int i = 0; i < _debrisParticles.Count; i++)
            {
                if (_debrisParticles[i].state == ParticleState.Dropped || _debrisParticles[i].progressM > channelLengthM)
                {
                    RespawnDebris(_debrisParticles[i], true);
                    return;
                }
            }
        }

        private void RespawnDebris(DebrisParticle debris, bool nearStart)
        {
            debris.channelIndex = Random.Range(0, _channels.Count);
            debris.progressM = nearStart ? Random.Range(0f, 0.35f) : Random.Range(0f, channelLengthM * 0.35f);
            debris.localX = Random.Range(-channelWidthM * 0.38f, channelWidthM * 0.38f);
            debris.sizeM = Random.Range(0.008f, 0.09f);
            debris.massKg = Mathf.Lerp(0.02f, 0.45f, Mathf.InverseLerp(0.008f, 0.09f, debris.sizeM));
            debris.state = ParticleState.Flowing;
            debris.evaluatedAtScreen = false;
            debris.seed = Random.Range(0f, 100f);
            debris.angularVelocityDegPerSec = new Vector3(Random.Range(-60f, 60f), Random.Range(-90f, 90f), Random.Range(-70f, 70f));
            debris.transform.SetParent(_debrisParticleRoot, false);
            debris.transform.localScale = new Vector3(debris.sizeM * Random.Range(2.0f, 4.0f), debris.sizeM * Random.Range(0.35f, 0.8f), debris.sizeM * Random.Range(1.3f, 3.0f));
            Rigidbody rb = debris.rigidbody;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private void UpdateTelemetry()
        {
            if (_operatorText == null)
            {
                return;
            }

            float maxHeadloss = 0f;
            float maxUpstream = 0f;
            float totalLoad = 0f;
            float maxCurrent = 0f;
            string activeMode = "";

            for (int i = 0; i < _channels.Count; i++)
            {
                ScreenChannelRuntime channel = _channels[i];
                maxHeadloss = Mathf.Max(maxHeadloss, channel.headlossM);
                maxUpstream = Mathf.Max(maxUpstream, channel.upstreamDepthM);
                totalLoad += channel.capturedLoadKg;
                maxCurrent = Mathf.Max(maxCurrent, channel.motorCurrentA);
                activeMode += "S" + (i + 1) + ":" + channel.rakeMode + " ";
            }

            _operatorText.text =
                "PRELIMINARY SCREENING DIGITAL TWIN\n" +
                "State: " + _state + " | " + activeMode + "\n" +
                "Avg flow: " + _avgFlowM3s.ToString("0.000") + " m3/s (" + averagePlantFlowM3PerDay.ToString("0") + " m3/day)\n" +
                "Peak design flow: " + _peakFlowM3s.ToString("0.000") + " m3/s | approach velocity: " + _approachVelocityMps.ToString("0.00") + " m/s\n" +
                "Max upstream depth: " + maxUpstream.ToString("0.00") + " m | headloss: " + maxHeadloss.ToString("0.00") + " m / limit " + maxAllowableHeadlossM.ToString("0.00") + " m\n" +
                "Captured load: " + totalLoad.ToString("0.0") + " kg | dumpster: " + (_dumpsterFill01 * 100f).ToString("0") + "%\n" +
                "Motor current max: " + maxCurrent.ToString("0.0") + " A | keys P start/stop, C clean, H high load, J jam, R reset";
        }

        private void ClearGenerated()
        {
            _channels.Clear();
            _waterParticles.Clear();
            _debrisParticles.Clear();
            _materials.Clear();
            _totalScreeningsKg = 0f;
            _dumpsterFill01 = 0f;

            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(existing.gameObject);
                }
                else
                {
                    DestroyImmediate(existing.gameObject);
                }
            }

            _generatedRoot = null;
            _waterParticleRoot = null;
            _debrisParticleRoot = null;
            _operatorText = null;
            _alarmBeacon = null;
        }

        private void BuildUnit()
        {
            _generatedRoot = new GameObject(GeneratedRootName).transform;
            _generatedRoot.SetParent(transform, false);

            BuildCivilStructure();
            BuildMachineChannels();
            BuildSharedDischargeEquipment();
            BuildBypassChannel();
            BuildOperatorPanel();
        }

        private void BuildCivilStructure()
        {
            Material concrete = GetMaterial("Concrete_Worn", concreteColor);
            Material wallStain = GetMaterial("Concrete_Wet_Stains", new Color(0.24f, 0.23f, 0.21f, 1f));

            float overallWidth = mechanicallyCleanedChannels * channelCenterSpacingM + (includeBypassChannel ? channelCenterSpacingM : 0f) + 1.2f;
            CreateBox("Concrete_Service_Slab", _generatedRoot, new Vector3(0f, -0.12f, 0f), new Vector3(overallWidth + 1.2f, 0.22f, channelLengthM + 1.4f), concrete);
            CreateBox("Rear_Equipment_Wall", _generatedRoot, new Vector3(0f, 1.55f, -channelLengthM * 0.5f - 0.72f), new Vector3(overallWidth + 1.2f, 3.1f, 0.24f), concrete);
            CreateBox("Wet_Stain_Band", _generatedRoot, new Vector3(0f, 0.62f, -channelLengthM * 0.5f - 0.585f), new Vector3(overallWidth + 0.8f, 0.34f, 0.035f), wallStain);
        }

        private void BuildMachineChannels()
        {
            float originX = -((mechanicallyCleanedChannels - 1) * channelCenterSpacingM) * 0.5f;
            for (int i = 0; i < mechanicallyCleanedChannels; i++)
            {
                float centerX = originX + i * channelCenterSpacingM;
                ScreenChannelRuntime channel = BuildScreenChannel(i, centerX);
                _channels.Add(channel);
            }
        }

        private ScreenChannelRuntime BuildScreenChannel(int index, float centerX)
        {
            Material concrete = GetMaterial("Concrete_Worn", concreteColor);
            Material water = GetMaterial("Raw_Wastewater", rawWaterColor);
            Material stainless = GetMaterial("Pickled_Stainless", stainlessColor);
            Material blue = GetMaterial("Blue_Motor_And_Valves", new Color(0.04f, 0.22f, 0.68f, 1f));
            Material dark = GetMaterial("Dark_Wet_Channel", new Color(0.08f, 0.07f, 0.055f, 1f));

            Transform root = new GameObject("Screen_Channel_" + (index + 1)).transform;
            root.SetParent(_generatedRoot, false);
            root.localPosition = Vector3.zero;

            CreateBox("Channel_Floor", root, new Vector3(centerX, 0f, 0f), new Vector3(channelWidthM + channelWallThicknessM * 2f, 0.16f, channelLengthM), concrete);
            CreateBox("Left_Channel_Wall", root, new Vector3(centerX - channelWidthM * 0.5f - channelWallThicknessM * 0.5f, channelDepthM * 0.5f, 0f), new Vector3(channelWallThicknessM, channelDepthM, channelLengthM), concrete);
            CreateBox("Right_Channel_Wall", root, new Vector3(centerX + channelWidthM * 0.5f + channelWallThicknessM * 0.5f, channelDepthM * 0.5f, 0f), new Vector3(channelWallThicknessM, channelDepthM, channelLengthM), concrete);
            CreateBox("Dark_Wetted_Concrete_Left", root, new Vector3(centerX - channelWidthM * 0.5f - 0.006f, normalWaterDepthM * 0.55f, 0f), new Vector3(0.018f, normalWaterDepthM, channelLengthM * 0.95f), dark);
            CreateBox("Dark_Wetted_Concrete_Right", root, new Vector3(centerX + channelWidthM * 0.5f + 0.006f, normalWaterDepthM * 0.55f, 0f), new Vector3(0.018f, normalWaterDepthM, channelLengthM * 0.95f), dark);

            Transform upstreamWater = CreateBox("Upstream_Water_Surface", root, new Vector3(centerX, normalWaterDepthM, -channelLengthM * 0.27f), new Vector3(channelWidthM * 0.92f, 0.018f, channelLengthM * 0.43f), water).transform;
            Transform downstreamWater = CreateBox("Downstream_Water_Surface", root, new Vector3(centerX, normalWaterDepthM - 0.03f, channelLengthM * 0.25f), new Vector3(channelWidthM * 0.92f, 0.018f, channelLengthM * 0.42f), water).transform;

            CreatePipe("Influent_Pipe_" + (index + 1), root, new Vector3(centerX, normalWaterDepthM + 0.25f, -channelLengthM * 0.5f - 0.65f), 1.55f, 0.18f, PipeAxis.Z, GetMaterial("Raw_Brown_Pipe", new Color(0.42f, 0.25f, 0.19f, 1f)));
            CreateValve(root, new Vector3(centerX, normalWaterDepthM + 0.58f, -channelLengthM * 0.5f - 0.05f), 0.22f, blue);

            Transform screenRoot = new GameObject("Step_Screen_Pack_" + (index + 1)).transform;
            screenRoot.SetParent(root, false);
            float screenZ = -0.65f;
            screenRoot.localPosition = new Vector3(centerX, 0.12f, screenZ);
            screenRoot.localRotation = Quaternion.Euler(-(90f - screenAngleFromHorizontalDeg), 0f, 0f);

            int barCount = Mathf.Clamp(Mathf.RoundToInt(channelWidthM / Mathf.Max(0.002f, screenSlotWidthM + screenBarThicknessM)), 32, 140);
            float clearPackWidth = (barCount - 1) * (screenSlotWidthM + screenBarThicknessM);
            for (int b = 0; b < barCount; b++)
            {
                float x = -clearPackWidth * 0.5f + b * (screenSlotWidthM + screenBarThicknessM);
                CreateBox("Fixed_Lamella_" + b, screenRoot, new Vector3(x, screenPackLengthM * 0.5f, 0f), new Vector3(screenBarThicknessM, screenPackLengthM, 0.035f), stainless);
            }

            CreateBox("Screen_Left_Frame", screenRoot, new Vector3(-channelWidthM * 0.52f, screenPackLengthM * 0.5f, 0f), new Vector3(0.04f, screenPackLengthM + 0.08f, 0.08f), stainless);
            CreateBox("Screen_Right_Frame", screenRoot, new Vector3(channelWidthM * 0.52f, screenPackLengthM * 0.5f, 0f), new Vector3(0.04f, screenPackLengthM + 0.08f, 0.08f), stainless);
            CreateBox("Screen_Top_Discharge_Lip", screenRoot, new Vector3(0f, screenPackLengthM + 0.08f, -0.02f), new Vector3(channelWidthM + 0.28f, 0.05f, 0.16f), stainless);

            Transform rake = new GameObject("Moving_Rake_" + (index + 1)).transform;
            rake.SetParent(screenRoot, false);
            rake.localPosition = new Vector3(0f, 0.12f, -0.035f);
            CreateBox("Rake_Crossbar", rake, Vector3.zero, new Vector3(channelWidthM + 0.08f, 0.045f, 0.055f), stainless);
            int toothCount = Mathf.Clamp(Mathf.RoundToInt(channelWidthM / 0.055f), 8, 18);
            for (int t = 0; t < toothCount; t++)
            {
                float x = -channelWidthM * 0.43f + t * (channelWidthM * 0.86f / Mathf.Max(1, toothCount - 1));
                CreateBox("Rake_Tooth_" + t, rake, new Vector3(x, -0.035f, -0.055f), new Vector3(0.018f, 0.12f, 0.055f), stainless);
            }

            CreateBox("Drive_Housing", root, new Vector3(centerX, 2.72f, screenZ - 0.55f), new Vector3(channelWidthM + 0.32f, 0.28f, 0.28f), stainless);
            CreatePipe("Drive_Motor_Blue", root, new Vector3(centerX - channelWidthM * 0.45f, 2.76f, screenZ - 0.82f), 0.42f, 0.12f, PipeAxis.X, blue);
            CreateGrating(root, new Vector3(centerX, channelDepthM + 0.08f, -1.35f), channelWidthM + 0.7f, 1.25f);
            CreateRailing(root, new Vector3(centerX, channelDepthM + 0.16f, -1.35f), channelWidthM + 0.88f, 1.42f);

            Vector3 dischargeWorld = transform.TransformPoint(new Vector3(centerX, 2.45f, -2.7f));
            CreateBox("Screenings_Discharge_Chute_" + (index + 1), root, new Vector3(centerX, 2.37f, -2.15f), new Vector3(channelWidthM * 0.55f, 0.16f, 1.25f), stainless);

            Renderer statusLight = CreateSphere("Screen_Status_Light_" + (index + 1), root, new Vector3(centerX + channelWidthM * 0.58f, 2.55f, -1.18f), 0.07f, GetMaterial("Running_Green", runningColor));

            return new ScreenChannelRuntime
            {
                index = index,
                root = root,
                centerX = centerX,
                screenZ = screenZ,
                screenRoot = screenRoot,
                rake = rake,
                upstreamWater = upstreamWater,
                downstreamWater = downstreamWater,
                dischargeWorldPosition = dischargeWorld,
                statusLight = statusLight,
                rakeLocalY = 0.12f,
                motorCurrentA = motorNominalCurrentA * 0.35f
            };
        }

        private void BuildSharedDischargeEquipment()
        {
            Material yellow = GetMaterial("Yellow_Dumpster", new Color(1f, 0.74f, 0.02f, 1f));
            Material stainless = GetMaterial("Pickled_Stainless", stainlessColor);
            Material screenings = GetMaterial("Screenings_Dark_Wet", screeningsColor);
            float centerX = 0f;
            CreateBox("Shared_Transfer_Trough", _generatedRoot, new Vector3(centerX, 2.22f, -2.95f), new Vector3(mechanicallyCleanedChannels * channelCenterSpacingM + 0.7f, 0.18f, 0.38f), stainless);
            CreatePipe("Screenings_Screw_Conveyor", _generatedRoot, new Vector3(centerX, 2.36f, -2.95f), mechanicallyCleanedChannels * channelCenterSpacingM + 0.85f, 0.055f, PipeAxis.X, stainless);
            CreateBox("Yellow_Screenings_Dumpster", _generatedRoot, new Vector3(centerX, 0.55f, -3.72f), new Vector3(2.6f, 1.1f, 1.25f), yellow);
            CreateBox("Dumpster_Open_Top", _generatedRoot, new Vector3(centerX, 1.13f, -3.72f), new Vector3(2.75f, 0.08f, 1.38f), yellow);
            CreateBox("Screenings_Pile_Base", _generatedRoot, new Vector3(centerX, 1.24f, -3.72f), new Vector3(1.9f, 0.24f, 0.85f), screenings);
            for (int i = 0; i < 18; i++)
            {
                CreateSphere("Static_Screenings_Clump_" + i, _generatedRoot, new Vector3(Random.Range(-0.9f, 0.9f), 1.34f + Random.Range(0f, 0.25f), -3.72f + Random.Range(-0.38f, 0.38f)), Random.Range(0.035f, 0.11f), screenings);
            }
        }

        private void BuildBypassChannel()
        {
            if (!includeBypassChannel)
            {
                return;
            }

            Material concrete = GetMaterial("Concrete_Worn", concreteColor);
            Material stainless = GetMaterial("Pickled_Stainless", stainlessColor);
            Material red = GetMaterial("Bypass_Red_Lockout", new Color(0.8f, 0.08f, 0.05f, 1f));

            float centerX = mechanicallyCleanedChannels * channelCenterSpacingM * 0.5f + 0.9f;
            CreateBox("Bypass_Channel_Floor", _generatedRoot, new Vector3(centerX, 0f, 0f), new Vector3(0.68f, 0.16f, channelLengthM), concrete);
            CreateBox("Bypass_Left_Wall", _generatedRoot, new Vector3(centerX - 0.43f, channelDepthM * 0.5f, 0f), new Vector3(channelWallThicknessM, channelDepthM, channelLengthM), concrete);
            CreateBox("Bypass_Right_Wall", _generatedRoot, new Vector3(centerX + 0.43f, channelDepthM * 0.5f, 0f), new Vector3(channelWallThicknessM, channelDepthM, channelLengthM), concrete);
            Transform manualRack = new GameObject("Emergency_Bypass_Manual_Bar_Rack").transform;
            manualRack.SetParent(_generatedRoot, false);
            manualRack.localPosition = new Vector3(centerX, 0.14f, -0.65f);
            manualRack.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            for (int i = 0; i < 16; i++)
            {
                float x = -0.28f + i * (0.56f / 15f);
                CreateBox("Manual_Rack_Bar_" + i, manualRack, new Vector3(x, 0.92f, 0f), new Vector3(0.018f, 1.84f, 0.04f), stainless);
            }
            CreateBox("Bypass_Stop_Gate", _generatedRoot, new Vector3(centerX, 1.05f, -1.65f), new Vector3(0.72f, 1.65f, 0.08f), red);
            CreateValve(_generatedRoot, new Vector3(centerX, 2.1f, -1.65f), 0.25f, red);
        }

        private void BuildOperatorPanel()
        {
            Material panel = GetMaterial("Dark_Operator_Panel", new Color(0.04f, 0.05f, 0.055f, 1f));
            CreateBox("Operator_Panel_Backplate", _generatedRoot, new Vector3(3.7f, 1.65f, 1.85f), new Vector3(3.7f, 1.25f, 0.08f), panel);
            _operatorText = CreateLabel("Operator_Telemetry_Text", _generatedRoot, new Vector3(3.7f, 1.72f, 1.79f), "", 0.92f, Color.white, new Vector2(3.35f, 1.06f));
            _alarmBeacon = CreateSphere("Alarm_Beacon", _generatedRoot, new Vector3(2.05f, 2.23f, 1.72f), 0.08f, GetMaterial("Alarm_Beacon_Material", runningColor));
        }

        private void InitializeParticles()
        {
            _waterParticleRoot = new GameObject("Hydraulic_Flow_Particles").transform;
            _waterParticleRoot.SetParent(_generatedRoot, false);
            _debrisParticleRoot = new GameObject("Screenings_Debris_Particles").transform;
            _debrisParticleRoot.SetParent(_generatedRoot, false);

            Material waterMat = GetMaterial("Water_Particle_Mat", new Color(0.55f, 0.38f, 0.22f, 1f));
            Material debrisMat = GetMaterial("Debris_Particle_Mat", screeningsColor);

            for (int i = 0; i < waterParticleCount; i++)
            {
                int channel = Random.Range(0, _channels.Count);
                Renderer sphere = CreateSphere("Water_Droplet_" + i, _waterParticleRoot, Vector3.zero, Random.Range(0.018f, 0.035f), waterMat);
                FlowParticle p = new FlowParticle
                {
                    transform = sphere.transform,
                    channelIndex = channel,
                    progressM = Random.Range(0f, channelLengthM),
                    laneOffset = Random.Range(-channelWidthM * 0.42f, channelWidthM * 0.42f)
                };
                _waterParticles.Add(p);
            }

            for (int i = 0; i < debrisParticleCount; i++)
            {
                Renderer body = CreateBox("Debris_Rag_Plastic_" + i, _debrisParticleRoot, Vector3.zero, Vector3.one * 0.05f, new Material(debrisMat)).GetComponent<Renderer>();
                Rigidbody rb = body.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                BoxCollider collider = body.gameObject.GetComponent<BoxCollider>();
                collider.size = Vector3.one;

                DebrisParticle debris = new DebrisParticle
                {
                    transform = body.transform,
                    renderer = body,
                    rigidbody = rb
                };
                _debrisParticles.Add(debris);
                RespawnDebris(debris, false);
            }
        }

        private void SetStatusLight(ScreenChannelRuntime channel, Color color)
        {
            if (channel.statusLight != null)
            {
                SetMaterialColor(channel.statusLight.sharedMaterial, color);
            }
        }

        private GameObject CreateBox(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private Renderer CreateSphere(string name, Transform parent, Vector3 localPosition, float radius, Material material)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = localPosition;
            sphere.transform.localScale = Vector3.one * radius * 2f;
            Renderer renderer = sphere.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private void CreatePipe(string name, Transform parent, Vector3 localPosition, float length, float radius, PipeAxis axis, Material material)
        {
            GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = name;
            pipe.transform.SetParent(parent, false);
            pipe.transform.localPosition = localPosition;
            pipe.transform.localScale = new Vector3(radius, length * 0.5f, radius);
            if (axis == PipeAxis.X)
            {
                pipe.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else if (axis == PipeAxis.Z)
            {
                pipe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            pipe.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void CreateValve(Transform parent, Vector3 localPosition, float size, Material material)
        {
            CreateBox("Valve_Body", parent, localPosition, new Vector3(size * 0.8f, size * 0.62f, size * 0.5f), material);
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Valve_Handwheel";
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = localPosition + new Vector3(0f, size * 0.62f, 0f);
            wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            wheel.transform.localScale = new Vector3(size, 0.018f, size);
            wheel.GetComponent<Renderer>().sharedMaterial = material;
            CreatePipe("Valve_Stem", parent, localPosition + new Vector3(0f, size * 0.34f, 0f), size * 0.72f, size * 0.035f, PipeAxis.Y, material);
        }

        private void CreateGrating(Transform parent, Vector3 center, float width, float depth)
        {
            Material material = GetMaterial("Galvanized_Grating", new Color(0.72f, 0.72f, 0.68f, 1f));
            int xBars = Mathf.Max(4, Mathf.RoundToInt(width / 0.12f));
            int zBars = Mathf.Max(4, Mathf.RoundToInt(depth / 0.12f));
            for (int i = 0; i < xBars; i++)
            {
                float x = center.x - width * 0.5f + i * width / Mathf.Max(1, xBars - 1);
                CreateBox("Grating_Longitudinal_" + i, parent, new Vector3(x, center.y, center.z), new Vector3(0.018f, 0.025f, depth), material);
            }

            for (int i = 0; i < zBars; i++)
            {
                float z = center.z - depth * 0.5f + i * depth / Mathf.Max(1, zBars - 1);
                CreateBox("Grating_Cross_" + i, parent, new Vector3(center.x, center.y + 0.012f, z), new Vector3(width, 0.022f, 0.018f), material);
            }
        }

        private void CreateRailing(Transform parent, Vector3 center, float width, float depth)
        {
            Material material = GetMaterial("White_Safety_Railing", new Color(0.88f, 0.88f, 0.84f, 1f));
            float postHeight = 0.9f;
            Vector3[] postPositions =
            {
                new Vector3(center.x - width * 0.5f, center.y + postHeight * 0.5f, center.z - depth * 0.5f),
                new Vector3(center.x + width * 0.5f, center.y + postHeight * 0.5f, center.z - depth * 0.5f),
                new Vector3(center.x - width * 0.5f, center.y + postHeight * 0.5f, center.z + depth * 0.5f),
                new Vector3(center.x + width * 0.5f, center.y + postHeight * 0.5f, center.z + depth * 0.5f)
            };

            for (int i = 0; i < postPositions.Length; i++)
            {
                CreatePipe("Railing_Post_" + i, parent, postPositions[i], postHeight, 0.025f, PipeAxis.Y, material);
            }

            CreateBox("Railing_North_Rail", parent, new Vector3(center.x, center.y + postHeight, center.z - depth * 0.5f), new Vector3(width, 0.035f, 0.035f), material);
            CreateBox("Railing_South_Rail", parent, new Vector3(center.x, center.y + postHeight, center.z + depth * 0.5f), new Vector3(width, 0.035f, 0.035f), material);
            CreateBox("Railing_West_Rail", parent, new Vector3(center.x - width * 0.5f, center.y + postHeight, center.z), new Vector3(0.035f, 0.035f, depth), material);
            CreateBox("Railing_East_Rail", parent, new Vector3(center.x + width * 0.5f, center.y + postHeight, center.z), new Vector3(0.035f, 0.035f, depth), material);
        }

        private TextMeshPro CreateLabel(string name, Transform parent, Vector3 localPosition, string text, float fontSize, Color color, Vector2 size)
        {
            GameObject labelObject = new GameObject(name);
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one * 0.1f;
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Left;
            label.enableWordWrapping = true;
            label.rectTransform.sizeDelta = size;
            return label;
        }

        private Material GetMaterial(string name, Color color)
        {
            if (_materials.TryGetValue(name, out Material material))
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
            material.name = name;
            SetMaterialColor(material, color);
            _materials.Add(name, material);
            return material;
        }

        private void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private enum RakeMode
        {
            Idle,
            TravelUp,
            DischargePause,
            ReturnDown
        }

        private sealed class ScreenChannelRuntime
        {
            public int index;
            public Transform root;
            public Transform screenRoot;
            public Transform rake;
            public Transform upstreamWater;
            public Transform downstreamWater;
            public Renderer statusLight;
            public Vector3 dischargeWorldPosition;
            public float centerX;
            public float screenZ;
            public float capturedLoadKg;
            public float cycleTimer;
            public float dischargeTimer;
            public float rakeLocalY;
            public float headlossM;
            public float upstreamDepthM;
            public float downstreamDepthM;
            public float motorCurrentA;
            public bool jammed;
            public RakeMode rakeMode;
        }

        private sealed class FlowParticle
        {
            public Transform transform;
            public int channelIndex;
            public float progressM;
            public float laneOffset;
        }

        private sealed class DebrisParticle
        {
            public Transform transform;
            public Renderer renderer;
            public Rigidbody rigidbody;
            public ParticleState state;
            public int channelIndex;
            public float progressM;
            public float localX;
            public float sizeM;
            public float massKg;
            public float seed;
            public float screenLocalY;
            public float droppedAt;
            public bool evaluatedAtScreen;
            public Vector3 rakeOffset;
            public Vector3 angularVelocityDegPerSec;
        }
    }
}
