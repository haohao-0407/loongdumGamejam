Shader "Hidden/Loongdum/VisionRangeMask"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Vision Range Mask"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #include "Assets/Shaders/VisionMaskCommon.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_VisionMaskEnabled - 0.5);

                float mask = EvaluateVisionMask(input.texcoord, SampleSceneDepth(input.texcoord));
                return half4(0, 0, 0, mask);
            }
            ENDHLSL
        }
    }
}
