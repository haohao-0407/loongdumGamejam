using UnityEngine;
using UnityEngine.UI;

/// <summary>Returns its page along the same two segments in reverse order.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UIRightPanelCloseButton : MonoBehaviour
{
    [SerializeField] private UIRightPanelFocus focusController;
    [SerializeField] private RectTransform panel;

    private Button button;

    private void OnEnable()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(Close);
    }

    private void Close()
    {
        if (focusController != null)
            focusController.Close(panel);
    }
}
