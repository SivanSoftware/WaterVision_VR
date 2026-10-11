Shader "WaterVision/AR/PassthroughAlphaClear"
{
    // Writes ALPHA = 0 across the whole frame, RGB untouched (ColorMask A). ARPassthroughAlphaFeature draws this as a
    // fullscreen triangle at BeforeRenderingOpaques in AR mode; opaque geometry + UI then redraw alpha = 1 where they
    // cover, leaving the empty background at alpha 0. The Unity OpenXR projection layer is submitted with SourceAlpha,
    // so the Quest compositor shows the Meta passthrough Underlay wherever the eye-buffer alpha is 0 → real room shows
    // through the gaps, virtual content stays opaque. Uniform output, so triangle orientation/Y-flip is irrelevant.
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PassthroughAlphaClear"
            Cull Off
            ZTest Always
            ZWrite Off
            Blend Off
            ColorMask A   // only the alpha channel is written; RGB is preserved

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // Standard fullscreen triangle from the vertex id (covers the whole viewport with 3 verts).
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(0.0, 0.0, 0.0, 0.0); // ColorMask A → only alpha (0) is written
            }
            ENDHLSL
        }
    }
    Fallback Off
}
