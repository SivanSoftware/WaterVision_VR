using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace XRMultiplayer
{
    /// <summary>
    /// Builds and animates an educational wastewater treatment plant flow based on the
    /// Beit She'an WWTP report: inlet monitoring, mechanical/primary/biological
    /// treatment, tertiary polishing, disinfection, reuse, and sludge handling.
    /// </summary>
    public class WastewaterTreatmentPlantAnimation : MonoBehaviour
    {
        private const string GeneratedRootName = "__WWTP_GeneratedAnimation";

        [Header("Build")]
        [SerializeField] private bool autoBuildOnAwake = true;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private float stationSpacing = 4.25f;
        [SerializeField] private float stationScale = 1f;
        [SerializeField] private bool showAerialSiteContext = true;

        [Header("Animation")]
        [SerializeField] private float stageDuration = 4f;
        [SerializeField] private float flowSpeed = 2.8f;
        [SerializeField] private int waterMarkerCount = 28;
        [SerializeField] private int sludgeMarkerCount = 10;
        [SerializeField] private int rasMarkerCount = 8;
        [SerializeField] private bool loopAnimation = true;
        [SerializeField] private bool keyboardShortcuts = true;

        [Header("Training Scenario")]
        [SerializeField] private bool failureMode;
        [SerializeField] private Color dirtyWaterColor = new Color(0.45f, 0.34f, 0.19f, 1f);
        [SerializeField] private Color mixedLiquorColor = new Color(0.25f, 0.48f, 0.38f, 1f);
        [SerializeField] private Color cleanWaterColor = new Color(0.16f, 0.58f, 0.95f, 1f);
        [SerializeField] private Color sludgeColor = new Color(0.28f, 0.16f, 0.08f, 1f);
        [SerializeField] private Color alertColor = new Color(1f, 0.18f, 0.12f, 1f);
        [SerializeField] private Color okColor = new Color(0.25f, 1f, 0.35f, 1f);

        private readonly List<StationRuntime> _stations = new List<StationRuntime>();
        private readonly List<Transform> _waterMarkers = new List<Transform>();
        private readonly List<Renderer> _waterMarkerRenderers = new List<Renderer>();
        private readonly List<Transform> _sludgeMarkers = new List<Transform>();
        private readonly List<Renderer> _sludgeMarkerRenderers = new List<Renderer>();
        private readonly List<Transform> _rasMarkers = new List<Transform>();
        private readonly List<Renderer> _rasMarkerRenderers = new List<Renderer>();
        private readonly List<Vector3> _waterRoute = new List<Vector3>();
        private readonly List<Vector3> _sludgeRoute = new List<Vector3>();
        private readonly List<Vector3> _rasRoute = new List<Vector3>();

        private Transform _generatedRoot;
        private TextMeshPro _statusText;
        private Renderer _statusLight;
        private float _waterRouteLength;
        private float _sludgeRouteLength;
        private float _rasRouteLength;
        private float _flowDistance;
        private float _stageTimer;
        private int _stageIndex;
        private bool _isPlaying;

        private void Awake()
        {
            if (autoBuildOnAwake)
            {
                RebuildPreview();
            }

            if (playOnStart)
            {
                StartAnimation();
            }
        }

        private void Update()
        {
            if (keyboardShortcuts)
            {
                HandleKeyboardShortcuts();
            }

            if (!_isPlaying || _stations.Count == 0)
            {
                return;
            }

            _stageTimer += Time.deltaTime;
            _flowDistance += Time.deltaTime * flowSpeed;

            if (_stageTimer >= stageDuration)
            {
                MoveToStage(_stageIndex + 1);
            }

            UpdateFlowMarkers();
            UpdateStageVisuals();
        }

        [ContextMenu("Rebuild WWTP Preview")]
        public void RebuildPreview()
        {
            ClearGenerated();
            BuildAnimation();
            ResetAnimationState();
            UpdateStageVisuals();
            UpdateFlowMarkers();
            UpdateStatusBoard();
        }

        public void StartAnimation()
        {
            if (_stations.Count == 0)
            {
                RebuildPreview();
            }

            _isPlaying = true;
        }

        public void PauseAnimation()
        {
            _isPlaying = false;
        }

        public void ResetAnimation()
        {
            ResetAnimationState();
            UpdateStageVisuals();
            UpdateFlowMarkers();
            UpdateStatusBoard();
            _isPlaying = playOnStart;
        }

        public void NextStage()
        {
            MoveToStage(_stageIndex + 1);
        }

        public void PreviousStage()
        {
            MoveToStage(_stageIndex - 1);
        }

        public void ToggleFailureMode()
        {
            failureMode = !failureMode;
            UpdateStageVisuals();
            UpdateStatusBoard();
        }

        public void SetFailureMode(bool enabled)
        {
            failureMode = enabled;
            UpdateStageVisuals();
            UpdateStatusBoard();
        }

        private void HandleKeyboardShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _isPlaying = !_isPlaying;
            }

            if (Input.GetKeyDown(KeyCode.N))
            {
                NextStage();
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                PreviousStage();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetAnimation();
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                ToggleFailureMode();
            }
        }

        private void ResetAnimationState()
        {
            _stageIndex = 0;
            _stageTimer = 0f;
            _flowDistance = 0f;
            _isPlaying = false;
        }

        private void MoveToStage(int newStageIndex)
        {
            if (_stations.Count == 0)
            {
                return;
            }

            if (newStageIndex >= _stations.Count)
            {
                if (loopAnimation)
                {
                    newStageIndex = 0;
                    _flowDistance = 0f;
                }
                else
                {
                    newStageIndex = _stations.Count - 1;
                    _isPlaying = false;
                }
            }

            if (newStageIndex < 0)
            {
                newStageIndex = _stations.Count - 1;
            }

            _stageIndex = newStageIndex;
            _stageTimer = 0f;
            UpdateStatusBoard();
        }

        private void ClearGenerated()
        {
            _stations.Clear();
            _waterMarkers.Clear();
            _waterMarkerRenderers.Clear();
            _sludgeMarkers.Clear();
            _sludgeMarkerRenderers.Clear();
            _rasMarkers.Clear();
            _rasMarkerRenderers.Clear();
            _waterRoute.Clear();
            _sludgeRoute.Clear();
            _rasRoute.Clear();

            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null)
            {
                DestroyObject(existing.gameObject);
            }

            _generatedRoot = null;
            _statusText = null;
            _statusLight = null;
            _waterRouteLength = 0f;
            _sludgeRouteLength = 0f;
            _rasRouteLength = 0f;
        }

        private void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void BuildAnimation()
        {
            _generatedRoot = new GameObject(GeneratedRootName).transform;
            _generatedRoot.SetParent(transform, false);
            _generatedRoot.localScale = Vector3.one * stationScale;

            Material pipeMaterial = CreateMaterial("WWTP Pipe", new Color(0.18f, 0.21f, 0.24f, 1f));
            Material cleanPipeMaterial = CreateMaterial("WWTP Clean Pipe", cleanWaterColor);
            Material sludgePipeMaterial = CreateMaterial("WWTP Sludge Pipe", sludgeColor);
            Material rasPipeMaterial = CreateMaterial("WWTP RAS Pipe", new Color(0.9f, 0.52f, 0.08f, 1f));
            Material markerMaterial = CreateMaterial("WWTP Marker", cleanWaterColor, true);
            Material sludgeMarkerMaterial = CreateMaterial("WWTP Sludge Marker", sludgeColor, true);
            Material rasMarkerMaterial = CreateMaterial("WWTP RAS Marker", new Color(1f, 0.65f, 0.12f, 1f), true);

            if (showAerialSiteContext)
            {
                CreateAerialSiteContext();
            }

            List<StationSpec> specs = BuildStationSpecs();
            for (int i = 0; i < specs.Count; i++)
            {
                StationRuntime station = CreateStation(specs[i], i);
                _stations.Add(station);

                if (specs[i].IsWaterRoute)
                {
                    _waterRoute.Add(specs[i].Position + Vector3.up * 1.1f);
                }
            }

            CreatePipe("Primary Water Route", _waterRoute, pipeMaterial, 0.12f);
            CreateBranchRoutes(sludgePipeMaterial, rasPipeMaterial);
            CreateFlowMarkers(_waterRoute, _waterMarkers, _waterMarkerRenderers, markerMaterial, waterMarkerCount, 0.26f);
            CreateFlowMarkers(_sludgeRoute, _sludgeMarkers, _sludgeMarkerRenderers, sludgeMarkerMaterial, sludgeMarkerCount, 0.22f);
            CreateFlowMarkers(_rasRoute, _rasMarkers, _rasMarkerRenderers, rasMarkerMaterial, rasMarkerCount, 0.18f);

            _waterRouteLength = GetPathLength(_waterRoute);
            _sludgeRouteLength = GetPathLength(_sludgeRoute);
            _rasRouteLength = GetPathLength(_rasRoute);

            CreateStatusBoard();
            CreateLegend(cleanPipeMaterial, sludgePipeMaterial, rasPipeMaterial);
        }

        private List<StationSpec> BuildStationSpecs()
        {
            List<StationSpec> specs = new List<StationSpec>();
            float mapScale = stationSpacing / 4.25f;

            specs.Add(new StationSpec("inlet", "Inlet Monitoring", "Online pH, EC, and flow checks catch industrial shock before biology is harmed.", "14,000-15,000 m3/day now; design max 18,000 m3/day.", -13.2f * mapScale, 2.7f * mapScale, new Color(0.3f, 0.45f, 0.72f, 1f), StationShape.Sensor, true));
            specs.Add(new StationSpec("screens", "Coarse + Fine Screens", "Mechanical bar screens remove wipes, plastic, rags, and large solids from the municipal line.", "Protects pumps and recirculation equipment.", -11.6f * mapScale, 1.35f * mapScale, new Color(0.42f, 0.42f, 0.44f, 1f), StationShape.Screen, true));
            specs.Add(new StationSpec("grit", "Grit + Sand Removal", "Heavy inert particles settle out before they can abrade pipes or clog sludge lines.", "Removes fast-settling grit and sand.", -9.7f * mapScale, 0.25f * mapScale, new Color(0.75f, 0.64f, 0.38f, 1f), StationShape.Basin, true));
            specs.Add(new StationSpec("primary", "Primary Clarifier", "Physical settling separates primary sludge from primary effluent.", "Primary sludge solids can reach up to 4%.", -7.6f * mapScale, 1.4f * mapScale, new Color(0.55f, 0.46f, 0.34f, 1f), StationShape.Clarifier, true));
            specs.Add(new StationSpec("anaerobic", "Anaerobic Reactor", "No dissolved oxygen. Conditions prepare phosphorus-removing bacteria.", "Phosphorus removal begins here.", -2.35f * mapScale, -4.35f * mapScale, new Color(0.45f, 0.28f, 0.56f, 1f), StationShape.Tank, true));
            specs.Add(new StationSpec("anoxic", "Anoxic Reactor", "Very low oxygen. Denitrification converts nitrate to free nitrogen gas.", "Nitrogen removal stage.", -0.1f * mapScale, -4.35f * mapScale, new Color(0.24f, 0.5f, 0.34f, 1f), StationShape.Tank, true));
            specs.Add(new StationSpec("aerobic", "Aerobic Reactor", "Air supply supports BOD breakdown and nitrification of ammonia.", "Aeration treats organic load and slaughterhouse blood load.", 2.15f * mapScale, -4.35f * mapScale, new Color(0.16f, 0.47f, 0.75f, 1f), StationShape.AeratedTank, true));
            specs.Add(new StationSpec("secondary", "Secondary Clarifier", "Biomass settles. RAS returns active sludge; WAS leaves for sludge treatment.", "WAS is roughly 1% solids before dewatering.", 4.75f * mapScale, 0.65f * mapScale, new Color(0.35f, 0.58f, 0.67f, 1f), StationShape.Clarifier, true));
            specs.Add(new StationSpec("turbidity", "Turbidity Gatekeeper", "Online turbidity at the outlet alerts operators and can stop non-compliant effluent.", "Red alert in failure mode.", 6.9f * mapScale, 2.75f * mapScale, new Color(0.18f, 0.62f, 0.72f, 1f), StationShape.Gate, true));
            specs.Add(new StationSpec("tertiary", "Tertiary Sand Filters", "Four gravity depth-filter modules remove remaining suspended solids.", "Final polishing before disinfection.", 3.35f * mapScale, 4.25f * mapScale, new Color(0.86f, 0.76f, 0.46f, 1f), StationShape.FilterBank, true));
            specs.Add(new StationSpec("chlorine", "Chlorine Contact", "Liquid chlorine is injected and held in contact cells to remove pathogens.", "Contact time: 30-40 minutes.", 8.15f * mapScale, 0.2f * mapScale, new Color(0.38f, 0.74f, 0.92f, 1f), StationShape.ContactChannel, true));
            specs.Add(new StationSpec("reuse", "Effluent Reservoir", "Disinfected effluent moves to agricultural reservoirs for unrestricted irrigation.", "Final product: reused water for agriculture.", 12.6f * mapScale, 0.8f * mapScale, new Color(0.18f, 0.68f, 0.34f, 1f), StationShape.Reservoir, true));
            specs.Add(new StationSpec("centrifuge", "Sludge Centrifuge", "Primary sludge and WAS are dewatered with polymer in a high-speed centrifuge.", "Cake leaves at 20-25% solids.", -7.9f * mapScale, -3.65f * mapScale, new Color(0.48f, 0.31f, 0.17f, 1f), StationShape.Centrifuge, false));
            specs.Add(new StationSpec("compost", "Compost / Fertilizer", "Dewatered cake is mixed with yard waste and matured into agricultural compost.", "Final product: fertilizer-grade compost.", -11.7f * mapScale, -5.9f * mapScale, new Color(0.36f, 0.55f, 0.22f, 1f), StationShape.Field, false));

            return specs;
        }

        private void CreateAerialSiteContext()
        {
            Material fieldMaterial = CreateMaterial("Mat_Aerial_Field", new Color(0.12f, 0.42f, 0.16f, 1f));
            Material soilMaterial = CreateMaterial("Mat_Aerial_Soil", new Color(0.55f, 0.49f, 0.39f, 1f));
            Material roadMaterial = CreateMaterial("Mat_Aerial_Road", new Color(0.68f, 0.64f, 0.55f, 1f));
            Material concreteMaterial = CreateMaterial("Mat_Aerial_Concrete", new Color(0.72f, 0.73f, 0.72f, 1f));
            Material buildingMaterial = CreateMaterial("Mat_Aerial_Building", new Color(0.9f, 0.9f, 0.86f, 1f));
            Material lagoonMaterial = CreateMaterial("Mat_Aerial_Lagoon", new Color(0.22f, 0.32f, 0.22f, 1f));

            CreatePrimitive("Aerial_Ground", PrimitiveType.Cube, _generatedRoot, new Vector3(0f, -0.08f, 0f), new Vector3(32f, 0.06f, 18f), soilMaterial);
            CreatePrimitive("Agricultural_Field_West", PrimitiveType.Cube, _generatedRoot, new Vector3(-16.7f, -0.04f, 0f), new Vector3(5.1f, 0.08f, 18f), fieldMaterial);
            CreatePrimitive("Agricultural_Field_South", PrimitiveType.Cube, _generatedRoot, new Vector3(0f, -0.035f, -8.5f), new Vector3(32f, 0.08f, 1.3f), fieldMaterial);
            CreatePrimitive("Orchard_North", PrimitiveType.Cube, _generatedRoot, new Vector3(2.4f, -0.035f, 7.7f), new Vector3(22f, 0.08f, 1.7f), fieldMaterial);

            CreatePrimitive("North_Service_Road", PrimitiveType.Cube, _generatedRoot, new Vector3(0f, 0f, 6.45f), new Vector3(30f, 0.08f, 0.55f), roadMaterial);
            CreatePrimitive("West_Service_Road", PrimitiveType.Cube, _generatedRoot, new Vector3(-14.05f, 0f, -0.4f), new Vector3(0.6f, 0.08f, 13f), roadMaterial);
            CreatePrimitive("Central_Service_Road", PrimitiveType.Cube, _generatedRoot, new Vector3(-3.1f, 0f, 0.2f), new Vector3(0.55f, 0.08f, 10.5f), roadMaterial);
            CreatePrimitive("South_Service_Road", PrimitiveType.Cube, _generatedRoot, new Vector3(-3.2f, 0f, -6.65f), new Vector3(18.5f, 0.08f, 0.55f), roadMaterial);

            CreatePrimitive("Operations_Building", PrimitiveType.Cube, _generatedRoot, new Vector3(-12.3f, 0.25f, -1.65f), new Vector3(2.3f, 0.5f, 1.45f), buildingMaterial);
            CreatePrimitive("Pretreatment_Building", PrimitiveType.Cube, _generatedRoot, new Vector3(-12.4f, 0.22f, 2.35f), new Vector3(1.45f, 0.44f, 1.1f), buildingMaterial);
            CreatePrimitive("Concrete_Work_Yard", PrimitiveType.Cube, _generatedRoot, new Vector3(-11.5f, -0.02f, -5.05f), new Vector3(4.4f, 0.08f, 2.6f), concreteMaterial);
            CreatePrimitive("Reactor_Pad", PrimitiveType.Cube, _generatedRoot, new Vector3(0.0f, -0.015f, -4.35f), new Vector3(7.3f, 0.08f, 3.0f), concreteMaterial);

            Renderer lagoon = CreatePrimitive("Aerial_Effluent_Reservoir_Footprint", PrimitiveType.Cylinder, _generatedRoot, new Vector3(12.6f, 0.01f, 0.8f), new Vector3(2.45f, 0.04f, 5.1f), lagoonMaterial);
            lagoon.transform.localRotation = Quaternion.identity;

            for (int i = 0; i < 8; i++)
            {
                float x = -4.5f + i * 1.2f;
                CreatePrimitive("Orchard_Row_" + i, PrimitiveType.Cube, _generatedRoot, new Vector3(x, 0.02f, 7.7f), new Vector3(0.18f, 0.08f, 1.45f), CreateMaterial("Mat_Orchard_Row", new Color(0.08f, 0.28f, 0.1f, 1f)));
            }
        }

        private StationRuntime CreateStation(StationSpec spec, int index)
        {
            GameObject rootObject = new GameObject(index.ToString("00") + "_" + spec.Id);
            Transform root = rootObject.transform;
            root.SetParent(_generatedRoot, false);
            root.localPosition = spec.Position;

            Material bodyMaterial = CreateMaterial("Mat_" + spec.Id, spec.Color);
            Renderer bodyRenderer = CreateStationBody(root, spec, bodyMaterial);
            CreateStationDetails(root, spec);

            TextMeshPro label = CreateWorldLabel(
                "Label",
                root,
                new Vector3(0f, 2.05f, -1.18f),
                spec.Title,
                2.15f,
                Color.white,
                new Vector2(3.4f, 1.15f));

            Renderer activeRing = CreatePrimitive(
                "Active_Ring",
                PrimitiveType.Cylinder,
                root,
                new Vector3(0f, 0.03f, 0f),
                new Vector3(2.5f, 0.02f, 2.5f),
                CreateMaterial("Mat_" + spec.Id + "_Active", new Color(1f, 0.86f, 0.2f, 1f), true));
            activeRing.gameObject.SetActive(false);

            ParticleSystem particles = null;
            if (spec.Shape == StationShape.AeratedTank || spec.Shape == StationShape.ContactChannel)
            {
                particles = CreateBubbles(root, spec.Shape == StationShape.AeratedTank ? Color.white : new Color(0.55f, 0.95f, 1f, 1f));
            }

            Renderer statusLight = null;
            if (spec.Shape == StationShape.Sensor || spec.Shape == StationShape.Gate)
            {
                statusLight = CreatePrimitive(
                    "Status_Light",
                    PrimitiveType.Sphere,
                    root,
                    new Vector3(0.95f, 1.7f, -0.55f),
                    Vector3.one * 0.26f,
                    CreateMaterial("Mat_" + spec.Id + "_Light", okColor, true));
            }

            return new StationRuntime(spec, root, bodyRenderer, label, activeRing, particles, statusLight);
        }

        private Renderer CreateStationBody(Transform parent, StationSpec spec, Material material)
        {
            switch (spec.Shape)
            {
                case StationShape.Sensor:
                    CreatePrimitive("Inlet_Channel", PrimitiveType.Cube, parent, new Vector3(0f, 0.32f, 0f), new Vector3(2.4f, 0.42f, 1.0f), material);
                    CreateWaterSurface(parent, "Raw_Inlet_Water", new Vector3(0f, 0.58f, 0f), new Vector3(2.1f, 0.04f, 0.78f), dirtyWaterColor);
                    CreatePipePrimitive("Incoming_Brown_Pipe", parent, new Vector3(-1.35f, 0.85f, 0f), 1.85f, 0.22f, PipeAxis.X, CreateMaterial("Mat_Raw_Pipe", new Color(0.46f, 0.28f, 0.2f, 1f)));
                    CreateValve(parent, new Vector3(-0.5f, 1.02f, 0f), 0.28f);
                    CreateValve(parent, new Vector3(0.22f, 1.02f, 0f), 0.28f);
                    return CreatePrimitive("Sensor_Tower", PrimitiveType.Cube, parent, new Vector3(0.85f, 1.05f, 0f), new Vector3(0.32f, 1.5f, 0.32f), material);

                case StationShape.Screen:
                    Renderer screenBody = CreatePrimitive("Screen_Channel", PrimitiveType.Cube, parent, new Vector3(0f, 0.35f, 0f), new Vector3(2.2f, 0.55f, 1.0f), material);
                    CreateGratingFloor(parent, new Vector3(0f, 1.12f, -0.78f), 2.45f, 1.1f, 9, 5);
                    for (int i = 0; i < 18; i++)
                    {
                        float x = -0.9f + i * 0.105f;
                        CreatePrimitive("Comb_Bar_" + i, PrimitiveType.Cube, parent, new Vector3(x, 0.95f, -0.03f), new Vector3(0.035f, 1.25f, 0.9f), material);
                    }
                    Renderer rake = CreatePrimitive("Inclined_Rake_Conveyor", PrimitiveType.Cube, parent, new Vector3(-0.2f, 1.15f, -0.7f), new Vector3(0.38f, 1.6f, 0.14f), CreateMaterial("Mat_Stainless_Conveyor", new Color(0.74f, 0.73f, 0.68f, 1f)));
                    rake.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
                    CreatePipePrimitive("Screen_Drive_Motor", parent, new Vector3(-0.2f, 1.95f, -0.96f), 0.38f, 0.16f, PipeAxis.X, CreateMaterial("Mat_Blue_Motor", new Color(0.08f, 0.28f, 0.7f, 1f)));
                    CreateGarbageDumpster(parent, new Vector3(1.05f, 0.38f, -1.25f));
                    CreateDebrisPile(parent, new Vector3(1.05f, 0.85f, -1.25f), 0.55f, "Screenings_Pile");
                    return screenBody;

                case StationShape.Basin:
                    Renderer basin = CreatePrimitive("Grit_Basin", PrimitiveType.Cube, parent, new Vector3(0f, 0.42f, 0f), new Vector3(2.45f, 0.7f, 1.2f), material);
                    CreateWaterSurface(parent, "Grit_Water_Surface", new Vector3(0f, 0.8f, 0f), new Vector3(2.2f, 0.035f, 0.95f), dirtyWaterColor);
                    CreatePrimitive("Settled_Grit", PrimitiveType.Cube, parent, new Vector3(0f, 0.18f, 0f), new Vector3(1.8f, 0.12f, 0.72f), CreateMaterial("Mat_Settled_Grit", new Color(0.54f, 0.42f, 0.22f, 1f)));
                    CreatePipePrimitive("Grit_Screw_Conveyor", parent, new Vector3(0.0f, 1.05f, -0.62f), 2.1f, 0.11f, PipeAxis.X, CreateMaterial("Mat_Grit_Screw", new Color(0.68f, 0.66f, 0.58f, 1f)));
                    CreateDebrisPile(parent, new Vector3(0.84f, 0.72f, -0.82f), 0.34f, "Grit_Heap");
                    return basin;

                case StationShape.Clarifier:
                    if (spec.Id == "secondary")
                    {
                        Renderer firstClarifier = CreatePrimitive("Secondary_Clarifier_A", PrimitiveType.Cylinder, parent, new Vector3(-0.72f, 0.45f, -0.2f), new Vector3(1.28f, 0.35f, 1.28f), material);
                        CreatePrimitive("Secondary_Clarifier_B", PrimitiveType.Cylinder, parent, new Vector3(0.78f, 0.45f, 0.25f), new Vector3(1.28f, 0.35f, 1.28f), material);
                        CreateWaterSurface(parent, "Secondary_Surface_A", new Vector3(-0.72f, 0.67f, -0.2f), new Vector3(1.08f, 0.035f, 1.08f), cleanWaterColor, PrimitiveType.Cylinder);
                        CreateWaterSurface(parent, "Secondary_Surface_B", new Vector3(0.78f, 0.67f, 0.25f), new Vector3(1.08f, 0.035f, 1.08f), cleanWaterColor, PrimitiveType.Cylinder);
                        CreateClarifierBridge(parent, new Vector3(-0.72f, 0.9f, -0.2f), 1.28f, "Bridge_A");
                        CreateClarifierBridge(parent, new Vector3(0.78f, 0.9f, 0.25f), 1.28f, "Bridge_B");
                        CreatePrimitive("Sludge_Blanket_A", PrimitiveType.Cylinder, parent, new Vector3(-0.72f, 0.18f, -0.2f), new Vector3(0.95f, 0.08f, 0.95f), CreateMaterial("Mat_Sludge_Blanket", sludgeColor));
                        CreatePrimitive("Sludge_Blanket_B", PrimitiveType.Cylinder, parent, new Vector3(0.78f, 0.18f, 0.25f), new Vector3(0.95f, 0.08f, 0.95f), CreateMaterial("Mat_Sludge_Blanket", sludgeColor));
                        return firstClarifier;
                    }

                    Renderer clarifier = CreatePrimitive("Clarifier_Basin", PrimitiveType.Cylinder, parent, new Vector3(0f, 0.45f, 0f), new Vector3(1.9f, 0.35f, 1.9f), material);
                    CreateWaterSurface(parent, "Primary_Surface", new Vector3(0f, 0.68f, 0f), new Vector3(1.55f, 0.04f, 1.55f), dirtyWaterColor, PrimitiveType.Cylinder);
                    CreateClarifierBridge(parent, new Vector3(0f, 0.95f, 0f), 1.9f, "Primary_Rotating_Bridge");
                    CreatePrimitive("Sludge_Blanket", PrimitiveType.Cylinder, parent, new Vector3(0f, 0.18f, 0f), new Vector3(1.45f, 0.08f, 1.45f), CreateMaterial("Mat_Sludge_Blanket", sludgeColor));
                    return clarifier;

                case StationShape.Tank:
                    Renderer tank = CreatePrimitive("Process_Tank", PrimitiveType.Cube, parent, new Vector3(0f, 0.6f, 0f), new Vector3(2.5f, 1.0f, 1.25f), material);
                    CreateWaterSurface(parent, "Biological_Water_Surface", new Vector3(0f, 1.13f, 0f), new Vector3(2.2f, 0.035f, 0.95f), mixedLiquorColor);
                    CreateRailing(parent, new Vector3(0f, 1.42f, 0f), 2.7f, 1.45f);
                    return tank;

                case StationShape.AeratedTank:
                    Renderer aerated = CreatePrimitive("Aeration_Tank", PrimitiveType.Cube, parent, new Vector3(0f, 0.6f, 0f), new Vector3(2.5f, 1.0f, 1.25f), material);
                    CreateWaterSurface(parent, "Aeration_Mixed_Liquor", new Vector3(0f, 1.13f, 0f), new Vector3(2.2f, 0.035f, 0.95f), mixedLiquorColor);
                    CreateRailing(parent, new Vector3(0f, 1.42f, 0f), 2.7f, 1.45f);
                    for (int i = 0; i < 5; i++)
                    {
                        CreatePrimitive("Air_Diffuser_" + i, PrimitiveType.Sphere, parent, new Vector3(-0.8f + i * 0.4f, 0.2f, 0.35f), Vector3.one * 0.12f, CreateMaterial("Mat_Air_Diffuser", Color.white, true));
                    }
                    for (int i = 0; i < 7; i++)
                    {
                        CreatePrimitive("Foam_Patch_" + i, PrimitiveType.Sphere, parent, new Vector3(-0.9f + i * 0.3f, 1.22f, 0.25f), new Vector3(0.18f, 0.025f, 0.08f), CreateMaterial("Mat_Aeration_Foam", new Color(0.95f, 0.9f, 0.78f, 1f)));
                    }
                    return aerated;

                case StationShape.Gate:
                    Renderer gate = CreatePrimitive("Gate_Frame", PrimitiveType.Cube, parent, new Vector3(0f, 0.95f, 0f), new Vector3(1.85f, 1.55f, 0.18f), material);
                    CreatePrimitive("Turbidity_Probe", PrimitiveType.Cube, parent, new Vector3(-0.65f, 0.85f, -0.55f), new Vector3(0.18f, 1.1f, 0.18f), material);
                    return gate;

                case StationShape.FilterBank:
                    Renderer firstFilter = null;
                    for (int i = 0; i < 4; i++)
                    {
                        Renderer filter = CreatePrimitive("Sand_Filter_" + (i + 1), PrimitiveType.Cube, parent, new Vector3(-0.8f + i * 0.53f, 0.55f, 0f), new Vector3(0.42f, 0.9f, 1.15f), material);
                        CreateWaterSurface(parent, "Filter_Black_Surface_" + (i + 1), new Vector3(-0.8f + i * 0.53f, 1.02f, 0f), new Vector3(0.34f, 0.03f, 0.92f), new Color(0.02f, 0.025f, 0.025f, 1f));
                        if (firstFilter == null)
                        {
                            firstFilter = filter;
                        }
                    }
                    CreateRailing(parent, new Vector3(0f, 1.35f, 0f), 2.7f, 1.45f);
                    CreatePipePrimitive("Purple_Filter_Inlet", parent, new Vector3(-0.6f, 1.14f, -0.8f), 1.5f, 0.08f, PipeAxis.X, CreateMaterial("Mat_Purple_Filter_Pipe", new Color(0.55f, 0.22f, 0.65f, 1f)));
                    return firstFilter;

                case StationShape.ContactChannel:
                    Renderer channel = CreatePrimitive("Contact_Channel", PrimitiveType.Cube, parent, new Vector3(0f, 0.35f, 0f), new Vector3(2.5f, 0.55f, 1.25f), material);
                    CreateWaterSurface(parent, "Chlorine_Channel_Surface", new Vector3(0f, 0.65f, 0f), new Vector3(2.2f, 0.035f, 0.95f), cleanWaterColor);
                    for (int i = 0; i < 5; i++)
                    {
                        CreatePrimitive("Baffle_" + i, PrimitiveType.Cube, parent, new Vector3(-0.85f + i * 0.42f, 0.72f, 0f), new Vector3(0.06f, 0.58f, 1.16f), CreateMaterial("Mat_Baffle", new Color(0.8f, 0.95f, 1f, 1f)));
                    }
                    CreateRailing(parent, new Vector3(0f, 1.08f, 0f), 2.7f, 1.45f);
                    CreatePrimitive("Chlorine_Dose", PrimitiveType.Sphere, parent, new Vector3(-1f, 1.1f, -0.55f), Vector3.one * 0.24f, CreateMaterial("Mat_Chlorine", new Color(0.65f, 1f, 0.35f, 1f), true));
                    return channel;

                case StationShape.Field:
                    Renderer field = CreatePrimitive("Field", PrimitiveType.Cube, parent, new Vector3(0f, 0.08f, 0f), new Vector3(2.4f, 0.1f, 1.35f), material);
                    for (int i = 0; i < 5; i++)
                    {
                        CreatePrimitive("Crop_Row_" + i, PrimitiveType.Cube, parent, new Vector3(-0.9f + i * 0.45f, 0.25f, 0f), new Vector3(0.08f, 0.32f, 1.2f), CreateMaterial("Mat_Crop_Row", new Color(0.08f, 0.48f, 0.18f, 1f)));
                    }
                    return field;

                case StationShape.Reservoir:
                    Renderer reservoir = CreatePrimitive("Effluent_Reservoir", PrimitiveType.Cylinder, parent, new Vector3(0f, 0.1f, 0f), new Vector3(1.35f, 0.12f, 2.75f), material);
                    CreatePrimitive("Reservoir_Water_Surface", PrimitiveType.Cylinder, parent, new Vector3(0f, 0.18f, 0f), new Vector3(1.15f, 0.04f, 2.45f), CreateMaterial("Mat_Reservoir_Surface", cleanWaterColor));
                    CreatePrimitive("Reservoir_Liner", PrimitiveType.Cylinder, parent, new Vector3(0f, 0.11f, 0f), new Vector3(1.5f, 0.035f, 2.95f), CreateMaterial("Mat_Black_Liner", new Color(0.015f, 0.015f, 0.018f, 1f)));
                    return reservoir;

                case StationShape.Centrifuge:
                    Renderer centrifuge = CreatePrimitive("Centrifuge_Drum", PrimitiveType.Cylinder, parent, new Vector3(0f, 0.75f, 0f), new Vector3(1.65f, 0.42f, 1.65f), material);
                    centrifuge.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    CreatePrimitive("Polymer_Tank", PrimitiveType.Cylinder, parent, new Vector3(-0.95f, 0.62f, -0.55f), new Vector3(0.42f, 0.42f, 0.42f), CreateMaterial("Mat_Polymer", new Color(0.9f, 0.7f, 0.95f, 1f)));
                    CreatePipePrimitive("Dewatered_Cake_Chute", parent, new Vector3(0.95f, 0.42f, -0.42f), 0.9f, 0.14f, PipeAxis.X, CreateMaterial("Mat_Cake_Chute", new Color(0.62f, 0.58f, 0.48f, 1f)));
                    CreateGarbageDumpster(parent, new Vector3(1.55f, 0.18f, -0.45f));
                    CreateDebrisPile(parent, new Vector3(1.55f, 0.66f, -0.45f), 0.42f, "Dewatered_Cake_Pile");
                    return centrifuge;
            }

            return CreatePrimitive("Station_Body", PrimitiveType.Cube, parent, new Vector3(0f, 0.6f, 0f), new Vector3(2.2f, 1f, 1.2f), material);
        }

        private void CreateStationDetails(Transform root, StationSpec spec)
        {
            CreateWorldLabel(
                "Metric",
                root,
                new Vector3(0f, 1.55f, 1.02f),
                spec.Metric,
                1.15f,
                new Color(0.92f, 0.96f, 1f, 1f),
                new Vector2(3.1f, 0.8f));
        }

        private Renderer CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;

            Renderer renderer = primitive.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private Renderer CreatePipePrimitive(string name, Transform parent, Vector3 localPosition, float length, float radius, PipeAxis axis, Material material)
        {
            Renderer pipe = CreatePrimitive(name, PrimitiveType.Cylinder, parent, localPosition, new Vector3(radius, length * 0.5f, radius), material);
            if (axis == PipeAxis.X)
            {
                pipe.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else if (axis == PipeAxis.Z)
            {
                pipe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            return pipe;
        }

        private Renderer CreateWaterSurface(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, PrimitiveType type = PrimitiveType.Cube)
        {
            return CreatePrimitive(name, type, parent, localPosition, localScale, CreateMaterial("Mat_" + name, color));
        }

        private void CreateValve(Transform parent, Vector3 localPosition, float size)
        {
            Material valveMaterial = CreateMaterial("Mat_Blue_Valve", new Color(0.05f, 0.27f, 0.72f, 1f));
            CreatePrimitive("Valve_Body", PrimitiveType.Cube, parent, localPosition, new Vector3(size * 0.8f, size * 0.75f, size * 0.55f), valveMaterial);
            Renderer wheel = CreatePrimitive("Valve_Handwheel", PrimitiveType.Cylinder, parent, localPosition + new Vector3(0f, size * 0.72f, 0f), new Vector3(size * 0.85f, 0.025f, size * 0.85f), valveMaterial);
            wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreatePrimitive("Valve_Stem", PrimitiveType.Cylinder, parent, localPosition + new Vector3(0f, size * 0.42f, 0f), new Vector3(size * 0.08f, size * 0.42f, size * 0.08f), valveMaterial);
        }

        private void CreateGratingFloor(Transform parent, Vector3 center, float width, float depth, int xBars, int zBars)
        {
            Material gratingMaterial = CreateMaterial("Mat_Metal_Grating", new Color(0.62f, 0.62f, 0.58f, 1f));
            for (int i = 0; i < xBars; i++)
            {
                float x = center.x - width * 0.5f + width * i / Mathf.Max(1, xBars - 1);
                CreatePrimitive("Grating_X_" + i, PrimitiveType.Cube, parent, new Vector3(x, center.y, center.z), new Vector3(0.035f, 0.035f, depth), gratingMaterial);
            }

            for (int i = 0; i < zBars; i++)
            {
                float z = center.z - depth * 0.5f + depth * i / Mathf.Max(1, zBars - 1);
                CreatePrimitive("Grating_Z_" + i, PrimitiveType.Cube, parent, new Vector3(center.x, center.y + 0.01f, z), new Vector3(width, 0.035f, 0.035f), gratingMaterial);
            }
        }

        private void CreateRailing(Transform parent, Vector3 center, float width, float depth)
        {
            Material railMaterial = CreateMaterial("Mat_White_Railing", new Color(0.86f, 0.86f, 0.82f, 1f));
            float y = center.y;
            Vector3[] corners =
            {
                new Vector3(center.x - width * 0.5f, y, center.z - depth * 0.5f),
                new Vector3(center.x + width * 0.5f, y, center.z - depth * 0.5f),
                new Vector3(center.x - width * 0.5f, y, center.z + depth * 0.5f),
                new Vector3(center.x + width * 0.5f, y, center.z + depth * 0.5f)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                CreatePrimitive("Railing_Post_" + i, PrimitiveType.Cylinder, parent, corners[i] + Vector3.up * 0.28f, new Vector3(0.035f, 0.28f, 0.035f), railMaterial);
            }

            CreatePrimitive("Railing_North", PrimitiveType.Cube, parent, new Vector3(center.x, y + 0.56f, center.z - depth * 0.5f), new Vector3(width, 0.035f, 0.035f), railMaterial);
            CreatePrimitive("Railing_South", PrimitiveType.Cube, parent, new Vector3(center.x, y + 0.56f, center.z + depth * 0.5f), new Vector3(width, 0.035f, 0.035f), railMaterial);
            CreatePrimitive("Railing_West", PrimitiveType.Cube, parent, new Vector3(center.x - width * 0.5f, y + 0.56f, center.z), new Vector3(0.035f, 0.035f, depth), railMaterial);
            CreatePrimitive("Railing_East", PrimitiveType.Cube, parent, new Vector3(center.x + width * 0.5f, y + 0.56f, center.z), new Vector3(0.035f, 0.035f, depth), railMaterial);
        }

        private void CreateGarbageDumpster(Transform parent, Vector3 localPosition)
        {
            Material dumpsterMaterial = CreateMaterial("Mat_Yellow_Dumpster", new Color(1f, 0.73f, 0.02f, 1f));
            CreatePrimitive("Yellow_Dumpster_Body", PrimitiveType.Cube, parent, localPosition, new Vector3(0.9f, 0.55f, 0.65f), dumpsterMaterial);
            CreatePrimitive("Yellow_Dumpster_Rim", PrimitiveType.Cube, parent, localPosition + new Vector3(0f, 0.32f, 0f), new Vector3(1.0f, 0.08f, 0.75f), dumpsterMaterial);
        }

        private void CreateDebrisPile(Transform parent, Vector3 localPosition, float radius, string name)
        {
            Material debrisMaterial = CreateMaterial("Mat_" + name, new Color(0.29f, 0.25f, 0.2f, 1f));
            for (int i = 0; i < 11; i++)
            {
                float angle = i * 47f * Mathf.Deg2Rad;
                float distance = radius * (0.18f + (i % 4) * 0.18f);
                Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0.03f * (i % 3), Mathf.Sin(angle) * distance);
                CreatePrimitive(name + "_Clump_" + i, PrimitiveType.Sphere, parent, localPosition + offset, Vector3.one * radius * (0.24f + 0.04f * (i % 3)), debrisMaterial);
            }
        }

        private void CreateClarifierBridge(Transform parent, Vector3 center, float diameter, string name)
        {
            Material bridgeMaterial = CreateMaterial("Mat_" + name, new Color(0.76f, 0.77f, 0.74f, 1f));
            CreatePrimitive(name + "_Hub", PrimitiveType.Cylinder, parent, center, new Vector3(0.22f, 0.08f, 0.22f), bridgeMaterial);
            CreatePrimitive(name + "_Walkway", PrimitiveType.Cube, parent, center + new Vector3(0f, 0.05f, 0f), new Vector3(diameter * 1.1f, 0.055f, 0.13f), bridgeMaterial);
            CreatePrimitive(name + "_Scraper_Arm", PrimitiveType.Cube, parent, center + new Vector3(diameter * 0.23f, -0.45f, 0.08f), new Vector3(diameter * 0.55f, 0.035f, 0.05f), CreateMaterial("Mat_Scraper_Arm", new Color(0.22f, 0.23f, 0.24f, 1f)));
        }

        private TextMeshPro CreateWorldLabel(string name, Transform parent, Vector3 localPosition, string text, float fontSize, Color color, Vector2 size)
        {
            GameObject labelObject = new GameObject(name);
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one * 0.12f;

            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.sizeDelta = size;
            return label;
        }

        private ParticleSystem CreateBubbles(Transform parent, Color color)
        {
            GameObject particleObject = new GameObject("Process_Bubbles");
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = new Vector3(0f, 0.25f, 0f);

            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = 1.25f;
            main.startSpeed = 0.8f;
            main.startSize = 0.08f;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 28f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.6f, 0.05f, 0.85f);

            particles.Stop();
            return particles;
        }

        private void CreatePipe(string name, List<Vector3> points, Material material, float width)
        {
            if (points.Count < 2)
            {
                return;
            }

            GameObject pipeObject = new GameObject(name);
            pipeObject.transform.SetParent(_generatedRoot, false);

            LineRenderer line = pipeObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            line.widthMultiplier = width;
            line.numCapVertices = 8;
            line.numCornerVertices = 8;
            line.sharedMaterial = material;
        }

        private void CreateBranchRoutes(Material sludgePipeMaterial, Material rasPipeMaterial)
        {
            StationRuntime primary = FindStation("primary");
            StationRuntime secondary = FindStation("secondary");
            StationRuntime anaerobic = FindStation("anaerobic");
            StationRuntime centrifuge = FindStation("centrifuge");
            StationRuntime compost = FindStation("compost");

            if (primary != null && secondary != null && centrifuge != null && compost != null)
            {
                _sludgeRoute.Add(primary.Spec.Position + new Vector3(0f, 0.65f, -1.15f));
                _sludgeRoute.Add(primary.Spec.Position + new Vector3(0f, 0.65f, -3.2f));
                _sludgeRoute.Add(secondary.Spec.Position + new Vector3(0f, 0.65f, -3.2f));
                _sludgeRoute.Add(centrifuge.Spec.Position + Vector3.up * 1.1f);
                _sludgeRoute.Add(compost.Spec.Position + Vector3.up * 0.65f);
                CreatePipe("Sludge WAS/Primary Route", _sludgeRoute, sludgePipeMaterial, 0.1f);
            }

            if (secondary != null && anaerobic != null)
            {
                _rasRoute.Add(secondary.Spec.Position + new Vector3(0f, 1f, 1.1f));
                _rasRoute.Add(secondary.Spec.Position + new Vector3(0f, 1f, 2.7f));
                _rasRoute.Add(anaerobic.Spec.Position + new Vector3(0f, 1f, 2.7f));
                _rasRoute.Add(anaerobic.Spec.Position + new Vector3(0f, 1f, 1.1f));
                CreatePipe("RAS Return Route", _rasRoute, rasPipeMaterial, 0.08f);
            }
        }

        private void CreateFlowMarkers(List<Vector3> route, List<Transform> markerList, List<Renderer> rendererList, Material material, int count, float markerSize)
        {
            if (route.Count < 2 || count <= 0)
            {
                return;
            }

            GameObject markerRoot = new GameObject(material.name + "_Markers");
            markerRoot.transform.SetParent(_generatedRoot, false);

            for (int i = 0; i < count; i++)
            {
                Renderer marker = CreatePrimitive("Marker_" + i, PrimitiveType.Sphere, markerRoot.transform, Vector3.zero, Vector3.one * markerSize, new Material(material));
                markerList.Add(marker.transform);
                rendererList.Add(marker);
            }
        }

        private void UpdateFlowMarkers()
        {
            UpdateRouteMarkers(_waterRoute, _waterRouteLength, _waterMarkers, _waterMarkerRenderers, true);
            UpdateRouteMarkers(_sludgeRoute, _sludgeRouteLength, _sludgeMarkers, _sludgeMarkerRenderers, false);
            UpdateRouteMarkers(_rasRoute, _rasRouteLength, _rasMarkers, _rasMarkerRenderers, false);
        }

        private void UpdateRouteMarkers(List<Vector3> route, float routeLength, List<Transform> markers, List<Renderer> renderers, bool isWater)
        {
            if (route.Count < 2 || routeLength <= 0f || markers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < markers.Count; i++)
            {
                float spacing = routeLength / markers.Count;
                float distance = Mathf.Repeat(_flowDistance + i * spacing, routeLength);
                float normalized;
                Vector3 localPosition = EvaluatePath(route, distance, out normalized);

                bool blockedByFailure = isWater && failureMode && normalized > 0.74f;
                markers[i].gameObject.SetActive(!blockedByFailure);
                markers[i].localPosition = localPosition;

                if (renderers.Count > i)
                {
                    Color color = isWater ? GetWaterColor(normalized) : renderers[i].sharedMaterial.color;
                    SetMaterialColor(renderers[i].sharedMaterial, failureMode && isWater && normalized > 0.66f ? alertColor : color);
                }
            }
        }

        private Color GetWaterColor(float normalized)
        {
            if (normalized < 0.35f)
            {
                return Color.Lerp(dirtyWaterColor, mixedLiquorColor, normalized / 0.35f);
            }

            return Color.Lerp(mixedLiquorColor, cleanWaterColor, Mathf.InverseLerp(0.35f, 1f, normalized));
        }

        private float GetPathLength(List<Vector3> route)
        {
            float length = 0f;
            for (int i = 1; i < route.Count; i++)
            {
                length += Vector3.Distance(route[i - 1], route[i]);
            }
            return length;
        }

        private Vector3 EvaluatePath(List<Vector3> route, float distance, out float normalized)
        {
            float totalLength = GetPathLength(route);
            normalized = totalLength <= 0f ? 0f : Mathf.Clamp01(distance / totalLength);

            float walked = 0f;
            for (int i = 1; i < route.Count; i++)
            {
                float segmentLength = Vector3.Distance(route[i - 1], route[i]);
                if (walked + segmentLength >= distance)
                {
                    float segmentT = Mathf.InverseLerp(walked, walked + segmentLength, distance);
                    return Vector3.Lerp(route[i - 1], route[i], segmentT);
                }

                walked += segmentLength;
            }

            return route[route.Count - 1];
        }

        private void UpdateStageVisuals()
        {
            for (int i = 0; i < _stations.Count; i++)
            {
                StationRuntime station = _stations[i];
                bool active = i == _stageIndex;
                float pulse = active ? 1f + Mathf.Sin(Time.time * 5f) * 0.08f : 1f;

                if (station.ActiveRing != null)
                {
                    station.ActiveRing.gameObject.SetActive(active);
                    station.ActiveRing.transform.localScale = new Vector3(2.65f * pulse, 0.02f, 2.65f * pulse);
                }

                if (station.Body != null)
                {
                    SetMaterialColor(station.Body.sharedMaterial, active ? Brighten(station.Spec.Color, 1.35f) : station.Spec.Color);
                }

                if (station.Particles != null)
                {
                    if (active && !station.Particles.isPlaying)
                    {
                        station.Particles.Play();
                    }
                    else if (!active && station.Particles.isPlaying)
                    {
                        station.Particles.Stop();
                    }
                }

                if (station.StatusLight != null)
                {
                    bool isAlert = failureMode && (station.Spec.Id == "inlet" || station.Spec.Id == "turbidity");
                    SetMaterialColor(station.StatusLight.sharedMaterial, isAlert ? alertColor : okColor);
                }
            }

            if (_statusLight != null)
            {
                bool gateBlocked = failureMode && _stageIndex >= GetStationIndex("turbidity");
                SetMaterialColor(_statusLight.sharedMaterial, gateBlocked ? alertColor : okColor);
            }
        }

        private Color Brighten(Color color, float multiplier)
        {
            return new Color(
                Mathf.Clamp01(color.r * multiplier),
                Mathf.Clamp01(color.g * multiplier),
                Mathf.Clamp01(color.b * multiplier),
                color.a);
        }

        private void CreateStatusBoard()
        {
            GameObject boardRoot = new GameObject("WWTP_Status_Board");
            boardRoot.transform.SetParent(_generatedRoot, false);
            boardRoot.transform.localPosition = new Vector3(0f, 3.25f, -8.15f);

            CreatePrimitive("Board_Backplate", PrimitiveType.Cube, boardRoot.transform, Vector3.zero, new Vector3(9.8f, 2.45f, 0.12f), CreateMaterial("Mat_Board", new Color(0.05f, 0.07f, 0.09f, 1f)));
            _statusText = CreateWorldLabel("Board_Text", boardRoot.transform, new Vector3(0f, 0.1f, -0.08f), "", 2.2f, Color.white, new Vector2(8.8f, 2.0f));
            _statusLight = CreatePrimitive("Board_Status_Light", PrimitiveType.Sphere, boardRoot.transform, new Vector3(-4.6f, 0.95f, -0.16f), Vector3.one * 0.25f, CreateMaterial("Mat_Board_Status", okColor, true));
        }

        private void CreateLegend(Material cleanPipeMaterial, Material sludgePipeMaterial, Material rasPipeMaterial)
        {
            GameObject legend = new GameObject("Legend");
            legend.transform.SetParent(_generatedRoot, false);
            legend.transform.localPosition = new Vector3(0f, 0.35f, 7.15f);

            CreateLegendItem(legend.transform, -2.8f, cleanPipeMaterial, "Water path");
            CreateLegendItem(legend.transform, 0f, sludgePipeMaterial, "Sludge path");
            CreateLegendItem(legend.transform, 2.8f, rasPipeMaterial, "RAS return");
        }

        private void CreateLegendItem(Transform parent, float x, Material material, string text)
        {
            CreatePrimitive("Legend_Line_" + text, PrimitiveType.Cube, parent, new Vector3(x - 0.55f, 0f, 0f), new Vector3(0.8f, 0.08f, 0.08f), material);
            CreateWorldLabel("Legend_Text_" + text, parent, new Vector3(x + 0.38f, 0f, -0.08f), text, 1.15f, Color.white, new Vector2(2f, 0.45f));
        }

        private void UpdateStatusBoard()
        {
            if (_statusText == null || _stations.Count == 0)
            {
                return;
            }

            StationRuntime station = _stations[Mathf.Clamp(_stageIndex, 0, _stations.Count - 1)];
            string mode = failureMode ? "FAULT DEMO" : "GUIDED WWTP FLOW";
            string alert = failureMode && (station.Spec.Id == "inlet" || station.Spec.Id == "turbidity")
                ? "\nAlert: abnormal pH/EC or turbidity. Gate blocks outgoing effluent."
                : "";

            _statusText.text =
                mode + "   Step " + (_stageIndex + 1) + "/" + _stations.Count +
                "\n" + station.Spec.Title +
                "\n" + station.Spec.Description +
                "\n" + station.Spec.Metric + alert;
        }

        private StationRuntime FindStation(string id)
        {
            for (int i = 0; i < _stations.Count; i++)
            {
                if (_stations[i].Spec.Id == id)
                {
                    return _stations[i];
                }
            }

            return null;
        }

        private int GetStationIndex(string id)
        {
            for (int i = 0; i < _stations.Count; i++)
            {
                if (_stations[i].Spec.Id == id)
                {
                    return i;
                }
            }

            return _stations.Count - 1;
        }

        private Material CreateMaterial(string name, Color color, bool emissive = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.name = name;
            SetMaterialColor(material, color);

            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.4f);
            }

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

        private enum PipeAxis
        {
            X,
            Y,
            Z
        }

        private enum StationShape
        {
            Sensor,
            Screen,
            Basin,
            Clarifier,
            Tank,
            AeratedTank,
            Gate,
            FilterBank,
            ContactChannel,
            Field,
            Reservoir,
            Centrifuge
        }

        private sealed class StationSpec
        {
            public readonly string Id;
            public readonly string Title;
            public readonly string Description;
            public readonly string Metric;
            public readonly Vector3 Position;
            public readonly Color Color;
            public readonly StationShape Shape;
            public readonly bool IsWaterRoute;

            public StationSpec(string id, string title, string description, string metric, float x, float z, Color color, StationShape shape, bool isWaterRoute)
            {
                Id = id;
                Title = title;
                Description = description;
                Metric = metric;
                Position = new Vector3(x, 0f, z);
                Color = color;
                Shape = shape;
                IsWaterRoute = isWaterRoute;
            }
        }

        private sealed class StationRuntime
        {
            public readonly StationSpec Spec;
            public readonly Transform Root;
            public readonly Renderer Body;
            public readonly TextMeshPro Label;
            public readonly Renderer ActiveRing;
            public readonly ParticleSystem Particles;
            public readonly Renderer StatusLight;

            public StationRuntime(StationSpec spec, Transform root, Renderer body, TextMeshPro label, Renderer activeRing, ParticleSystem particles, Renderer statusLight)
            {
                Spec = spec;
                Root = root;
                Body = body;
                Label = label;
                ActiveRing = activeRing;
                Particles = particles;
                StatusLight = statusLight;
            }
        }
    }
}
