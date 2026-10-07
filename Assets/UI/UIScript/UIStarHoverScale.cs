using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.Image))]
public sealed class UIStarHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Min(1f)] private float hoverScaleMultiplier = 1.2f;
    [SerializeField, Min(0.01f)] private float animationDuration = 0.2f;
    [SerializeField, Range(0.01f, 1f)] private float visiblePixelThreshold = 0.05f;

    private Vector3 originalScale;
    private Coroutine scaleAnimation;
    private bool isHovered;

    private void Awake()
    {
        originalScale = transform.localScale;
        LetPointerThroughTitleDecoration();

        UnityEngine.UI.Image image = GetComponent<UnityEngine.UI.Image>();
        if (image.sprite != null && image.sprite.texture.isReadable && !image.sprite.packed)
            image.alphaHitTestMinimumThreshold = visiblePixelThreshold;
    }

    private void LetPointerThroughTitleDecoration()
    {
        Transform title = transform.parent;
        if (title == null)
            return;

        UnityEngine.UI.Image titleImage = title.GetComponent<UnityEngine.UI.Image>();
        if (titleImage != null)
            titleImage.raycastTarget = false;

        for (int index = 0; index < title.childCount; index++)
        {
            Transform sibling = title.GetChild(index);
            if (sibling == transform || (sibling.name != "Image" && sibling.name != "Image (1)"))
                continue;

            UnityEngine.UI.Image decoration = sibling.GetComponent<UnityEngine.UI.Image>();
            if (decoration != null && sibling.GetComponent<UnityEngine.UI.Selectable>() == null)
                decoration.raycastTarget = false;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHovered(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHovered(false);
    }

    private void SetHovered(bool hovered)
    {
        if (isHovered == hovered)
            return;

        isHovered = hovered;
        if (scaleAnimation != null)
            StopCoroutine(scaleAnimation);

        Vector3 target = hovered ? originalScale * hoverScaleMultiplier : originalScale;
        scaleAnimation = StartCoroutine(ScaleTo(target));
    }

    private IEnumerator ScaleTo(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, animationDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.localScale = Vector3.LerpUnclamped(start, target, t);
            yield return null;
        }

        transform.localScale = target;
        scaleAnimation = null;
    }

    private void OnDisable()
    {
        if (scaleAnimation != null)
        {
            StopCoroutine(scaleAnimation);
            scaleAnimation = null;
        }

        transform.localScale = originalScale;
        isHovered = false;
    }
}
