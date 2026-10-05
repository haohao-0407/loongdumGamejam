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

            float4 _VisionSourcePositionRadius;
            float _VisionEdgeSoftness;
            float _VisionMaskEnabled;
            TEXTURE2D(_VisionObstacleDistances);
            SAMPLER(sampler_VisionObstacleDistances);
            float _VisionOcclusionEnabled;
            float _VisionRayCount;
            float _VisionSurfacePadding;
            #define MAX_PORTAL_VIEWS 8
            float _VisionPortalCount;
            float4 _VisionPortalOrigins[MAX_PORTAL_VIEWS];
            TEXTURE2D(_VisionPortalDistances);
            SAMPLER(sampler_VisionPortalDistances);

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_VisionMaskEnabled - 0.5);

                float2 uv = input.texcoord;
                float depth = SampleSceneDepth(uv);

                // The sky has no world surface and stays outside the visible area.
                #if UNITY_REVERSED_Z
                    if (depth <= 0.000001)
                        return half4(0, 0, 0, 1);
                #else
                    if (depth >= 0.999999)
                        return half4(0, 0, 0, 1);
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif

                float3 worldPosition = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float distanceFromSource = distance(worldPosition.xz, _VisionSourcePositionRadius.xz);
                float radius = _VisionSourcePositionRadius.w;
                float softness = min(_VisionEdgeSoftness, radius);
                float mask = softness > 0.0001
                    ? smoothstep(radius - softness, radius, distanceFromSource)
                    : step(radius, distanceFromSource);

                if (_VisionOcclusionEnabled > 0.5)
                {
                    float2 direction = worldPosition.xz - _VisionSourcePositionRadius.xz;
                    float angle = atan2(direction.y, direction.x) / TWO_PI;
                    float2 lookupUV = float2(frac(angle + 0.5 / _VisionRayCount), 0.5);
                    float visibleDistance = SAMPLE_TEXTURE2D_LOD(_VisionObstacleDistances,
                        sampler_VisionObstacleDistances, lookupUV, 0).r * radius;
                    float occlusionMask = step(visibleDistance + _VisionSurfacePadding, distanceFromSource);
                    mask = max(mask, occlusionMask);
                }

                // Union each exit's aperture-clipped visibility with the direct view.
                [loop]
                for (int portal = 0; portal < (int)_VisionPortalCount; portal++)
                {
                    float4 origin = _VisionPortalOrigins[portal];
                    float2 delta = worldPosition.xz - origin.xz;
                    float portalDistance = length(delta);
                    float angle = atan2(delta.y, delta.x) / TWO_PI;
                    float2 lookup = float2(frac(angle + 0.5 / _VisionRayCount),
                        (portal + 0.5) / MAX_PORTAL_VIEWS);
                    float2 limits = SAMPLE_TEXTURE2D_LOD(_VisionPortalDistances,
                        sampler_VisionPortalDistances, lookup, 0).rg * origin.w;
                    float visible = step(limits.x, portalDistance)
                        * (1.0 - step(limits.y + _VisionSurfacePadding, portalDistance));
                    float portalSoftness = min(_VisionEdgeSoftness, origin.w);
                    float rangeMask = portalSoftness > 0.0001
                        ? smoothstep(origin.w - portalSoftness, origin.w, portalDistance)
                        : step(origin.w, portalDistance);
                    mask = min(mask, 1.0 - visible * (1.0 - rangeMask));
                }
                return half4(0, 0, 0, mask);
            }
            ENDHLSL
        }
    }
}
