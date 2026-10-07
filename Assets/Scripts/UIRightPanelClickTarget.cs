using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Lets clicks on a page's visible UI graphics focus its whole panel.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class UIRightPanelClickTarget : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private UIRightPanelFocus focusController;
    [SerializeField, Range(0.01f, 0.5f)] private float minimumVisibleAlpha = 0.05f;

    private void Awake()
    {
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (!graphic.raycastTarget)
                continue;

            if (graphic is not Image image)
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
            else if (image.GetComponentInParent<Selectable>() == null)
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
}
