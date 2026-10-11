using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace XRMultiplayer
{
    /// <summary>
    /// Enhanced water filtration simulation with visual feedback and educational content.
    /// Shows the process of water flowing through multiple filtration layers.
    /// </summary>
    public class WaterProcessSim : MonoBehaviour
    {
        [Header("Player References")]
        [Tooltip("The XR Origin/Player rig that will move through the filtration process")]
        [SerializeField] private GameObject playerRig;
        
        [Tooltip("Starting point inside the water layer (InsideWaterPoint)")]
        [SerializeField] private Transform insideWaterPoint;
        
        [Tooltip("Return position after simulation ends (usually on the floor near the tank)")]
        [SerializeField] private Transform returnPoint;
        
        [Tooltip("Back button position - where player teleports when pressing Back")]
        [SerializeField] private Transform backButtonPosition;

        [Header("Layer Zone Markers")]
        [Tooltip("Transform marking the water layer boundary")]
        [SerializeField] private Transform waterLayerZone;
        
        [Tooltip("Transform marking the sand layer boundary")]
        [SerializeField] private Transform sandLayerZone;
        
        [Tooltip("Transform marking the gravel layer boundary")]
        [SerializeField] private Transform gravelLayerZone;
        
        [Tooltip("Transform marking the clean water zone at bottom")]
        [SerializeField] private Transform cleanWaterZone;

        [Header("Particle Systems")]
        [Tooltip("Large dirt particles - stopped in water layer")]
        [SerializeField] private ParticleSystem largeDirtParticles;
        
        [Tooltip("Medium dirt particles - stopped in sand layer")]
        [SerializeField] private ParticleSystem mediumDirtParticles;
        
        [Tooltip("Small dirt particles - stopped in gravel layer")]
        [SerializeField] private ParticleSystem smallDirtParticles;

        [Header("Layer Visual Effects - Optional")]
        [Tooltip("Ambient particles in sand layer to show filtration")]
        [SerializeField] private ParticleSystem sandLayerAmbient;
        
        [Tooltip("Ambient particles in gravel layer")]
        [SerializeField] private ParticleSystem gravelLayerAmbient;
        
        [Tooltip("Bubbles or clean water particles in clean zone")]
        [SerializeField] private ParticleSystem cleanWaterBubbles;
        
        [Tooltip("Water flow particles to show liquid movement")]
        [SerializeField] private ParticleSystem waterFlowParticles;
        
        [Tooltip("Particle system that follows player - creates water wave aura effect")]
        [SerializeField] private ParticleSystem playerWaterAura;
        
        [Tooltip("Multiply particle emission rates by this factor (1 = normal, 3 = triple particles)")]
        [SerializeField] private float particleIntensityMultiplier = 3f;
        
        [Tooltip("Distance of water aura from player (radius)")]
        [SerializeField] private float auraRadius = 1.5f;
        
        [Tooltip("Height offset for water aura above player")]
        [SerializeField] private float auraHeightOffset = 0f;

        [Header("Visual Effects")]
        [Tooltip("Material of the water to change color (assign water material)")]
        [SerializeField] private Material waterMaterial;
        
        [Tooltip("Reference to the Renderer component of water layer (optional - for direct material access)")]
        [SerializeField] private Renderer waterLayerRenderer;
        
        [Tooltip("Renderers for each water layer - assign all 4 layers for individual effects")]
        [SerializeField] private Renderer waterLayer;
        [SerializeField] private Renderer sandLayerRenderer;
        [SerializeField] private Renderer gravelLayerRenderer;
        [SerializeField] private Renderer cleanLayerRenderer;
        
        [Tooltip("Color of dirty water at start")]
        [SerializeField] private Color dirtyWaterColor = new Color(0.6f, 0.5f, 0.4f, 0.8f);
        
        [Tooltip("Color of clean water at end")]
        [SerializeField] private Color cleanWaterColor = new Color(0.2f, 0.5f, 0.9f, 0.8f);
        
        [Tooltip("Sand layer color")]
        [SerializeField] private Color sandLayerColor = new Color(0.9f, 0.8f, 0.5f, 0.7f);
        
        [Tooltip("Gravel layer color")]
        [SerializeField] private Color gravelLayerColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        
        [Tooltip("Enable animated water surface waves (requires shader support)")]
        [SerializeField] private bool enableWaterSurfaceWaves = true;
        
        [Tooltip("Wave speed multiplier")]
        [SerializeField] private float waveSpeed = 0.5f;
        
        [Tooltip("Wave height/strength")]
        [SerializeField] private float waveStrength = 0.1f;

        [Header("Immersive Water Effects")]
        [Tooltip("Main camera to apply post-processing effects (usually inside XR Origin)")]
        [SerializeField] private Camera playerCamera;
        
        [Tooltip("Fog color when inside water - matches water layer color")]
        [SerializeField] private Color waterFogColor = new Color(0.4f, 0.6f, 0.8f, 1f);
        
        [Tooltip("Fog density inside water (0.05-0.2 recommended)")]
        [SerializeField] private float waterFogDensity = 0.15f;
        
        [Tooltip("Enable underwater wave distortion effect")]
        [SerializeField] private bool enableWaveDistortion = true;
        
        [Tooltip("Vignette effect intensity (darkens edges, 0-1)")]
        [SerializeField] private float vignetteIntensity = 0.3f;

        [Header("UI Elements")]
        [Tooltip("Text showing current layer name (e.g., 'Dirty Water')")]
        [SerializeField] private TextMeshProUGUI layerNameText;
        
        [Tooltip("Text showing what's happening in current layer")]
        [SerializeField] private TextMeshProUGUI descriptionText;
        
        [Tooltip("Fill image showing dirt level (assign Image with Fill type)")]
        [SerializeField] private Image dirtLevelBar;

        [Header("Simulation Settings")]
        [Tooltip("Target scale for microscopic effect (0.03 = 3% of original size)")]
        [SerializeField] private float microScale = 0.03f;
        
        [Tooltip("Speed of downward movement through layers")]
        [SerializeField] private float moveDownSpeed = 0.2f;
        
        [Tooltip("Time to pause in each layer for observation")]
        [SerializeField] private float layerPauseTime = 3f;
        
        [Tooltip("Rate at which dirt level decreases (visual feedback)")]
        [SerializeField] private float particleDecayRate = 0.3f;

        // Private state variables
        private bool _isSimulating = false;
        private Vector3 _originalScale;
        private float _currentDirtLevel = 1f; // 1 = very dirty, 0 = clean
        
        // Store original rendering settings to restore after simulation
        private bool _originalFogEnabled;
        private Color _originalFogColor;
        private float _originalFogDensity;
        private FogMode _originalFogMode;
        private Color _originalCameraBackgroundColor;
        
        // Camera wave effect
        private Quaternion _originalCameraRotation;
        private float _waveTime = 0f;
        private float _waterWaveTime = 0f;

        // Educational content for each layer
        private readonly string[] _layerNames = { 
            "Dirty Water", 
            "Sand Layer", 
            "Gravel Layer", 
            "Clean Water" 
        };
        
        private readonly string[] _layerDescriptions = {
            "Water contains dirt, mud and large particles",
            "Sand absorbs medium particles and bacteria",
            "Gravel filters large particles and stabilizes flow",
            "Water is clean and filtered! Process complete"
        };

        private void Start()
        {
            // Cache original player scale for restoration later
            if (playerRig != null)
            {
                _originalScale = playerRig.transform.localScale;
            }
            
            // Find player camera if not assigned
            if (playerCamera == null && playerRig != null)
            {
                playerCamera = playerRig.GetComponentInChildren<Camera>();
            }
            
            // Store original camera rotation for wave effect
            if (playerCamera != null)
            {
                _originalCameraRotation = playerCamera.transform.localRotation;
                _originalCameraBackgroundColor = playerCamera.backgroundColor;
            }
            
            // Store original rendering settings
            _originalFogEnabled = RenderSettings.fog;
            _originalFogColor = RenderSettings.fogColor;
            _originalFogDensity = RenderSettings.fogDensity;
            _originalFogMode = RenderSettings.fogMode;

            // Initialize water as dirty
            if (waterMaterial != null)
            {
                waterMaterial.color = dirtyWaterColor;
            }
            else if (waterLayerRenderer != null)
            {
                // Alternative: get material from renderer
                waterMaterial = waterLayerRenderer.material;
                waterMaterial.color = dirtyWaterColor;
            }
            
            // Apply particle intensity multiplier to all particle systems
            ApplyParticleIntensity();
            
            // Set initial layer colors to make them visually distinct
            SetupLayerVisuals();
        }
        
        private void Update()
        {
            // Apply underwater wave distortion effect when inside water
            if (_isSimulating && enableWaveDistortion && playerCamera != null)
            {
                ApplyWaveDistortion();
            }
            
            // Update player water aura position to follow player
            if (_isSimulating && playerWaterAura != null && playerRig != null)
            {
                UpdateWaterAuraPosition();
            }
            
            // Animate water surface waves
            if (enableWaterSurfaceWaves && waterMaterial != null)
            {
                AnimateWaterSurface();
            }
        }

        /// <summary>
        /// Public method called by the Start button in VR
        /// </summary>
        public void StartInternalProcess()
        {
            // Prevent multiple simultaneous simulations
            if (!_isSimulating)
            {
                StartCoroutine(ProcessSequence());
            }
        }
        
        /// <summary>
        /// Public method called by the Back button - teleports player to starting position
        /// </summary>
        public void OnBackButtonPressed()
        {
            // Stop any running simulation
            if (_isSimulating)
            {
                StopAllCoroutines();
                _isSimulating = false;
                
                // Restore original settings
                RestoreAllSettings();
            }
            
            // Teleport to back button position
            if (backButtonPosition != null)
            {
                StartCoroutine(TeleportToPosition(backButtonPosition.position));
            }
            else
            {
                Debug.LogWarning("Back Button Position not assigned! Please assign it in the Inspector.");
            }
        }

        /// <summary>
        /// Main simulation sequence coroutine
        /// </summary>
        private IEnumerator ProcessSequence()
        {
            _isSimulating = true;
            _currentDirtLevel = 1f;

            // Start water flow effect throughout simulation
            if (waterFlowParticles != null) waterFlowParticles.Play();
            
            // Start player water aura effect
            if (playerWaterAura != null) playerWaterAura.Play();

            // Start all particle systems at full intensity
            if (largeDirtParticles != null) largeDirtParticles.Play();
            if (mediumDirtParticles != null) mediumDirtParticles.Play();
            if (smallDirtParticles != null) smallDirtParticles.Play();

            // Phase 1: Teleport player inside and shrink to microscopic size
            yield return StartCoroutine(TeleportAndShrink());

            // Phase 2: Flow through each filtration layer
            yield return StartCoroutine(FlowThroughWaterLayer());
            yield return StartCoroutine(FlowThroughSandLayer());
            yield return StartCoroutine(FlowThroughGravelLayer());
            yield return StartCoroutine(ReachCleanWater());

            // Phase 3: Restore player to normal
            yield return StartCoroutine(RestorePlayer());

            _isSimulating = false;
        }

        /// <summary>
        /// Teleport player into water and gradually shrink to microscopic size
        /// </summary>
        private IEnumerator TeleportAndShrink()
        {
            // Get CharacterController and XRBodyTransformer references
            CharacterController cc = playerRig.GetComponent<CharacterController>();
            var bodyTransformer = playerRig.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.XRBodyTransformer>();
            
            // Disable XRBodyTransformer to prevent it from trying to move the disabled CharacterController
            if (bodyTransformer != null)
            {
                bodyTransformer.enabled = false;
            }
            
            // Disable CharacterController to allow teleportation
            if (cc != null) cc.enabled = false;

            // Teleport to starting position inside water
            playerRig.transform.position = insideWaterPoint.position;
            Physics.SyncTransforms();

            // Re-enable CharacterController
            if (cc != null) cc.enabled = true;
            
            // Re-enable XRBodyTransformer after teleportation complete
            if (bodyTransformer != null)
            {
                bodyTransformer.enabled = true;
            }
            
            // Start underwater visual effects
            StartCoroutine(FadeInUnderwaterEffect());

            // Gradually shrink to microscopic size
            float t = 0;
            while (t < 1.0f)
            {
                t += Time.deltaTime * 0.5f; // Shrink speed multiplier
                playerRig.transform.localScale = Vector3.Lerp(_originalScale, Vector3.one * microScale, t);
                
                // Update UI feedback
                UpdateDescriptionText("Shrinking to microscopic size...");
                
                yield return null;
            }

            yield return new WaitForSeconds(1f); // Pause before starting flow
        }

        /// <summary>
        /// Flow through the water layer - large particles are filtered here
        /// </summary>
        private IEnumerator FlowThroughWaterLayer()
        {
            UpdateLayerInfo(0); // Display water layer info
            
            // Set dirty water fog color
            UpdateUnderwaterColor(dirtyWaterColor);
            
            float timeInLayer = 0;
            // Continue until pause time elapsed or reached sand layer
            while (timeInLayer < layerPauseTime && 
                   playerRig.transform.position.y > sandLayerZone.position.y)
            {
                // Move player downward at normal speed
                playerRig.transform.Translate(Vector3.down * moveDownSpeed * Time.deltaTime, Space.World);
                timeInLayer += Time.deltaTime;
                
                // Gradually reduce dirt level (large particles being filtered)
                UpdateDirtLevel(0.85f);
                
                yield return null;
            }

            // Large particles are now filtered out - stop their particle system
            if (largeDirtParticles != null) largeDirtParticles.Stop();
            
            yield return new WaitForSeconds(0.5f); // Brief pause to show effect
        }

        /// <summary>
        /// Flow through sand layer - medium particles and bacteria are filtered
        /// </summary>
        private IEnumerator FlowThroughSandLayer()
        {
            UpdateLayerInfo(1); // Display sand layer info
            
            // Make sand layer more visible
            HighlightLayer(sandLayerRenderer);
            
            // Activate sand layer ambient particles to show we're in sand
            if (sandLayerAmbient != null) sandLayerAmbient.Play();
            
            float timeInLayer = 0;
            // Continue until pause time elapsed or reached gravel layer
            while (timeInLayer < layerPauseTime && 
                   playerRig.transform.position.y > gravelLayerZone.position.y)
            {
                // Move slower through sand (70% speed) to show filtration
                playerRig.transform.Translate(Vector3.down * moveDownSpeed * 0.7f * Time.deltaTime, Space.World);
                timeInLayer += Time.deltaTime;
                
                // Sand absorbs medium-sized particles
                UpdateDirtLevel(0.5f);
                
                // Gradually change fog to sandy color
                Color sandyFog = Color.Lerp(waterFogColor, sandLayerColor, timeInLayer / layerPauseTime);
                RenderSettings.fogColor = sandyFog;
                
                yield return null;
            }

            // Medium particles are now filtered out
            if (mediumDirtParticles != null) mediumDirtParticles.Stop();
            
            yield return new WaitForSeconds(1.5f); // Longer pause to observe sand filtration
        }

        /// <summary>
        /// Flow through gravel layer - final filtration of small particles
        /// </summary>
        private IEnumerator FlowThroughGravelLayer()
        {
            UpdateLayerInfo(2); // Display gravel layer info
            
            // Make gravel layer more visible
            HighlightLayer(gravelLayerRenderer);
            
            // Stop sand ambient and start gravel ambient
            if (sandLayerAmbient != null) sandLayerAmbient.Stop();
            if (gravelLayerAmbient != null) gravelLayerAmbient.Play();
            
            float timeInLayer = 0;
            // Continue until pause time elapsed or reached clean water zone
            while (timeInLayer < layerPauseTime && 
                   playerRig.transform.position.y > cleanWaterZone.position.y)
            {
                // Move even slower through gravel (50% speed) for final filtration
                playerRig.transform.Translate(Vector3.down * moveDownSpeed * 0.5f * Time.deltaTime, Space.World);
                timeInLayer += Time.deltaTime;
                
                // Gravel filters remaining small particles
                UpdateDirtLevel(0.1f);
                
                // Gradually change fog to gravel color
                Color gravelFog = Color.Lerp(sandLayerColor, gravelLayerColor, timeInLayer / layerPauseTime);
                RenderSettings.fogColor = gravelFog;
                
                yield return null;
            }

            // All particles now filtered out
            if (smallDirtParticles != null) smallDirtParticles.Stop();
            
            yield return new WaitForSeconds(1.5f); // Pause to observe final filtration
        }

        /// <summary>
        /// Reach clean water zone - show the final clean result
        /// </summary>
        private IEnumerator ReachCleanWater()
        {
            UpdateLayerInfo(3); // Display clean water info
            UpdateDirtLevel(0f); // Dirt level at zero
            
            // Stop gravel ambient and start clean water effects
            if (gravelLayerAmbient != null) gravelLayerAmbient.Stop();
            if (cleanWaterBubbles != null) cleanWaterBubbles.Play();
            
            // Gradually change water color AND underwater effect from dirty to clean
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 0.5f;
                
                // Interpolate water color
                Color currentWaterColor = Color.Lerp(dirtyWaterColor, cleanWaterColor, t);
                
                if (waterMaterial != null)
                {
                    waterMaterial.color = currentWaterColor;
                }
                
                // Update underwater fog/color effect
                UpdateUnderwaterColor(currentWaterColor);
                
                yield return null;
            }

            yield return new WaitForSeconds(3f); // Let player observe the clean water
        }

        /// <summary>
        /// Restore player to original size and position with smooth animations
        /// </summary>
        private IEnumerator RestorePlayer()
        {
            // Show completion message
            UpdateLayerInfo(3);
            UpdateDescriptionText("Simulation complete! Returning to ground...");
            
            yield return new WaitForSeconds(2f); // Let player read the message
            
            // Start fading out underwater effects (runs in parallel)
            StartCoroutine(FadeOutUnderwaterEffect());
            
            // Stop all ambient effects
            if (cleanWaterBubbles != null) cleanWaterBubbles.Stop();
            if (waterFlowParticles != null) waterFlowParticles.Stop();
            if (playerWaterAura != null) playerWaterAura.Stop();
            
            // Determine return position early
            Vector3 finalPosition;
            if (returnPoint != null)
            {
                finalPosition = returnPoint.position;
            }
            else
            {
                finalPosition = insideWaterPoint.position + Vector3.right * 2f;
                finalPosition.y = 0.1f;
            }
            
            // Store current state
            Vector3 currentScale = playerRig.transform.localScale;
            Vector3 currentPosition = playerRig.transform.position;
            
            // Disable both CharacterController and XRBodyTransformer for smooth movement
            CharacterController cc = playerRig.GetComponent<CharacterController>();
            var bodyTransformer = playerRig.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.XRBodyTransformer>();
            
            if (bodyTransformer != null)
            {
                bodyTransformer.enabled = false;
            }
            
            if (cc != null) cc.enabled = false;
            
            // Phase 1: Move upward while growing (exit water)
            float exitDuration = 2f;
            float t = 0;
            
            UpdateDescriptionText("Rising to surface...");
            
            Vector3 surfacePosition = currentPosition;
            surfacePosition.y = waterLayerZone != null ? waterLayerZone.position.y + 1f : currentPosition.y + 2f;
            
            while (t < 1.0f)
            {
                t += Time.deltaTime / exitDuration;
                float smoothT = Mathf.SmoothStep(0, 1, t); // Smooth acceleration/deceleration
                
                // Grow back to normal size
                playerRig.transform.localScale = Vector3.Lerp(currentScale, _originalScale, smoothT);
                
                // Move upward smoothly
                playerRig.transform.position = Vector3.Lerp(currentPosition, surfacePosition, smoothT);
                
                yield return null;
            }
            
            yield return new WaitForSeconds(0.5f); // Brief pause at surface
            
            // Phase 2: Smooth transition to ground position
            UpdateDescriptionText("Returning to ground...");
            
            float returnDuration = 2.5f;
            t = 0;
            currentPosition = playerRig.transform.position;
            
            while (t < 1.0f)
            {
                t += Time.deltaTime / returnDuration;
                
                // Use ease-in-out curve for ultra-smooth motion
                float smoothT = t * t * (3f - 2f * t); // Smoothstep formula
                
                // Smoothly move to final position with a slight arc (more natural)
                Vector3 arcPosition = Vector3.Lerp(currentPosition, finalPosition, smoothT);
                
                // Add slight upward arc in the middle of the journey
                float arcHeight = Mathf.Sin(smoothT * Mathf.PI) * 0.5f;
                arcPosition.y += arcHeight;
                
                playerRig.transform.position = arcPosition;
                
                yield return null;
            }
            
            // Ensure exact final position
            playerRig.transform.position = finalPosition;
            Physics.SyncTransforms();
            
            // Gentle landing animation (slight bounce)
            yield return StartCoroutine(LandingBounce());
            
            // Re-enable both CharacterController and XRBodyTransformer
            if (cc != null) cc.enabled = true;
            
            if (bodyTransformer != null)
            {
                bodyTransformer.enabled = true;
            }
            
            // Reset water material to dirty state for next simulation
            if (waterMaterial != null)
            {
                waterMaterial.color = dirtyWaterColor;
            }
            
            // Fade out UI
            yield return StartCoroutine(FadeOutUI());
        }
        
        /// <summary>
        /// Create a subtle bounce effect when landing on ground
        /// </summary>
        private IEnumerator LandingBounce()
        {
            Vector3 landPosition = playerRig.transform.position;
            float bounceHeight = 0.1f;
            float bounceDuration = 0.3f;
            
            // Bounce up
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime / bounceDuration;
                float bounce = Mathf.Sin(t * Mathf.PI) * bounceHeight;
                
                Vector3 bouncePosition = landPosition;
                bouncePosition.y += bounce;
                playerRig.transform.position = bouncePosition;
                
                yield return null;
            }
            
            // Settle at final position
            playerRig.transform.position = landPosition;
        }
        
        /// <summary>
        /// Smoothly fade out the UI elements
        /// </summary>
        private IEnumerator FadeOutUI()
        {
            float duration = 1f;
            float t = 0;
            
            // Store original alpha values (if using CanvasGroup, otherwise just clear)
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                
                // For now, just clear at the end
                // If you want smooth fade, add CanvasGroup component to UI
                yield return null;
            }
            
            // Clear UI
            if (layerNameText != null) layerNameText.text = "";
            if (descriptionText != null) descriptionText.text = "";
            if (dirtLevelBar != null) dirtLevelBar.fillAmount = 0;
        }

        /// <summary>
        /// Update UI to show current layer information
        /// </summary>
        private void UpdateLayerInfo(int layerIndex)
        {
            if (layerNameText != null)
            {
                layerNameText.text = _layerNames[layerIndex];
            }
            
            if (descriptionText != null)
            {
                descriptionText.text = _layerDescriptions[layerIndex];
            }
        }

        /// <summary>
        /// Smoothly update dirt level visual indicator
        /// </summary>
        private void UpdateDirtLevel(float targetLevel)
        {
            // Lerp to target level smoothly
            _currentDirtLevel = Mathf.Lerp(_currentDirtLevel, targetLevel, Time.deltaTime * particleDecayRate);
            
            if (dirtLevelBar != null)
            {
                // Update fill amount
                dirtLevelBar.fillAmount = _currentDirtLevel;
                // Change color from clean to dirty based on level
                dirtLevelBar.color = Color.Lerp(cleanWaterColor, dirtyWaterColor, _currentDirtLevel);
            }
        }

        /// <summary>
        /// Update description text UI element
        /// </summary>
        private void UpdateDescriptionText(string text)
        {
            if (descriptionText != null)
            {
                descriptionText.text = text;
            }
        }

        /// <summary>
        /// Increase particle emission rates for more visible effect
        /// </summary>
        private void ApplyParticleIntensity()
        {
            if (particleIntensityMultiplier <= 1f) return;
            
            // Multiply emission rates for dirt particles
            MultiplyEmissionRate(largeDirtParticles, particleIntensityMultiplier);
            MultiplyEmissionRate(mediumDirtParticles, particleIntensityMultiplier);
            MultiplyEmissionRate(smallDirtParticles, particleIntensityMultiplier);
            
            // Multiply ambient effects
            MultiplyEmissionRate(sandLayerAmbient, particleIntensityMultiplier * 0.5f);
            MultiplyEmissionRate(gravelLayerAmbient, particleIntensityMultiplier * 0.5f);
            MultiplyEmissionRate(cleanWaterBubbles, particleIntensityMultiplier * 2f);
            MultiplyEmissionRate(waterFlowParticles, particleIntensityMultiplier * 1.5f);
            MultiplyEmissionRate(playerWaterAura, particleIntensityMultiplier * 2f);
        }
        
        /// <summary>
        /// Helper method to multiply particle system emission rate
        /// </summary>
        private void MultiplyEmissionRate(ParticleSystem ps, float multiplier)
        {
            if (ps == null) return;
            
            var emission = ps.emission;
            var rateOverTime = emission.rateOverTime;
            rateOverTime.constant *= multiplier;
            emission.rateOverTime = rateOverTime;
        }

        /// <summary>
        /// Draw zone boundaries in Scene view for debugging
        /// </summary>
        private void OnDrawGizmos()
        {
            // Draw water layer zone in blue
            if (waterLayerZone != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(waterLayerZone.position, Vector3.one * 0.5f);
            }
            
            // Draw sand layer zone in yellow
            if (sandLayerZone != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(sandLayerZone.position, Vector3.one * 0.5f);
            }
            
            // Draw gravel layer zone in gray
            if (gravelLayerZone != null)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawWireCube(gravelLayerZone.position, Vector3.one * 0.5f);
            }
            
            // Draw clean water zone in cyan
            if (cleanWaterZone != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(cleanWaterZone.position, Vector3.one * 0.5f);
            }
        }
        
        // ==================== UNDERWATER VISUAL EFFECTS ====================
        
        /// <summary>
        /// Gradually fade in underwater visual effects (fog, camera tint)
        /// </summary>
        private IEnumerator FadeInUnderwaterEffect()
        {
            float t = 0;
            float duration = 1.5f;
            
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                
                // Enable and configure fog
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = Mathf.Lerp(0, waterFogDensity, t);
                RenderSettings.fogColor = Color.Lerp(_originalFogColor, waterFogColor, t);
                
                // Tint camera background to water color
                if (playerCamera != null)
                {
                    playerCamera.backgroundColor = Color.Lerp(_originalCameraBackgroundColor, waterFogColor, t);
                }
                
                yield return null;
            }
        }
        
        /// <summary>
        /// Gradually fade out underwater effects when exiting water
        /// </summary>
        private IEnumerator FadeOutUnderwaterEffect()
        {
            float t = 0;
            float duration = 1f;
            
            Color currentFogColor = RenderSettings.fogColor;
            float currentFogDensity = RenderSettings.fogDensity;
            Color currentCameraColor = playerCamera != null ? playerCamera.backgroundColor : Color.black;
            
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                
                // Restore fog settings
                RenderSettings.fogDensity = Mathf.Lerp(currentFogDensity, _originalFogDensity, t);
                RenderSettings.fogColor = Color.Lerp(currentFogColor, _originalFogColor, t);
                
                // Restore camera background
                if (playerCamera != null)
                {
                    playerCamera.backgroundColor = Color.Lerp(currentCameraColor, _originalCameraBackgroundColor, t);
                }
                
                yield return null;
            }
            
            // Fully restore original settings
            RenderSettings.fog = _originalFogEnabled;
            RenderSettings.fogMode = _originalFogMode;
            RenderSettings.fogDensity = _originalFogDensity;
            RenderSettings.fogColor = _originalFogColor;
            
            // Restore camera rotation (stop wave effect)
            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = _originalCameraRotation;
            }
        }
        
        /// <summary>
        /// Update underwater color effect in real-time (changes as water gets cleaner)
        /// </summary>
        private void UpdateUnderwaterColor(Color targetColor)
        {
            // Make fog color slightly desaturated version of water color
            Color fogColor = new Color(
                targetColor.r * 0.7f + 0.3f,
                targetColor.g * 0.7f + 0.3f,
                targetColor.b * 0.7f + 0.3f,
                1f
            );
            
            RenderSettings.fogColor = fogColor;
            
            if (playerCamera != null)
            {
                playerCamera.backgroundColor = fogColor;
            }
        }
        
        /// <summary>
        /// Apply subtle wave distortion to camera to simulate underwater movement
        /// Uses sine waves to create gentle swaying motion
        /// </summary>
        private void ApplyWaveDistortion()
        {
            _waveTime += Time.deltaTime;
            
            // Multiple sine waves at different frequencies create natural water motion
            float waveX = Mathf.Sin(_waveTime * 1.5f) * 0.015f;          // Slow horizontal sway
            float waveY = Mathf.Sin(_waveTime * 2.0f) * 0.01f;           // Medium vertical bob
            float waveZ = Mathf.Sin(_waveTime * 1.2f) * 0.012f;          // Slow roll
            
            // Additional layer of complexity with different frequency
            waveX += Mathf.Sin(_waveTime * 3.0f) * 0.005f;
            waveY += Mathf.Cos(_waveTime * 2.5f) * 0.008f;
            
            // Convert to degrees and apply to camera rotation
            Quaternion waveRotation = Quaternion.Euler(
                waveY * Mathf.Rad2Deg, 
                waveX * Mathf.Rad2Deg, 
                waveZ * Mathf.Rad2Deg
            );
            
            // Blend with original rotation
            playerCamera.transform.localRotation = _originalCameraRotation * waveRotation;
        }
        
        /// <summary>
        /// Update water aura position to follow player and create flowing wave effect
        /// </summary>
        private void UpdateWaterAuraPosition()
        {
            if (playerWaterAura == null || playerRig == null) return;
            
            // Position the aura at player's position with offset
            Vector3 auraPosition = playerRig.transform.position;
            auraPosition.y += auraHeightOffset;
            
            playerWaterAura.transform.position = auraPosition;
            
            // Optional: Rotate aura slowly for more dynamic effect
            float rotationSpeed = 20f; // degrees per second
            playerWaterAura.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            
            // Dynamically adjust shape radius based on player scale for microscopic effect
            var shape = playerWaterAura.shape;
            float scaleFactor = playerRig.transform.localScale.x / _originalScale.x;
            shape.radius = auraRadius * scaleFactor;
        }
        
        /// <summary>
        /// Animate water surface with flowing wave effect using material properties
        /// Creates dynamic water movement visible on water layers
        /// </summary>
        private void AnimateWaterSurface()
        {
            _waterWaveTime += Time.deltaTime * waveSpeed;
            
            // Animate ALL water layers for better wave effect
            AnimateLayerWaves(waterLayer, 1.0f);
            AnimateLayerWaves(sandLayerRenderer, 0.7f);
            AnimateLayerWaves(gravelLayerRenderer, 0.5f);
            AnimateLayerWaves(cleanLayerRenderer, 0.3f);
        }
        
        /// <summary>
        /// Animate individual layer with wave motion
        /// </summary>
        private void AnimateLayerWaves(Renderer layerRenderer, float intensity)
        {
            if (layerRenderer == null) return;
            
            Material layerMat = layerRenderer.material;
            if (layerMat == null) return;
            
            // Create flowing wave pattern with texture offset
            if (layerMat.HasProperty("_MainTex"))
            {
                Vector2 offset = new Vector2(
                    Mathf.Sin(_waterWaveTime * intensity) * 0.2f,
                    _waterWaveTime * 0.1f * intensity
                );
                layerMat.SetTextureOffset("_MainTex", offset);
            }
            
            // Add pulsing transparency for wave depth effect
            if (layerMat.HasProperty("_Color"))
            {
                Color baseColor = layerMat.color;
                float wave = Mathf.Sin(_waterWaveTime * 2f * intensity) * 0.1f;
                
                Color wavedColor = new Color(
                    baseColor.r,
                    baseColor.g,
                    baseColor.b,
                    Mathf.Clamp01(baseColor.a + wave)
                );
                layerMat.color = wavedColor;
            }
        }
        
        /// <summary>
        /// Setup visual distinction between layers at start
        /// </summary>
        private void SetupLayerVisuals()
        {
            // Set distinct colors for each layer
            if (waterLayer != null && waterLayer.material != null)
            {
                waterLayer.material.color = dirtyWaterColor;
            }
            
            if (sandLayerRenderer != null && sandLayerRenderer.material != null)
            {
                sandLayerRenderer.material.color = sandLayerColor;
            }
            
            if (gravelLayerRenderer != null && gravelLayerRenderer.material != null)
            {
                gravelLayerRenderer.material.color = gravelLayerColor;
            }
            
            if (cleanLayerRenderer != null && cleanLayerRenderer.material != null)
            {
                cleanLayerRenderer.material.color = cleanWaterColor;
            }
        }
        
        /// <summary>
        /// Highlight a specific layer when player enters it
        /// </summary>
        private void HighlightLayer(Renderer layer)
        {
            if (layer == null || layer.material == null) return;
            
            // Increase brightness/emission of current layer
            Color currentColor = layer.material.color;
            Color brightColor = new Color(
                Mathf.Min(currentColor.r * 1.3f, 1f),
                Mathf.Min(currentColor.g * 1.3f, 1f),
                Mathf.Min(currentColor.b * 1.3f, 1f),
                currentColor.a
            );
            
            layer.material.color = brightColor;
            
            // Add emission if supported
            if (layer.material.HasProperty("_EmissionColor"))
            {
                layer.material.EnableKeyword("_EMISSION");
                layer.material.SetColor("_EmissionColor", brightColor * 0.5f);
            }
        }
        
        // ==================== BACK BUTTON TELEPORT ====================
        
        /// <summary>
        /// Smoothly teleport player to a specific position (used by Back button)
        /// </summary>
        private IEnumerator TeleportToPosition(Vector3 targetPosition)
        {
            // Get references
            CharacterController cc = playerRig.GetComponent<CharacterController>();
            var bodyTransformer = playerRig.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.XRBodyTransformer>();
            
            // Disable components for smooth teleportation
            if (bodyTransformer != null) bodyTransformer.enabled = false;
            if (cc != null) cc.enabled = false;
            
            Vector3 startPosition = playerRig.transform.position;
            float duration = 0.5f; // Quick teleport
            float t = 0;
            
            // Smooth movement to target position
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float smoothT = Mathf.SmoothStep(0, 1, t);
                
                playerRig.transform.position = Vector3.Lerp(startPosition, targetPosition, smoothT);
                
                yield return null;
            }
            
            // Ensure exact position
            playerRig.transform.position = targetPosition;
            Physics.SyncTransforms();
            
            // Re-enable components
            if (cc != null) cc.enabled = true;
            if (bodyTransformer != null) bodyTransformer.enabled = true;
        }
        
        /// <summary>
        /// Restore all visual effects and settings to original state
        /// Called when simulation is interrupted or ended
        /// </summary>
        private void RestoreAllSettings()
        {
            // Restore fog settings
            RenderSettings.fog = _originalFogEnabled;
            RenderSettings.fogMode = _originalFogMode;
            RenderSettings.fogDensity = _originalFogDensity;
            RenderSettings.fogColor = _originalFogColor;
            
            // Restore camera
            if (playerCamera != null)
            {
                playerCamera.backgroundColor = _originalCameraBackgroundColor;
                playerCamera.transform.localRotation = _originalCameraRotation;
            }
            
            // Restore player scale
            if (playerRig != null)
            {
                playerRig.transform.localScale = _originalScale;
            }
            
            // Stop all particle systems
            if (largeDirtParticles != null) largeDirtParticles.Stop();
            if (mediumDirtParticles != null) mediumDirtParticles.Stop();
            if (smallDirtParticles != null) smallDirtParticles.Stop();
            if (sandLayerAmbient != null) sandLayerAmbient.Stop();
            if (gravelLayerAmbient != null) gravelLayerAmbient.Stop();
            if (cleanWaterBubbles != null) cleanWaterBubbles.Stop();
            if (waterFlowParticles != null) waterFlowParticles.Stop();
            if (playerWaterAura != null) playerWaterAura.Stop();
            
            // Reset water color
            if (waterMaterial != null)
            {
                waterMaterial.color = dirtyWaterColor;
            }
            
            // Clear UI
            if (layerNameText != null) layerNameText.text = "";
            if (descriptionText != null) descriptionText.text = "";
            if (dirtLevelBar != null) dirtLevelBar.fillAmount = 0;
        }
    }
}