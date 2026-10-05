Shader "Loongdum/Character/Ink Dots"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _DotColor ("Dot Color", Color) = (0.02, 0.02, 0.02, 1)
        _DotBackgroundColor ("Dot Background Color", Color) = (0.55, 0.55, 0.55, 1)
        _DotDensity ("Dot Density (per unit)", Range(2, 100)) = 42
        _DotSize ("Dot Size", Range(0.05, 0.48)) = 0.31
        _BlackThreshold ("Black Threshold", Range(0, 1)) = 0.3

        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "CanUseSpriteAtlas" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "Character Ink Dots"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float2 localXY : TEXCOORD1;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _DotColor;
                half4 _DotBackgroundColor;
                float _DotDensity;
                float _DotSize;
                float _BlackThreshold;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                // Object space keeps the dots stable when an animation swaps sprite frames.
                output.localXY = input.positionOS.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(source.rgb, half3(0.2126, 0.7152, 0.0722));
                half blackMask = 1.0h - smoothstep(
                    max(0.0h, _BlackThreshold - 0.04h),
                    min(1.0h, _BlackThreshold + 0.04h), luminance);

                float2 cell = frac(input.localXY * max(_DotDensity, 0.001)) - 0.5;
                float distanceToCenter = length(cell);
                float antialiasWidth = max(fwidth(distanceToCenter), 0.01);
                half dot = 1.0h - smoothstep(
                    _DotSize - antialiasWidth, _DotSize + antialiasWidth, distanceToCenter);

                half3 dottedInk = lerp(_DotBackgroundColor.rgb, _DotColor.rgb, dot);
                half3 rgb = lerp(source.rgb, dottedInk, blackMask) * input.color.rgb;
                return half4(rgb, source.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
