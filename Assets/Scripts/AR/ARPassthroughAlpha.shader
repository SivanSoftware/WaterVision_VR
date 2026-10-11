Shader "WaterVision/AR/PassthroughAlphaClear"
{
    // Fullscreen pass that writes alpha = 0 into the camera color target WITHOUT touching RGB.
    // Used by ARPassthroughAlphaFeature when AR mode is active so the Meta passthrough Underlay
    // shows through the eye buffer. Opaque scene geometry (pipes) and world-space UI redraw alpha
    // back to 1 in the regions they cover (this pass runs BeforeRenderingOpaques), so only the
    // empty background ends up transparent. The Unity OpenXR projection layer is submitted with
    // XrCompositionLayerFlags.SourceAlpha (OpenXRProjectionLayer.cs:203), so the compositor blends
    // the underlay wherever eye-buffer alpha < 1.
    //
    // ColorMask A  -> only the alpha channel is written; RGB is preserved.
    // Blend One Zero -> straight replace of the (alpha-only) destination with the source alpha (0).
    // ZTest/ZWrite Off + Cull Off -> cheap fullscreen triangle, no depth interaction.
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PassthroughAlphaClear"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One Zero
            ColorMask A

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Provides Vert (fullscreen triangle) + Blit helpers for URP RenderGraph blits.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // RGB is masked out by ColorMask A; only this 0 reaches the alpha channel.
                return half4(0.0h, 0.0h, 0.0h, 0.0h);
            }
            ENDHLSL
        }
    }
}
