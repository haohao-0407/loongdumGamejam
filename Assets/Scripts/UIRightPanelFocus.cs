using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Moves an exposed right-side UI page left first, then up.
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
    }

    [SerializeField, Min(0.05f)] private float moveDuration = 0.8f;
    [SerializeField, Range(0.05f, 0.95f)] private float horizontalTimeShare = 0.5f;
    [SerializeField] private PanelBinding[] panels = Array.Empty<PanelBinding>();

    private Vector2[] homePositions;
    private int[] homeSiblingIndices;
    private int focusedIndex = -1;
    private Coroutine movement;
    private bool isClosing;

    private void Awake()
    {
        homePositions = new Vector2[panels.Length];
        homeSiblingIndices = new int[panels.Length];

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i].panel == null)
                continue;

            homePositions[i] = panels[i].panel.anchoredPosition;
            homeSiblingIndices[i] = panels[i].panel.GetSiblingIndex();
        }
    }

    public void Focus(RectTransform panel)
    {
        if (panel == null)
            return;

        int index = Array.FindIndex(panels, entry => entry.panel == panel);
        if (index < 0 || index == focusedIndex)
            return;

        if (movement != null)
        {
            StopCoroutine(movement);
            movement = null;
        }

        if (focusedIndex >= 0)
            Restore(focusedIndex);

        isClosing = false;

        Vector2 start = panel.anchoredPosition;
        Vector2 afterLeftMove = start + Vector2.left * panels[index].leftDistance;
        Vector2 destination = afterLeftMove + Vector2.up * panels[index].upDistance;

        focusedIndex = index;
        panel.SetAsLastSibling();
        movement = StartCoroutine(MoveInTwoSteps(panel, start, afterLeftMove, destination));
    }

    public void Close(RectTransform panel)
    {
        if (focusedIndex < 0 || panels[focusedIndex].panel != panel || isClosing)
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

    private IEnumerator MoveInTwoSteps(RectTransform panel, Vector2 start, Vector2 afterLeftMove, Vector2 destination)
    {
        float duration = Mathf.Max(0.05f, moveDuration);
        yield return Interpolate(panel, start, afterLeftMove, duration * horizontalTimeShare);
        yield return Interpolate(panel, afterLeftMove, destination, duration * (1f - horizontalTimeShare));
        movement = null;
    }

    private IEnumerator MoveBackAlongPath(RectTransform panel, Vector2 current, Vector2 afterLeftMove, Vector2 home, int index)
    {
        float duration = Mathf.Max(0.05f, moveDuration);

        // An early click during the first leg returns directly to its starting
        // point; otherwise the page travels down to the corner, then right.
        bool stillOnFirstLeg = Mathf.Abs(current.y - home.y) < 0.01f &&
                               Mathf.Abs(current.x - afterLeftMove.x) > 0.01f;
        if (!stillOnFirstLeg)
            yield return Interpolate(panel, current, afterLeftMove, duration * (1f - horizontalTimeShare));

        yield return Interpolate(panel, stillOnFirstLeg ? current : afterLeftMove, home, duration * horizontalTimeShare);
        Restore(index);
        focusedIndex = -1;
        isClosing = false;
        movement = null;
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

        panel.anchoredPosition = homePositions[index];
        panel.SetSiblingIndex(homeSiblingIndices[index]);
    }

    private void OnDisable()
    {
        if (movement != null)
        {
            StopCoroutine(movement);
            movement = null;
        }

        if (focusedIndex >= 0)
        {
            Restore(focusedIndex);
            focusedIndex = -1;
        }

        isClosing = false;
    }
}
