// 宿傩·领域展开 —— 单根「长条刀光」着色器
//
// 设计要点（改这个文件前先读）：
// 1) 面片是 Shuriken 的 Billboard quad：local X = 刃长（size.x），local Y = 刃宽（size.y）。
//    ⇒ 「外白内黑」的渐变走在 **UV.y**（刃宽方向），不是 UV.x。
// 2) 内核是实心黑 —— 加色混合做不到（黑=不加任何东西），所以必须走 Alpha 混合。
//    本工程透明材质统一用预乘：Blend One OneMinusSrcAlpha。据此：
//      · 内核：rgb 给 0、alpha 给 ~1        ⇒ out = 0 + dst*0 = 纯黑，实心遮住背景
//      · 白边：rgb 给 HDR 亮值、alpha 给中等 ⇒ out = 亮 + dst*(1-a)，靠"加法"发白
// 3) 粒子的 Color over Lifetime 通过顶点色进来（input.color），寿命淡入淡出靠它驱动。
// 4) 【2026-10-06 对照参考图实测】目标横截面是「白 / 黑 / 白」三明治：
//    白色两侧各约 30%、黑芯约 40%，白色接近纯白且是**硬边**（不是柔和辉光）。
//    ⇒ _EdgeStart=0.40 + _EdgeAlpha=0.85 + _EdgeSoftness=0.08。
//    若黑芯比例调太大（如 0.62），在刀身只有 2~4 px 宽时整体会读成"暗划痕"，与参考图相反。
//    ⚠️ 因此在 800px 宽的预览里看不清是正常的，要用 ≥1600px 宽来判断。
Shader "Loongdum/VH Domain Slash"
{
    Properties
    {
        _CoreColor ("Core Color", Color) = (0, 0, 0, 1)
        _EdgeColor ("Edge Color (HDR)", Color) = (1, 1, 1, 1)
        _EdgeIntensity ("Edge Intensity", Range(0, 8)) = 1.15
        // ⚠️ 最关键的一个旋钮：黑芯占半宽的比例。
        //    0.62 ⇒ 黑占 62%（读起来像一道暗划痕，错）
        //    0.40 ⇒ 白30% / 黑40% / 白30%，与参考图的横截面一致
        _EdgeStart ("Edge Start (0 = core, 1 = rim)", Range(0, 1)) = 0.40
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.08
        _CoreAlpha ("Core Alpha", Range(0, 1)) = 1
        _EdgeAlpha ("Edge Alpha", Range(0, 1)) = 0.85
        _TipFade ("Tip Fade (UV)", Range(0, 0.5)) = 0.05
        _Wobble ("Edge Wobble", Range(0, 0.4)) = 0.05
        _WobbleFreq ("Wobble Frequency", Range(1, 60)) = 14
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "VH Domain Slash"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _EdgeColor;
                float _EdgeIntensity;
                float _EdgeStart;
                float _EdgeSoftness;
                float _CoreAlpha;
                float _EdgeAlpha;
                float _TipFade;
                float _Wobble;
                float _WobbleFreq;
                float _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // 沿刃长的低频抖动，让边缘像手绘而不是尺子画的（与本工程的水墨描边风格一致）
            float Hash1(float x)
            {
                return frac(sin(x * 127.1) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;

                // ---- 刃宽方向：0 = 中轴（黑芯），1 = 两侧外缘（白边）----
                float d = abs(uv.y * 2.0 - 1.0);
                float band = floor(uv.x * _WobbleFreq);
                float noise = (Hash1(band) * 2.0 - 1.0) * 0.7
                            + (Hash1(band * 1.7 + 11.0) * 2.0 - 1.0) * 0.3;
                d = saturate(d + noise * _Wobble);

                float softness = max(_EdgeSoftness, 1e-4);
                float edge = smoothstep(_EdgeStart - softness, _EdgeStart + softness, d);

                // ---- 刃长两端收尾，避免出现方块断口 ----
                float tip = max(_TipFade, 1e-4);
                float taper = smoothstep(0.0, tip, uv.x) * smoothstep(0.0, tip, 1.0 - uv.x);

                // ---- 粒子寿命淡入淡出（顶点色 alpha）----
                half life = input.color.a;

                // 预乘输出：内核乘 alpha 参与遮挡，白边额外叠加发光
                half alpha = lerp((half)_CoreAlpha, (half)_EdgeAlpha, (half)edge) * (half)taper;
                half3 rgb = _CoreColor.rgb * (1.0h - (half)edge) * (half)_CoreAlpha * (half)taper
                          + _EdgeColor.rgb * (half)_EdgeIntensity * (half)edge * (half)taper;

                rgb *= life;
                alpha *= life;

                return half4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
