Shader "Hidden/Loongdum/StarBlackWhiteFlash"
{
    Properties
    {
        _MainTex ("Captured Frame", 2D) = "white" {}
        _RingTex ("Impact Ring", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay" "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend One Zero

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_RingTex);
            SAMPLER(sampler_RingTex);

            CBUFFER_START(UnityPerMaterial)
                float _FlashStrength;
                float _InvertThreshold;
                float _TextureThreshold;
                float _RingRadius;
                float _RingWidth;
                float _TextureYStretch;
                float _TextureRepeats;
                float _EdgeIrregularity;
                float _WhiteNoiseAmount;
                float _JitterPhase;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float value)
            {
                return frac(sin(value * 127.1) * 43758.5453);
            }

            float BinaryWhite(float2 uv)
            {
                float3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    saturate(uv)).rgb;
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                return step(_InvertThreshold, 1.0 - luminance);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 frame = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                float luminance = dot(frame, float3(0.2126, 0.7152, 0.0722));
                float white = step(_InvertThreshold, 1.0 - luminance);

                // Unequal rays break up the single impact texture's repeated silhouette.
                float2 radialPosition = input.uv - 0.5;
                radialPosition.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float radius = length(radialPosition);
                float angle = atan2(radialPosition.y, radialPosition.x) / 6.28318530718 + 0.5;
                float fineSegments = 89.0 + _TextureRepeats * 12.0;
                float coarseSegments = 47.0 + _TextureRepeats * 5.0;
                float fineCell = floor(angle * fineSegments);
                float coarseCell = floor(angle * coarseSegments + 0.31);
                float finePosition = frac(angle * fineSegments);
                float coarsePosition = frac(angle * coarseSegments + 0.31);
                float fineCenter = lerp(0.24, 0.76, Hash(fineCell + 3.0));
                float coarseCenter = lerp(0.23, 0.77, Hash(coarseCell + 5.0));
                float fineWidth = lerp(0.05, 0.26, Hash(fineCell + 9.0));
                float coarseWidth = lerp(0.08, 0.31, Hash(coarseCell + 31.0));
                float fineRay = saturate((fineWidth - abs(finePosition - fineCenter))
                    / max(fineWidth, 0.001)) * Hash(fineCell + 23.0);
                float coarseRay = saturate((coarseWidth - abs(coarsePosition - coarseCenter))
                    / max(coarseWidth, 0.001)) * Hash(coarseCell + 57.0);
                float rays = max(fineRay, coarseRay * 0.72);

                float frameStep = floor(_JitterPhase * 2.0);
                float sectorJitter = Hash(fineCell + frameStep * 37.0);
                float bandWidth = max(_RingWidth * _TextureYStretch, 0.001);
                float polarV = saturate((radius - _RingRadius) / bandWidth + 0.5);
                float polarU = angle * max(_TextureRepeats, 1.0)
                    + (sectorJitter - 0.5) * 0.65;
                float impact = SAMPLE_TEXTURE2D(_RingTex, sampler_RingTex,
                    float2(polarU, polarV)).r;
                float inkRay = rays * saturate((impact - _TextureThreshold)
                    / max(1.0 - _TextureThreshold, 0.001));

                // The expanding front affects only pixels next to a black/white boundary.
                float shock = saturate(1.0 - abs(radius - _RingRadius)
                    / max(bandWidth * 0.85, 0.001));
                float2 screenRadial = input.uv - 0.5;
                float2 direction = screenRadial / max(length(screenRadial), 0.001);
                float edgeReach = 0.002 + _EdgeIrregularity
                    * (0.12 + inkRay * 0.82);
                float2 edgeOffset = direction * edgeReach;
                float acrossEdge = abs(BinaryWhite(input.uv + edgeOffset)
                    - BinaryWhite(input.uv - edgeOffset));
                float2 tangent = float2(-direction.y, direction.x);
                float alongEdge = abs(BinaryWhite(input.uv + tangent * edgeReach * 0.5)
                    - BinaryWhite(input.uv - tangent * edgeReach * 0.5));
                float edge = max(acrossEdge, alongEdge);

                float unevenPush = (0.2 + inkRay * 2.3
                    + (sectorJitter - 0.5) * 0.32) * edgeReach * shock;
                float inkCut = step(0.78, Hash(fineCell * 1.7 + frameStep * 13.0));
                unevenPush *= lerp(1.0, -0.45, inkCut);
                float displacedWhite = BinaryWhite(input.uv - direction * unevenPush);
                float edgeEffect = edge * step(0.03, shock)
                    * saturate(_WhiteNoiseAmount);
                float blackWhite = lerp(white, displacedWhite, edgeEffect);
                float3 result = lerp(frame, blackWhite.xxx, saturate(_FlashStrength));
                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }
}
