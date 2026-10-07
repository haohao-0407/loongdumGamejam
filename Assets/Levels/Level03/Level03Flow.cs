using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Only owns this scene's lever wiring, pressure shutter and objective.</summary>
[DisallowMultipleComponent]
public sealed class Level03Flow : MonoBehaviour
{
    public CharacterController player;
    public VisionSource source;
    public BodyReversal reversal;
    public Camera levelCamera;
    public Transform[] levers;
    public Animator[] doors;
    public Collider[] barriers;
    public Renderer[] leverHandles;
    public Renderer plateMarker;
    public Material idleMarker;
    public Material activeMarker;
    public Vector3 lowerSpawn;
    public Vector3 upperSpawn;

    [Header("音效（留空则不发声）")]
    [Tooltip("按 F 拨动拉杆成功时播放的音效，例如「SFX_UI_BottonClick」。")]
    [SerializeField] private AudioClip interactClip;

    [Tooltip("交互音效音量。")]
    [SerializeField, Range(0f, 1f)] private float interactVolume = 1f;

    public bool AOpened { get; private set; }
    public bool COpened { get; private set; }
    public bool BObserved { get; private set; }
    public bool BOpened { get; private set; }
    public bool PlateHeld { get; private set; }
    public bool Completed { get; private set; }
    public int Stage { get; private set; }
    private Vector3 lastUpper;
    private WhiteboxPlayerMovement movement;
    private GUIStyle bodyStyle;
    private GUIStyle titleStyle;

    private void Start()
    {
        movement = player.GetComponent<WhiteboxPlayerMovement>();
        ResetLevel();
    }

    private static float Distance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    private void LateUpdate()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame) ResetLevel();
        if (Completed) return;
        if (player.transform.position.y < -4f) { ResetLevel(); return; }

        // BodyReversal remains responsible for the skill and its physical checks.
        if (Distance(lastUpper, source.transform.position) > .1f)
        {
            lastUpper = source.transform.position;
            if (Stage == 0 && Distance(lastUpper, Level03Layout.FirstTile) < 1.5f)
            {
                Stage = 1;
            }
            else if (Stage >= 1 && AOpened && COpened
                && Distance(lastUpper, Level03Layout.SecondTile) < 1f)
            {
                Stage = Mathf.Max(Stage, 3);
            }
        }

        bool held = player.isGrounded && Distance(player.transform.position, Level03Layout.Find('1')) < .85f;
        if (held != PlateHeld)
        {
            PlateHeld = held;
            SetDoor(3, held);
            plateMarker.sharedMaterial = held ? activeMarker : idleMarker;
        }

        if (keyboard != null && keyboard.fKey.wasPressedThisFrame) Interact();
        // Winning requires walking together, and never merely exchanging positions.
        // Both bodies are solid in Whitebox1. Allow their collision separation
        // and controller skin width, rather than requiring them to overlap.
        if (BOpened && player.isGrounded && Distance(player.transform.position, source.transform.position) < 1.1f)
        {
            Completed = true;
            Stage = 5;
            movement.enabled = false;
            reversal.enabled = false;
        }
    }

    private void OnEnable() => UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += Observe;
    private void OnDisable() => UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= Observe;
    private void Observe(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {
        // Query only after VisionSource has refreshed for this camera, so opening
        // a shutter cannot use last frame's visibility as evidence.
        if (!Application.isPlaying || camera != levelCamera || BObserved || !PlateHeld || Stage < 3) return;
        if (source.IsPointVisible(Level03Layout.Find('B', source.transform.position.y)))
        {
            BObserved = true;
            Stage = 4;
        }
    }

    /// <summary>按 F 时调用的交互入口。成功拨到拉杆才发交互音效。</summary>
    public bool Interact()
    {
        bool success = TryInteract();
        if (success)
            AudioOneShot.Play(interactClip, player != null ? player.gameObject : gameObject, interactVolume);
        return success;
    }

    private bool TryInteract()
    {
        if (Completed || !player.isGrounded) return false;
        int nearest = -1;
        float best = Level03Layout.CellSize * 1.52f;
        for (int i = 0; i < levers.Length; i++)
        {
            float d = Distance(player.transform.position, levers[i].position);
            if (d < best) { nearest = i; best = d; }
        }
        if (nearest < 0) return false;
        // Physical wall/door separation prevents reaching switches through walls.
        Vector3 start = player.transform.position;
        start.y = source.transform.position.y;
        Vector3 end = levers[nearest].position;
        end.y = start.y;
        foreach (RaycastHit hit in Physics.RaycastAll(start, (end - start).normalized,
                     Vector3.Distance(start, end) - .5f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(player.transform)) continue;
            if (hit.collider.transform.IsChildOf(levers[nearest])) continue;
            if (hit.collider.transform.IsChildOf(source.transform)) continue;
            return false;
        }
        if (nearest == 0 && !AOpened)
        {
            AOpened = true; SetDoor(0, true); MarkLever(0);
            return true;
        }
        if (nearest == 1 && !COpened)
        {
            if (!AOpened) return false;
            COpened = true; SetDoor(1, true); MarkLever(1); Stage = Mathf.Max(Stage, 2);
            return true;
        }
        if (nearest == 2 && !BOpened)
        {
            if (!BObserved) return false;
            BOpened = true; SetDoor(2, true); MarkLever(2);
            return true;
        }
        return false;
    }

    private void MarkLever(int index)
    {
        leverHandles[index].sharedMaterial = activeMarker;
        leverHandles[index].transform.localRotation = Quaternion.Euler(0, 0, -35);
    }

    private void SetDoor(int index, bool open)
    {
        doors[index].SetBool("DoorOpen", open);
        barriers[index].enabled = !open;
    }

    public void ResetLevel()
    {
        if (movement == null) movement = player.GetComponent<WhiteboxPlayerMovement>();
        player.enabled = false;
        player.transform.position = lowerSpawn;
        source.transform.position = upperSpawn;
        player.enabled = true;
        movement.enabled = true;
        reversal.enabled = true;
        Physics.SyncTransforms();
        AOpened = COpened = BObserved = BOpened = PlateHeld = Completed = false;
        Stage = 0;
        lastUpper = upperSpawn;
        for (int i = 0; i < doors.Length; i++) SetDoor(i, false);
        for (int i = 0; i < leverHandles.Length; i++)
        {
            leverHandles[i].sharedMaterial = idleMarker;
            leverHandles[i].transform.localRotation = Quaternion.identity;
        }
        plateMarker.sharedMaterial = idleMarker;
    }

    private void OnGUI()
    {
        if (bodyStyle == null)
        {
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            titleStyle = new GUIStyle(bodyStyle) { fontSize = 23, fontStyle = FontStyle.Bold };
        }
        // Editor hot reload can retain cached GUIStyle instances from the old
        // skin. Set contrast every draw rather than only during allocation.
        bodyStyle.normal.textColor = Color.white;
        titleStyle.normal.textColor = Color.white;
        Vector3 screen = levelCamera.WorldToScreenPoint(player.transform.position + Vector3.up);
        GUI.Label(new Rect(screen.x - 12, Screen.height - screen.y - 25, 35, 35), "▼", titleStyle);
        if (BObserved && !BOpened)
        {
            Vector3 marker = levelCamera.WorldToScreenPoint(Level03Layout.Find('B', .7f));
            GUI.Box(new Rect(marker.x - 44, Screen.height - marker.y - 30, 104, 36), "");
            GUI.Label(new Rect(marker.x - 40, Screen.height - marker.y - 26, 110, 35), "B → Z", bodyStyle);
        }
    }
}
