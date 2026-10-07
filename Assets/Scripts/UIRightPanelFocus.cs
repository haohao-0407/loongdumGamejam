using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Moves bound UI panels along their configured horizontal and vertical distances.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIRightPanelFocus : MonoBehaviour
{
    [Serializable]
    private struct PanelBinding
    {
        public RectTransform panel;
        [Min(-500f)] public float leftDistance;
        [Min(-500f)] public float upDistance;
        [Min(0f)] public float hoverLeftDistance;
        [Min(0.01f)] public float hoverDuration;
    }

    [SerializeField, Min(0.05f)] private float moveDuration = 0.8f;
    [SerializeField, Range(0.05f, 0.95f)] private float horizontalTimeShare = 0.5f;
    [SerializeField] private PanelBinding[] panels = Array.Empty<PanelBinding>();

    private Vector2[] homePositions;
    private int[] homeSiblingIndices;
    private Coroutine[] hoverMovements;
    private bool[] hovered;
    private bool[] dismissOnFocus;
    private bool[] dismissed;
    private int focusedIndex = -1;
    private int siblingToRestoreOnEnable = -1;
    private Coroutine movement;
    private bool isClosing;

    private void Awake()
    {
        homePositions = new Vector2[panels.Length];
        homeSiblingIndices = new int[panels.Length];
        hoverMovements = new Coroutine[panels.Length];
        hovered = new bool[panels.Length];
        dismissOnFocus = new bool[panels.Length];
        dismissed = new bool[panels.Length];

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i].panel == null)
                continue;

            homePositions[i] = panels[i].panel.anchoredPosition;
            homeSiblingIndices[i] = panels[i].panel.GetSiblingIndex();
        }
    }

    private void OnEnable()
    {
        if (siblingToRestoreOnEnable >= 0)
            StartCoroutine(RestoreSiblingAfterActivation());
    }

    private IEnumerator RestoreSiblingAfterActivation()
    {
        // Unity rejects sibling changes while the parent Canvas is activating.
        yield return null;

        if (!isActiveAndEnabled || siblingToRestoreOnEnable < 0)
            yield break;

        int index = siblingToRestoreOnEnable;
        siblingToRestoreOnEnable = -1;
        RectTransform panel = panels[index].panel;
        if (panel != null)
            Restore(index);
    }

    public void Focus(RectTransform panel)
    {
        if (panel == null)
            return;

        int index = Array.FindIndex(panels, entry => entry.panel == panel);
        if (index < 0 || dismissed[index] || (index == focusedIndex && !isClosing))
            return;

        if (movement != null)
        {
            StopCoroutine(movement);
            movement = null;
        }

        StopHover(index);

        if (focusedIndex >= 0 && focusedIndex != index)
        {
            int previousIndex = focusedIndex;
            Restore(focusedIndex);
            StartHover(previousIndex);
        }

        isClosing = false;

        Vector2 start = panel.anchoredPosition;
        Vector2 afterLeftMove = homePositions[index] + Vector2.left * panels[index].leftDistance;
        Vector2 destination = afterLeftMove + Vector2.up * panels[index].upDistance;

        focusedIndex = index;
        dismissed[index] = dismissOnFocus[index];
        panel.SetAsLastSibling();
        movement = StartCoroutine(MoveInTwoSteps(panel, start, afterLeftMove, destination, index));
    }

    public void Toggle(RectTransform panel)
    {
        if (focusedIndex >= 0 && panels[focusedIndex].panel == panel && !isClosing)
            Close(panel);
        else
            Focus(panel);
    }

    /// <summary>Reset page motion when moving between the menu and a level.</summary>
    public void ResetPanels()
    {
        if (homePositions == null) return;
        StopAllCoroutines();
        movement = null;
        if (focusedIndex >= 0) Restore(focusedIndex);
        else if (siblingToRestoreOnEnable >= 0) Restore(siblingToRestoreOnEnable);
        for (int i = 0; i < panels.Length; i++)
        {
            hoverMovements[i] = null;
            hovered[i] = false;
            dismissed[i] = false;
            if (panels[i].panel != null) panels[i].panel.anchoredPosition = homePositions[i];
        }
        focusedIndex = -1;
        siblingToRestoreOnEnable = -1;
        isClosing = false;
    }

    /// <summary>Replay a page's existing return animation from its configured offset.</summary>
    public void ReplayReturn(RectTransform panel, bool dismissAfterClick = false)
    {
        if (panel == null || homePositions == null) return;
        int index = Array.FindIndex(panels, entry => entry.panel == panel);
        if (index < 0) return;
        ResetPanels();
        dismissOnFocus[index] = dismissAfterClick;
        panel.gameObject.SetActive(true);
        panel.anchoredPosition = homePositions[index] + Vector2.left * panels[index].leftDistance +
            Vector2.up * panels[index].upDistance;
        panel.SetAsLastSibling();
        focusedIndex = index;
        Close(panel);
    }

    public void SetHovered(RectTransform panel, bool isHovered)
    {
        if (panel == null)
            return;

        int index = Array.FindIndex(panels, entry => entry.panel == panel);
        if (index < 0)
            return;

        hovered[index] = isHovered;
        if (index != focusedIndex)
            StartHover(index);
    }

    private void StartHover(int index)
    {
        StopHover(index);

        RectTransform panel = panels[index].panel;
        if (panel == null || dismissed[index] || !panel.gameObject.activeInHierarchy)
            return;

        Vector2 destination = homePositions[index] +
            Vector2.left * (hovered[index] ? panels[index].hoverLeftDistance : 0f);
        if ((panel.anchoredPosition - destination).sqrMagnitude < 0.0001f)
            return;

        hoverMovements[index] = StartCoroutine(MoveHover(index, panel, destination));
    }

    private void StopHover(int index)
    {
        if (hoverMovements[index] == null)
            return;

        StopCoroutine(hoverMovements[index]);
        hoverMovements[index] = null;
    }

    private IEnumerator MoveHover(int index, RectTransform panel, Vector2 destination)
    {
        yield return Interpolate(panel, panel.anchoredPosition, destination,
            Mathf.Max(0.01f, panels[index].hoverDuration));
        hoverMovements[index] = null;
    }

    public void Close(RectTransform panel)
    {
        if (focusedIndex < 0 || dismissed[focusedIndex] || panels[focusedIndex].panel != panel || isClosing)
            return;

        if (movement != null)
        {
            StopCoroutine(movement);
            movement = null;
        }

        int index = focusedIndex;
        Vector2 home = homePositions[index];
        Vector2 afterLeftMove = home + Vector2.left * panels[index].leftDistance;
        Vector2 current = panel.anchoredPosition;
        isClosing = true;
        movement = StartCoroutine(MoveBackAlongPath(panel, current, afterLeftMove, home, index));
    }

    private IEnumerator MoveInTwoSteps(RectTransform panel, Vector2 start, Vector2 afterLeftMove, Vector2 destination, int index)
    {
        float duration = Mathf.Max(0.05f, moveDuration);
        if ((start - afterLeftMove).sqrMagnitude < 0.0001f)
            yield return Interpolate(panel, start, destination, duration);
        else if ((afterLeftMove - destination).sqrMagnitude < 0.0001f)
            yield return Interpolate(panel, start, afterLeftMove, duration);
        else
        {
            yield return Interpolate(panel, start, afterLeftMove, duration * horizontalTimeShare);
            yield return Interpolate(panel, afterLeftMove, destination, duration * (1f - horizontalTimeShare));
        }
        movement = null;
        if (dismissed[index]) panel.gameObject.SetActive(false);
    }

    private IEnumerator MoveBackAlongPath(RectTransform panel, Vector2 current, Vector2 afterLeftMove, Vector2 home, int index)
    {
        float duration = Mathf.Max(0.05f, moveDuration);

        // An early click during the first leg returns directly to its starting
        // point; otherwise the page travels down to the corner, then right.
        if ((afterLeftMove - home).sqrMagnitude < 0.0001f)
            yield return Interpolate(panel, current, home, duration);
        else
        {
            bool stillOnFirstLeg = Mathf.Abs(current.y - home.y) < 0.01f &&
                                   Mathf.Abs(current.x - afterLeftMove.x) > 0.01f;
            if (!stillOnFirstLeg)
                yield return Interpolate(panel, current, afterLeftMove, duration * (1f - horizontalTimeShare));

            yield return Interpolate(panel, stillOnFirstLeg ? current : afterLeftMove, home, duration * horizontalTimeShare);
        }
        Restore(index);
        focusedIndex = -1;
        isClosing = false;
        movement = null;
        StartHover(index);
    }

    private static IEnumerator Interpolate(RectTransform panel, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float fraction = Mathf.Clamp01(elapsed / duration);
            panel.anchoredPosition = Vector2.LerpUnclamped(from, to, Mathf.SmoothStep(0f, 1f, fraction));
            yield return null;
        }

        panel.anchoredPosition = to;
    }

    private void Restore(int index)
    {
        RectTransform panel = panels[index].panel;
        if (panel == null)
            return;

        // A dismissed entry Dialog must never be restored into view by a
        // sidebar switch, even if that switch interrupted its exit animation.
        panel.anchoredPosition = dismissed[index]
            ? homePositions[index] + Vector2.left * panels[index].leftDistance + Vector2.up * panels[index].upDistance
            : homePositions[index];
        if (dismissed[index]) panel.gameObject.SetActive(false);
        panel.SetSiblingIndex(homeSiblingIndices[index]);
    }

    private void OnDisable()
    {
        if (movement != null)
        {
            StopCoroutine(movement);
            movement = null;
        }

        if (hoverMovements != null)
        {
            for (int i = 0; i < hoverMovements.Length; i++)
            {
                StopHover(i);
                hovered[i] = false;
                if (panels[i].panel != null)
                    panels[i].panel.anchoredPosition = homePositions[i];
            }
        }

        if (focusedIndex >= 0)
        {
            // Positions are reset above. Reorder only after Canvas is active again.
            siblingToRestoreOnEnable = focusedIndex;
            focusedIndex = -1;
        }

        isClosing = false;
    }
}
