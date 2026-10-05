#ifndef LOONGDUM_VISION_MASK_COMMON_INCLUDED
#define LOONGDUM_VISION_MASK_COMMON_INCLUDED

// Shared by the gameplay mask and comic finish so hidden geometry cannot acquire ink contours.
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

bool VisionDepthIsSky(float depth)
{
    #if UNITY_REVERSED_Z
        return depth <= 0.000001;
    #else
        return depth >= 0.999999;
    #endif
}

float EvaluateVisionMask(float2 uv, float depth)
{
    if (_VisionMaskEnabled < 0.5)
        return 0.0;
    if (VisionDepthIsSky(depth))
        return 1.0;

    #if !UNITY_REVERSED_Z
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
        mask = max(mask, step(visibleDistance + _VisionSurfacePadding, distanceFromSource));
    }

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
    return mask;
}

#endif
