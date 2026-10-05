using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>Full-resolution pen contours, printed halftones and paper grain for URP RenderGraph.</summary>
public sealed class ComicInkRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader m_Shader;

    [Header("Paper and Ink")]
    [SerializeField] private Color m_PaperColor = new Color(0.97f, 0.97f, 0.96f, 1f);
    [SerializeField] private Color m_InkColor = new Color(0.025f, 0.025f, 0.025f, 1f);
    [SerializeField] private Color m_BackgroundColor = new Color(0.23f, 0.23f, 0.23f, 1f);
    [SerializeField, Range(0f, 0.1f)] private float m_PaperGrain = 0.018f;

    [Header("Pen Contours")]
    [Tooltip("Line width in pixels at a 720-pixel image height. Scales with the camera resolution.")]
    [SerializeField, Range(0.5f, 4f)] private float m_OutlineWidth = 1.6f;
    [SerializeField, Range(0.001f, 0.05f)] private float m_DepthThreshold = 0.008f;
    [SerializeField, Range(0.05f, 1f)] private float m_NormalThreshold = 0.22f;
    [SerializeField, Range(0f, 1f)] private float m_TextureEdgeStrength = 0.15f;
    [SerializeField, Range(0f, 2f)] private float m_LineWobble = 0.8f;
    [SerializeField, Range(0f, 1f)] private float m_SketchStrength = 0.6f;

    [Header("Printed Shadows")]
    [SerializeField, Range(1.5f, 8f)] private float m_HalftoneSpacing = 2.8f;
    [SerializeField, Range(0f, 1f)] private float m_HalftoneStrength = 1f;
    [SerializeField, Range(0f, 1f)] private float m_HatchingStrength = 0.35f;
    [Tooltip("Extra ink on curved, dark forms while retaining their bright highlights.")]
    [SerializeField, Range(0f, 1f)] private float m_CurvedSurfaceInk = 0.95f;
    [SerializeField, Range(0f, 0.3f)] private float m_BlackPoint = 0.025f;
    [SerializeField, Range(0.35f, 1f)] private float m_WhitePoint = 0.62f;
    [SerializeField, Range(2, 8)] private int m_ToneSteps = 4;

    [Header("Cameras")]
    [Tooltip("在 Scene 视图中预览漫画效果。关闭后只跳过 Scene 视图，Game 视图仍使用漫画渲染。")]
    [SerializeField] private bool m_ApplyToSceneView = false;
    [Tooltip("The vision mask this finish follows. Its enabled toggle controls the gray hidden area.")]
    [SerializeField] private FullScreenPassRendererFeature m_VisionMaskFeature;

    private Material m_Material;
    private ComicInkPass m_Pass;

    public override void Create()
    {
        CoreUtils.Destroy(m_Material);
        Shader shader = m_Shader != null ? m_Shader : Resources.Load<Shader>("ComicInk");
        if (shader == null)
            shader = Shader.Find("Hidden/Loongdum/ComicInk");

        m_Material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        m_Pass = m_Material != null ? new ComicInkPass(m_Material) : null;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        CameraData camera = renderingData.cameraData;
        if (m_Pass == null || !camera.resolveFinalTarget)
            return;
        if (camera.cameraType != CameraType.Game &&
            !(m_ApplyToSceneView && camera.cameraType == CameraType.SceneView))
            return;

        // Vectors intentionally carry display-space colors; the shader performs the conversion once.
        m_Material.SetVector("_ComicPaperColor", m_PaperColor);
        m_Material.SetVector("_ComicInkColor", m_InkColor);
        m_Material.SetVector("_ComicBackgroundColor", m_BackgroundColor);
        m_Material.SetVector("_ComicContour", new Vector4(
            m_OutlineWidth, m_DepthThreshold, m_NormalThreshold, m_TextureEdgeStrength));
        m_Material.SetVector("_ComicDrawing", new Vector4(
            m_LineWobble, m_SketchStrength, m_HatchingStrength, m_PaperGrain));
        m_Material.SetVector("_ComicTone", new Vector4(
            m_BlackPoint, Mathf.Max(m_BlackPoint + 0.01f, m_WhitePoint), m_ToneSteps, m_HalftoneStrength));
        m_Material.SetFloat("_ComicDotSpacing", m_HalftoneSpacing);
        m_Material.SetFloat("_ComicShapeInk", m_CurvedSurfaceInk);
        m_Material.SetFloat("_ComicUseVisionMask",
            m_VisionMaskFeature != null && m_VisionMaskFeature.isActive ? 1f : 0f);
        renderer.EnqueuePass(m_Pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_Material);
        m_Material = null;
        m_Pass = null;
    }

    private sealed class ComicInkPass : ScriptableRenderPass
    {
        private static readonly int DepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int NormalsTextureId = Shader.PropertyToID("_CameraNormalsTexture");
        private static readonly int TexelSizeId = Shader.PropertyToID("_ComicTexelSize");
        private static readonly int HasNormalsId = Shader.PropertyToID("_ComicHasNormals");
        private readonly Material m_Material;

        private sealed class PassData
        {
            public TextureHandle source;
            public TextureHandle depth;
            public TextureHandle normals;
            public Material material;
            public Vector4 texelSize;
        }

        public ComicInkPass(Material material)
        {
            m_Material = material;
            // Vision (600) -> globally visible smoke (600) -> comic ink (602) -> UI.
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingPostProcessing + 2);
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            UniversalCameraData camera = frameData.Get<UniversalCameraData>();
            if (resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid() ||
                !resources.cameraDepthTexture.IsValid())
                return;

            TextureHandle source = resources.activeColorTexture;
            TextureDesc descriptor = renderGraph.GetTextureDesc(source);
            descriptor.name = "Comic Ink Camera Color";
            descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            descriptor.bindTextureMS = false;
            descriptor.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(descriptor);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Hand Drawn Comic Ink", out var data))
            {
                data.source = source;
                data.depth = resources.cameraDepthTexture;
                data.normals = resources.cameraNormalsTexture;
                data.material = m_Material;
                int width = Mathf.Max(1, camera.cameraTargetDescriptor.width);
                int height = Mathf.Max(1, camera.cameraTargetDescriptor.height);
                data.texelSize = new Vector4(1f / width, 1f / height, width, height);
                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(data.depth, AccessFlags.Read);
                if (data.normals.IsValid())
                    builder.UseTexture(data.normals, AccessFlags.Read);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                {
                    pass.material.SetTexture(DepthTextureId, pass.depth);
                    if (pass.normals.IsValid())
                        pass.material.SetTexture(NormalsTextureId, pass.normals);
                    pass.material.SetFloat(HasNormalsId, pass.normals.IsValid() ? 1f : 0f);
                    pass.material.SetVector(TexelSizeId, pass.texelSize);
                    Blitter.BlitTexture(context.cmd, pass.source, new Vector4(1f, 1f, 0f, 0f), pass.material, 0);
                });
            }

            // The final blit consumes this texture directly, avoiding a second full-screen copy.
            resources.cameraColor = destination;
        }
    }
}
