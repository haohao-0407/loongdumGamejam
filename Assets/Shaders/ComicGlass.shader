Shader "Loongdum/Comic Glass"
{
    Properties
    {
        _BaseColor ("Glass Tint", Color) = (0.82, 0.88, 0.9, 0.035)
        _InkColor ("Stroke Color", Color) = (0.01, 0.01, 0.01, 1)
        _HaloColor ("Stroke Backing Color", Color) = (1, 1, 1, 0.95)
        _StrokeAngle ("Stroke Angle", Range(0, 180)) = 45
        _StrokeSpacing ("Stroke Group Spacing (m)", Float) = 0.85
        _PairSpacing ("Pair Spacing (m)", Float) = 0.12
        _StrokeLength ("Stroke Length (m)", Float) = 0.5
        _RowSpacing ("Stroke Row Spacing (m)", Float) = 1.2
        _StrokeWidth ("Stroke Width (m)", Float) = 0.035
        _HaloWidth ("Backing Width (m)", Float) = 0.025
        _Wobble ("Hand Drawn Wobble (m)", Range(0, 0.03)) = 0.006
        _BorderWidth ("Border Width (UV)", Range(0, 0.05)) = 0.006
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "DisableBatching" = "True" }
        Pass
        {
            Name "Comic Glass"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertGlass
            #pragma fragment FragGlass
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _InkColor;
                half4 _HaloColor;
                float _StrokeAngle;
                float _StrokeSpacing;
                float _PairSpacing;
                float _StrokeLength;
                float _RowSpacing;
                float _StrokeWidth;
                float _HaloWidth;
                float _Wobble;
                float _BorderWidth;
                float _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionScaledOS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings VertGlass(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // Keep the drawing on the pane, with metre-sized marks even on scaled cubes.
                float3 scale = float3(length(TransformObjectToWorldDir(float3(1, 0, 0), false)),
                                      length(TransformObjectToWorldDir(float3(0, 1, 0), false)),
                                      length(TransformObjectToWorldDir(float3(0, 0, 1), false)));
                output.positionScaledOS = input.positionOS.xyz * scale;
                output.normalOS = input.normalOS;
                output.uv = input.uv;
                return output;
            }

            half4 FragGlass(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = abs(input.normalOS);
                float2 pane = n.y >= max(n.x, n.z) ? input.positionScaledOS.xz
                            : n.x >= n.z ? input.positionScaledOS.zy : input.positionScaledOS.xy;
                float angle = radians(_StrokeAngle);
                float2 along = float2(cos(angle), sin(angle));
                float2 across = float2(-along.y, along.x);
                float u = dot(pane, across);
                float v = dot(pane, along);
                u += (sin(v * 24.0 + 1.7) + 0.35 * sin(v * 57.0)) * _Wobble;
                float groupSpacing = max(_StrokeSpacing, 0.05);
                float rowSpacing = max(_RowSpacing, 0.05);
                float group = floor(u / groupSpacing + 0.5);
                float groupU = u - group * groupSpacing;
                // Alternating rows keep the pairs sparse without making a continuous striped wall.
                float rowV = v + frac(group * 0.5) * rowSpacing * 0.5;
                rowV -= floor(rowV / rowSpacing + 0.5) * rowSpacing;
                float distanceToStroke = min(abs(groupU - _PairSpacing * 0.5),
                                             abs(groupU + _PairSpacing * 0.5));
                float aaU = max(fwidth(u), 0.0001);
                float aaV = max(fwidth(v), 0.0001);
                float halfWidth = max(_StrokeWidth * 0.5, aaU * 0.65);
                float halfLength = min(max(_StrokeLength * 0.5, 0.025), rowSpacing * 0.45);
                float ends = 1.0 - smoothstep(halfLength - aaV, halfLength + aaV, abs(rowV));
                float ink = (1.0 - smoothstep(halfWidth - aaU * 0.5, halfWidth + aaU * 0.5, distanceToStroke)) * ends;
                float halo = (1.0 - smoothstep(halfWidth + _HaloWidth - aaU * 0.5,
                                               halfWidth + _HaloWidth + aaU * 0.5, distanceToStroke)) * ends;

                float2 edgeUV = min(input.uv, 1.0 - input.uv);
                float edgeDistance = min(edgeUV.x, edgeUV.y);
                float edgeAA = max(min(fwidth(input.uv.x), fwidth(input.uv.y)), 0.0001);
                float border = _BorderWidth > 0.0
                    ? 1.0 - smoothstep(_BorderWidth - edgeAA * 0.5, _BorderWidth + edgeAA * 0.5, edgeDistance)
                    : 0.0;
                ink = max(ink, border);
                halo = max(halo, border);

                // Premultiplied layers keep the floor visible between the marks.
                half inkAlpha = saturate(ink * _InkColor.a);
                half haloAlpha = saturate(halo * _HaloColor.a);
                half baseAlpha = saturate(_BaseColor.a);
                half3 color = _BaseColor.rgb * baseAlpha;
                color = color * (1.0h - haloAlpha) + _HaloColor.rgb * haloAlpha;
                half alpha = baseAlpha * (1.0h - haloAlpha) + haloAlpha;
                color = color * (1.0h - inkAlpha) + _InkColor.rgb * inkAlpha;
                alpha = alpha * (1.0h - inkAlpha) + inkAlpha;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
