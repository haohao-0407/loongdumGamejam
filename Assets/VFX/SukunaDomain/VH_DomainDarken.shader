// 宿傩·领域展开 —— 全屏压暗层
//
// 为什么是「相机下的全屏四边形」而不是 Canvas + Image：
//   1) ScreenSpaceOverlay 画布永远画在相机 3D 输出**之上** ⇒ 会把刀光一起压暗，
//      和参考图正好相反（参考图是「背景压暗、刀光压在最上层」）。
//   2) ScreenSpaceCamera 画布虽然进相机队列，但运行时新建的 Canvas 在编辑器里
//      RectTransform.lossyScale 是 (0,0,0)、Image 网格还没生成，拍不到也调不准。
//   ⇒ 用普通几何体：进相机自己的透明队列，靠 renderQueue 精确控序，且可离屏验证。
//
// 排序：本材质 renderQueue = 2990（Transparent-10），刀光材质是 3000。
//       透明队列先小后大 ⇒ 先铺压暗、后画刀光，层级才是对的。
// 混合：普通 straight alpha（Blend SrcAlpha OneMinusSrcAlpha）。
//       压暗是「往暗色靠」，不是「发光」，所以不能像刀光那样用预乘/加法。
Shader "Loongdum/VH Domain Darken"
{
    Properties
    {
        _Color ("Color (RGB = 压到的颜色, A = 强度)", Color) = (0.30, 0.018, 0.028, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "VH Domain Darken"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return _Color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
