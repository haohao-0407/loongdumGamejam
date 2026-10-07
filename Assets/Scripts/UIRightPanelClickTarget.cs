using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Lets clicks on a page's visible UI graphics focus its whole panel.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class UIRightPanelClickTarget : MonoBehaviour, IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private UIRightPanelFocus focusController;
    [SerializeField, Range(0.01f, 0.5f)] private float minimumVisibleAlpha = 0.05f;

    private void Awake()
    {
        if (focusController == null)
            focusController = GetComponentInParent<UIRightPanelFocus>();

        foreach (UnityEngine.UI.Graphic graphic in GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
        {
            if (!graphic.raycastTarget)
                continue;

            if (graphic is not UnityEngine.UI.Image image)
            {
                // The page artwork and Button images handle clicks. Text bounds
                // should not cover transparent pixels of pages below them.
                graphic.raycastTarget = false;
                continue;
            }

            if (image.sprite == null)
            {
                if (image.color.a <= 0.001f)
                    image.raycastTarget = false;
                continue;
            }

            if (image.sprite.texture.isReadable && !image.sprite.packed)
            {
                image.alphaHitTestMinimumThreshold = minimumVisibleAlpha;
            }
            else if (image.transform != transform &&
                     image.GetComponentInParent<UnityEngine.UI.Selectable>() == null)
            {
                // Decorative graphics which cannot be alpha tested should not
                // block a visible page underneath them.
                image.raycastTarget = false;
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && focusController != null)
            focusController.Focus((RectTransform)transform);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (focusController != null)
            focusController.SetHovered((RectTransform)transform, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (focusController != null)
            focusController.SetHovered((RectTransform)transform, false);
    }
}
