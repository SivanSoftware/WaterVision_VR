using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace WaterVision.AR
{
    /// <summary>
    /// URP ScriptableRendererFeature that makes the Meta passthrough Underlay show through the eye buffer in AR mode.
    ///
    /// WHY: the Unity OpenXR projection (eye) layer is always submitted with XrCompositionLayerFlags.SourceAlpha
    /// (com.unity.xr.openxr .../CompositionLayers/OpenXRProjectionLayer.cs:203), so the Quest compositor blends the
    /// passthrough Underlay wherever the eye-buffer ALPHA &lt; 1. URP normally writes alpha = 1 everywhere, so the
    /// Underlay is fully occluded and AR looks solid black. This feature clears the color target's alpha to 0
    /// BEFORE the opaque pass; opaque pipes and world-space UI then redraw alpha back to 1 in the pixels they cover,
    /// leaving only the empty background transparent → passthrough shows there, geometry stays visible.
    ///
    /// The eye buffer must have an alpha channel for this to work: the active URP asset has HDR off, so the target is
    /// R8G8B8A8 (alpha present). Premultiplied-alpha mode (OVRManager default = true) is the correct/expected mode for
    /// this compositing — do NOT disable it (see Meta's EnableUnpremultipliedAlpha.cs note).
    ///
    /// SAFE OFF-DEVICE: gated by the static <see cref="Active"/> flag, which ARModeController sets true only after it
    /// confirms XR_FB_passthrough and turns AR on. In the editor / desktop the flag stays false → this feature is a
    /// pure no-op (no passes enqueued). Fully reverses on ExitAR (flag false again → eye buffer opaque → normal VR).
    /// Allocation-free per frame: no managed allocations inside RecordRenderGraph.
    /// </summary>
    public class ARPassthroughAlphaFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// Global gate. ARModeController sets this true on EnterAR (passthrough confirmed) and false on ExitAR.
        /// Static so the controller never needs a hard reference to the feature instance living on the URP asset.
        /// </summary>
        public static bool Active;

        // If the editor has "Enter Play Mode Options → Reload Domain" disabled, statics survive between play sessions.
        // Force the gate off at every play-mode start so AR never begins stuck-on after a previous session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive() => Active = false;

        [Tooltip("Shader that writes alpha = 0 with ColorMask A. Assigned by the AR setup tool; defaults to WaterVision/AR/PassthroughAlphaClear.")]
        [SerializeField] private Shader m_Shader;

        private Material m_Material;
        private ClearAlphaPass m_Pass;

        public override void Create()
        {
            if (m_Shader == null)
                m_Shader = Shader.Find("WaterVision/AR/PassthroughAlphaClear");
            if (m_Shader != null && m_Material == null)
                m_Material = CoreUtils.CreateEngineMaterial(m_Shader);

            m_Pass = new ClearAlphaPass(m_Material)
            {
                // Earliest sensible point: clear background alpha to 0 before opaque geometry writes alpha = 1.
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Active || m_Material == null)
                return;

            // Game cameras only — never scene view, material previews, or reflection probes.
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.VR)
                return;

            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (m_Material != null)
            {
                CoreUtils.Destroy(m_Material);
                m_Material = null;
            }
        }

        // -----------------------------------------------------------------------------------------
        private class ClearAlphaPass : ScriptableRenderPass
        {
            private const int k_PassIndex = 0;
            private readonly Material m_Material;

            // Per-pass data for the RenderGraph render function. Reused field reference; no per-frame managed alloc
            // beyond the small PassData the graph pools internally.
            private class PassData { public Material material; }

            public ClearAlphaPass(Material material)
            {
                m_Material = material;
                profilingSampler = new ProfilingSampler("AR Passthrough Alpha Clear");
            }

            // RenderGraph path (URP 17 default). Write-only raster pass into the active color target — we set the
            // attachment with AccessFlags.Write and draw a fullscreen triangle. No source texture is declared, so
            // there is no read/write hazard (unlike a self-blit), and ColorMask A in the shader limits writes to alpha.
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_Material == null)
                    return;

                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return; // raw backbuffer has no usable alpha target here; needs the intermediate color target

                var target = resourceData.activeColorTexture;
                if (!target.IsValid())
                    return;

                using var builder = renderGraph.AddRasterRenderPass<PassData>("AR Passthrough Alpha Clear", out var passData, profilingSampler);
                passData.material = m_Material;
                builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, k_PassIndex, MeshTopology.Triangles, 3);
                });
            }

            // Compatibility (non-RenderGraph) path — only used if the project enables URP Render Compatibility Mode.
#pragma warning disable CS0672 // Execute is obsolete in URP 17 but required for compatibility mode
#pragma warning disable CS0618
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_Material == null)
                    return;

                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    // Fullscreen triangle into the current color target; ColorMask A in the shader limits writes to alpha.
                    cmd.DrawProcedural(Matrix4x4.identity, m_Material, k_PassIndex, MeshTopology.Triangles, 3, 1);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore CS0618
#pragma warning restore CS0672
        }
    }
}
