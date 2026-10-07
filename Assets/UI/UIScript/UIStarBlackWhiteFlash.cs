using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.Image))]
public sealed class UIStarBlackWhiteFlash : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Shader flashShader;
    [SerializeField] private Texture2D ringTexture;

    [Header("Flash")]
    [SerializeField, Min(0.05f)] private float flashDuration = 0.36f;
    [SerializeField, Range(0f, 1f)] private float peakStrength = 1f;
    [SerializeField, Range(0f, 1f)] private float invertedThreshold = 0.5f;

    [Header("Texture Ring")]
    [SerializeField, Range(0f, 1f)] private float textureThreshold = 0.42f;
    [SerializeField, Min(0.01f)] private float impactBandWidth = 0.16f;
    [SerializeField, Min(0.1f)] private float impactYStretch = 1.6f;
    [SerializeField, Range(1, 8)] private int textureRepeats = 4;
    [SerializeField, Range(0f, 0.2f)] private float edgeIrregularity = 0.065f;
    [SerializeField, Min(0f)] private float expansionRadius = 1.35f;
    [SerializeField, InspectorName("Edge Burst Strength"), Range(0f, 1f)]
    private float whiteNoiseAmount = 1f;
    [SerializeField, Min(0f)] private float jitterSpeed = 8f;

    private static readonly int RingTexId = Shader.PropertyToID("_RingTex");
    private static readonly int StrengthId = Shader.PropertyToID("_FlashStrength");
    private static readonly int InvertedThresholdId = Shader.PropertyToID("_InvertThreshold");
    private static readonly int TextureThresholdId = Shader.PropertyToID("_TextureThreshold");
    private static readonly int RingRadiusId = Shader.PropertyToID("_RingRadius");
    private static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
    private static readonly int TextureYStretchId = Shader.PropertyToID("_TextureYStretch");
    private static readonly int TextureRepeatsId = Shader.PropertyToID("_TextureRepeats");
    private static readonly int EdgeIrregularityId = Shader.PropertyToID("_EdgeIrregularity");
    private static readonly int WhiteNoiseAmountId = Shader.PropertyToID("_WhiteNoiseAmount");
    private static readonly int JitterPhaseId = Shader.PropertyToID("_JitterPhase");

    private GameObject overlayRoot;
    private Canvas overlayCanvas;
    private UnityEngine.UI.RawImage overlayImage;
    private Material flashMaterial;
    private Texture2D capturedFrame;
    private Coroutine flashRoutine;
    public event System.Action Completed;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !isActiveAndEnabled)
            return;

        Play();
    }

    public void Play()
    {
        if (!isActiveAndEnabled || flashRoutine != null)
            return;

        if (flashShader == null || ringTexture == null)
        {
            Debug.LogError("Star flash needs a shader and ring texture.", this);
            Completed?.Invoke();
            return;
        }

        HideAndReleaseFrame();
        flashRoutine = StartCoroutine(PlayFlash());
    }

    private IEnumerator PlayFlash()
    {
        // Capture after all cameras and Screen Space Overlay canvases have rendered.
        yield return new WaitForEndOfFrame();
        if (!isActiveAndEnabled)
            yield break;

        capturedFrame = ScreenCapture.CaptureScreenshotAsTexture();
        if (capturedFrame == null)
        {
            flashRoutine = null;
            Completed?.Invoke();
            yield break;
        }

        EnsureOverlay();
        flashMaterial.SetTexture(RingTexId, ringTexture);
        flashMaterial.SetFloat(InvertedThresholdId, invertedThreshold);
        flashMaterial.SetFloat(TextureThresholdId, textureThreshold);
        flashMaterial.SetFloat(RingWidthId, Mathf.Max(0.01f, impactBandWidth));
        flashMaterial.SetFloat(TextureYStretchId, Mathf.Max(0.1f, impactYStretch));
        flashMaterial.SetFloat(TextureRepeatsId, Mathf.Clamp(textureRepeats, 1, 8));
        flashMaterial.SetFloat(EdgeIrregularityId, Mathf.Clamp(edgeIrregularity, 0f, 0.2f));
        flashMaterial.SetFloat(WhiteNoiseAmountId, Mathf.Clamp01(whiteNoiseAmount));
        overlayImage.texture = capturedFrame;
        overlayCanvas.enabled = true;

        float elapsed = 0f;
        float total = Mathf.Max(0.05f, flashDuration);
        while (elapsed < total)
        {
            float progress = Mathf.Clamp01(elapsed / total);
            float attack = Mathf.Clamp01(progress / 0.08f);
            float decay = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 1f, progress));
            flashMaterial.SetFloat(StrengthId, attack * decay * peakStrength);
            float expansion = 1f - Mathf.Pow(1f - progress, 2.3f);
            flashMaterial.SetFloat(RingRadiusId, expansionRadius * expansion);
            flashMaterial.SetFloat(JitterPhaseId, progress * Mathf.Max(0f, jitterSpeed));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        HideAndReleaseFrame();
        flashRoutine = null;
        Completed?.Invoke();
    }

    private void EnsureOverlay()
    {
        if (overlayRoot != null)
            return;

        flashMaterial = new Material(flashShader) { name = "Star Black White Flash (Runtime)" };
        overlayRoot = new GameObject("Star Black White Flash", typeof(RectTransform), typeof(Canvas));
        overlayCanvas = overlayRoot.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 32000;
        overlayCanvas.enabled = false;

        GameObject imageObject = new GameObject("Processed Frame", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.RawImage));
        imageObject.transform.SetParent(overlayRoot.transform, false);
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlayImage = imageObject.GetComponent<UnityEngine.UI.RawImage>();
        overlayImage.material = flashMaterial;
        overlayImage.raycastTarget = false;
    }

    private void HideAndReleaseFrame()
    {
        if (overlayCanvas != null)
            overlayCanvas.enabled = false;
        if (overlayImage != null)
            overlayImage.texture = null;
        if (capturedFrame != null)
        {
            Destroy(capturedFrame);
            capturedFrame = null;
        }
    }

    private void OnDisable()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        HideAndReleaseFrame();
    }

    private void OnDestroy()
    {
        if (overlayRoot != null)
            Destroy(overlayRoot);
        if (flashMaterial != null)
            Destroy(flashMaterial);
    }
}
