Shader "Hidden/Loongdum/ComicInk"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Hand Drawn Comic Ink"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragComic
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Assets/Shaders/VisionMaskCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ComicPaperColor;
                float4 _ComicInkColor;
                float4 _ComicBackgroundColor;
                float4 _ComicContour;
                float4 _ComicDrawing;
                float4 _ComicTone;
                float4 _ComicTexelSize;
                float _ComicDotSpacing;
                float _ComicHasNormals;
                float _ComicShapeInk;
                float _ComicUseVisionMask;
            CBUFFER_END

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float3 DisplayColor(float3 rgb)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                    return saturate(rgb);
                #else
                    return LinearToSRGB(saturate(rgb));
                #endif
            }

            float3 CameraColor(float3 rgb)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                    return saturate(rgb);
                #else
                    return SRGBToLinear(saturate(rgb));
                #endif
            }

            float LuminanceAt(float2 uv)
            {
                float3 rgb = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(uv), 0).rgb;
                return dot(DisplayColor(rgb), float3(0.2126, 0.7152, 0.0722));
            }

            float EyeDepthAt(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(saturate(uv)), _ZBufferParams);
            }

            float GeometryEdge(float2 uv, float2 delta, out float normalVariation)
            {
                normalVariation = 0.0;
                float depth = max(EyeDepthAt(uv), 0.001);
                float relativeDepth = max(
                    max(abs(EyeDepthAt(uv + float2(delta.x, 0)) - depth), abs(EyeDepthAt(uv - float2(delta.x, 0)) - depth)),
                    max(abs(EyeDepthAt(uv + float2(0, delta.y)) - depth), abs(EyeDepthAt(uv - float2(0, delta.y)) - depth))) / depth;
                float edge = smoothstep(_ComicContour.y, _ComicContour.y * 1.8, relativeDepth);
                if (_ComicHasNormals > 0.5)
                {
                    float3 normal = SampleSceneNormals(saturate(uv));
                    float difference = max(
                        max(length(normal - SampleSceneNormals(saturate(uv + float2(delta.x, 0)))),
                            length(normal - SampleSceneNormals(saturate(uv - float2(delta.x, 0))))),
                        max(length(normal - SampleSceneNormals(saturate(uv + float2(0, delta.y)))),
                            length(normal - SampleSceneNormals(saturate(uv - float2(0, delta.y))))));
                    normalVariation = difference;
                    edge = max(edge, smoothstep(_ComicContour.z, _ComicContour.z * 1.6, difference));
                }
                return edge;
            }

            float Halftone(float2 paper, float darkness)
            {
                // An imperfect rotated printing screen. No time input: the drawing does not flicker.
                float2 rotated = float2(paper.x + paper.y, paper.y - paper.x) * 0.70710678;
                float2 grid = rotated / max(_ComicDotSpacing, 1.5);
                float2 cell = floor(grid);
                float2 jitter = float2(Hash21(cell), Hash21(cell + 47.1)) - 0.5;
                float2 local = frac(grid) - 0.5 + jitter * 0.18;
                float radius = sqrt(saturate(darkness) / PI) * lerp(0.86, 1.12, Hash21(cell + 11.7));
                float distanceToDot = length(local);
                float aa = max(fwidth(distanceToDot) * 0.65, 0.015);
                return (1.0 - smoothstep(radius - aa, radius + aa, distanceToDot)) * step(0.015, darkness);
            }

            float HatchLine(float coordinate, float spacing, float width)
            {
                float distanceToLine = abs(frac(coordinate / spacing) - 0.5) * spacing;
                float aa = max(fwidth(coordinate) * 0.45, 0.15);
                return 1.0 - smoothstep(width - aa, width + aa, distanceToLine);
            }

            half4 FragComic(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 source = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
                float luminance = dot(DisplayColor(source.rgb), float3(0.2126, 0.7152, 0.0722));
                float rawDepth = SampleSceneDepth(uv);
                float mask = _ComicUseVisionMask > 0.5 ? EvaluateVisionMask(uv, rawDepth) : 0.0;
                float visibility = 1.0 - mask;

                // Preserve smoke already composited over hidden ground by Global Visible Smoke.
                float smokeVisibility = mask * smoothstep(0.015, 0.16, luminance);
                float surface = max(visibility, smokeVisibility);
                if ((_VisionMaskEnabled < 0.5 || _ComicUseVisionMask < 0.5) && VisionDepthIsSky(rawDepth))
                    surface = 0.0;

                float2 paper = uv * _ComicTexelSize.zw * (720.0 / _ComicTexelSize.w);
                float resolutionScale = _ComicTexelSize.w / 720.0;
                float2 pixelUV = _ComicTexelSize.xy * resolutionScale;
                float2 wobble = float2(
                    sin(paper.y * 0.19 + sin(paper.x * 0.073)),
                    cos(paper.x * 0.17 + cos(paper.y * 0.081))) * _ComicDrawing.x;
                float2 inkUV = saturate(uv + wobble * pixelUV);
                float2 widthUV = pixelUV * _ComicContour.x;
                float curvature;
                float outline = GeometryEdge(inkUV, widthUV, curvature) * visibility;
                if (_ComicDrawing.y > 0.001)
                {
                    float2 sketchOffset = float2(1.8 + sin(paper.y * 0.11), -1.2 + cos(paper.x * 0.13));
                    float ignoredCurvature;
                    float sketch = GeometryEdge(saturate(inkUV + sketchOffset * pixelUV), widthUV * 0.65, ignoredCurvature);
                    float brokenStroke = smoothstep(0.12, 0.45, Hash21(floor(paper / 7.0)));
                    outline = max(outline, sketch * _ComicDrawing.y * brokenStroke * visibility);
                }

                float colorEdge = max(
                    max(abs(LuminanceAt(uv + float2(widthUV.x, 0)) - luminance),
                        abs(LuminanceAt(uv - float2(widthUV.x, 0)) - luminance)),
                    max(abs(LuminanceAt(uv + float2(0, widthUV.y)) - luminance),
                        abs(LuminanceAt(uv - float2(0, widthUV.y)) - luminance)));
                outline = max(outline, smoothstep(0.12, 0.3, colorEdge) * _ComicContour.w * surface);
                outline = max(outline, saturate(fwidth(visibility) * 1.5) * visibility);

                float tone = saturate((luminance - _ComicTone.x) / max(_ComicTone.y - _ComicTone.x, 0.01));
                tone = floor(tone * _ComicTone.z + 0.5) / _ComicTone.z;
                float darkness = 1.0 - tone;
                float curvedInk = smoothstep(0.03, 0.10, curvature / max(_ComicContour.x, 0.5))
                    * (1.0 - smoothstep(0.52, 0.64, luminance)) * _ComicShapeInk;
                darkness = max(darkness, curvedInk * visibility);
                float dots = Halftone(paper, darkness * 0.88) * _ComicTone.w;
                float warp = sin(paper.y * 0.037) * 0.65;
                float hatch = HatchLine(paper.x + paper.y + warp, 6.0, 0.43) * smoothstep(0.22, 0.7, darkness);
                hatch = max(hatch, HatchLine(paper.x - paper.y - warp, 7.2, 0.38) * smoothstep(0.52, 0.95, darkness));
                float printedInk = saturate(max(dots, hatch * _ComicDrawing.z));
                // Reserve solid ink for the darkest forms; midtones remain visibly printed dots.
                printedInk = max(printedInk, smoothstep(0.86, 1.0, darkness) * 0.86);
                float paperNoise = (Hash21(floor(paper * 1.6)) - 0.5) * _ComicDrawing.w;
                float3 paperColor = _ComicPaperColor.rgb * (1.0 - darkness * 0.1) + paperNoise;
                float3 drawing = lerp(paperColor, _ComicInkColor.rgb, max(outline, printedInk));
                float3 background = _ComicBackgroundColor.rgb + paperNoise * 0.25;
                float3 result = lerp(background, drawing, surface);
                return half4(CameraColor(result), source.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
