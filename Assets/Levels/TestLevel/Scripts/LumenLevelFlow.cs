using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Scene-local objective, player locator and restart for TestLevel.</summary>
[DisallowMultipleComponent]
public sealed class LumenLevelFlow : MonoBehaviour
{
    [SerializeField] private CharacterController player;
    [SerializeField] private WhiteboxPlayerMovement movement;
    [SerializeField] private VisionSource source;
    [SerializeField] private Camera levelCamera;
    [SerializeField] private Transform spawn;
    [SerializeField] private Transform[] routeMilestones;
    [SerializeField] private Text objectiveText;
    [SerializeField] private Text stageText;
    [SerializeField] private Text timerText;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private Text victoryTimeText;
    [SerializeField] private RectTransform playerLocator;
    [SerializeField] private RectTransform locatorCanvas;
    [SerializeField] private RectTransform[] progressSegments;
    [SerializeField, Min(0.1f)] private float arrivalRadius = 1.35f;

    private float elapsed;
    private int reachedMilestones;
    private bool completed;
    private bool started;
    private readonly string[] stageNames =
    {
        "01  /  GLASS PASSAGE", "02  /  SHADOW TURN", "03  /  INNER COURT"
    };

    public bool Completed => completed;
    public int ReachedMilestones => reachedMilestones;
    public float Elapsed => elapsed;

    private void Start()
    {
        ResetLevel();
    }

    private void Update()
    {
        Keyboard keys = Keyboard.current;
        if (keys != null && keys.rKey.wasPressedThisFrame)
            ResetLevel();

        if (player == null || source == null) return;
        Vector3 position = player.transform.position;
        if (!started && spawn != null && FlatDistance(position, spawn.position) > 0.1f)
            started = true;
        if (started && !completed) elapsed += Time.deltaTime;

        // Milestones describe progress; the goal never requires artificial checkpoint locks.
        if (!completed && routeMilestones != null && reachedMilestones < routeMilestones.Length
            && FlatDistance(position, routeMilestones[reachedMilestones].position) < 1.6f)
        {
            reachedMilestones++;
            RefreshProgress();
        }

        if (!completed && FlatDistance(position, source.transform.position) <= arrivalRadius)
        {
            completed = true;
            if (movement != null) movement.enabled = false;
            if (victoryPanel != null) victoryPanel.SetActive(true);
            if (victoryTimeText != null) victoryTimeText.text = "TIME  " + FormatTime(elapsed);
            if (objectiveText != null) objectiveText.text = "Vision Source reached.";
            if (stageText != null) stageText.text = "COMPLETE  /  LUMEN CORRIDOR";
            RefreshProgress();
        }

        if (timerText != null) timerText.text = FormatTime(elapsed);
        // The floor is bounded, but recover if an editor move or physics glitch drops the player.
        if (position.y < -5f) ResetLevel();
    }

    private void LateUpdate()
    {
        if (playerLocator == null || levelCamera == null || player == null) return;
        Vector3 screen = levelCamera.WorldToScreenPoint(player.transform.position + Vector3.up * 0.9f);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(locatorCanvas,
            screen, null, out Vector2 local)) playerLocator.anchoredPosition = local;
    }

    public void ResetLevel()
    {
        if (player == null || spawn == null) return;
        player.enabled = false;
        player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        player.enabled = true;
        if (movement != null) movement.enabled = true;
        completed = false;
        started = false;
        elapsed = 0f;
        reachedMilestones = 0;
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (objectiveText != null) objectiveText.text = "Reach the Vision Source";
        RefreshProgress();
    }

    private void RefreshProgress()
    {
        int stage = completed ? 3 : Mathf.Min(2, reachedMilestones);
        if (stageText != null && !completed) stageText.text = stageNames[stage];
        if (progressSegments == null) return;
        for (int i = 0; i < progressSegments.Length; i++)
        {
            Image image = progressSegments[i] != null ? progressSegments[i].GetComponent<Image>() : null;
            if (image != null) image.color = i <= stage
                ? new Color(0.95f, 0.84f, 0.5f) : new Color(0.35f, 0.35f, 0.34f);
        }
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
        => new Vector2(a.x - b.x, a.z - b.z).magnitude;

    private static string FormatTime(float time)
        => ((int)time / 60).ToString("00") + ":" + ((int)time % 60).ToString("00");
}
