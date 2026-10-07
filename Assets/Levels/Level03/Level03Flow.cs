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

    public bool AOpened { get; private set; }
    public bool COpened { get; private set; }
    public bool BObserved { get; private set; }
    public bool BOpened { get; private set; }
    public bool PlateHeld { get; private set; }
    public bool Completed { get; private set; }
    public int Stage { get; private set; }
    private string feedback = "找到南翼西北的 E 反转格，进入远处的玻璃房。";
    private float feedbackUntil;
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
                Say("反转成功：光移到南翼。房内 A 开 X；再用 C 开出口 D。");
            }
            else if (Stage >= 1 && AOpened && COpened
                && Distance(lastUpper, Level03Layout.SecondTile) < 1f)
            {
                Stage = Mathf.Max(Stage, 3);
                Say("光已移到走廊。回南翼踩 1，让光穿过 a、玻璃和三个光门，找到 B。");
            }
        }

        bool held = player.isGrounded && Distance(player.transform.position, Level03Layout.Find('1')) < .85f;
        if (held != PlateHeld)
        {
            PlateHeld = held;
            SetDoor(3, held);
            plateMarker.sharedMaterial = held ? activeMarker : idleMarker;
            Say(held ? "1 已压下：a 开启。观察光门链是否照到 B。" : "离开 1：a 关闭。已看清的 B 标记会保留。");
        }

        if (keyboard != null && keyboard.eKey.wasPressedThisFrame) Interact();
        // Winning requires walking together, and never merely exchanging positions.
        // Both bodies are solid in Whitebox1. Allow their collision separation
        // and controller skin width, rather than requiring them to overlap.
        if (BOpened && player.isGrounded && Distance(player.transform.position, source.transform.position) < 1.1f)
        {
            Completed = true;
            Stage = 5;
            movement.enabled = false;
            reversal.enabled = false;
            Say("上下半身汇合。第三关完成！按 R 重新开始。");
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
            Say("已看清 B → Z。松开板也不会丢失线索：穿过 X，去 B 按 E。");
        }
    }

    public bool Interact()
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
            Say("机关被实体隔开，先走到它旁边。");
            return false;
        }
        if (nearest == 0 && !AOpened)
        {
            AOpened = true; SetDoor(0, true); MarkLever(0);
            Say("A 已锁定：南翼的 X 门开启。C 的线路现在可用。");
            return true;
        }
        if (nearest == 1 && !COpened)
        {
            if (!AOpened) { Say("C → D：先接通 A → X 的线路。"); return false; }
            COpened = true; SetDoor(1, true); MarkLever(1); Stage = Mathf.Max(Stage, 2);
            Say("C 已锁定：D 出口开启。沿走廊走到第二个 E 反转格。");
            return true;
        }
        if (nearest == 2 && !BOpened)
        {
            if (!BObserved) { Say("B 的线路尚未辨认：先把光移到走廊，再踩 1 照亮 B。"); return false; }
            BOpened = true; SetDoor(2, true); MarkLever(2);
            Say("B 已锁定：Z 开启。穿过 Z，走向走廊里的上半身。");
            return true;
        }
        Say("这条线路已接通，门会保持开启。");
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

    private void Say(string text) { feedback = text; feedbackUntil = Time.time + 7f; }

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
        Say("找到南翼西北的 E 反转格，进入远处的玻璃房。");
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
        GUI.Box(new Rect(12, 12, Mathf.Min(720, Screen.width - 24), 160), "");
        GUI.Label(new Rect(26, 20, 650, 32), "第三关 · 光线门    " + Mathf.Min(Stage + 1, 5) + " / 5", titleStyle);
        GUI.Label(new Rect(26, 56, 650, 30), "WASD 移动   E 站格反转 / 拨动拉杆   R 重置", bodyStyle);
        string[] tasks = { "找到西北的反转格；E 进入玻璃房。", "房内先 A → X，再 C → D。",
            "沿 D 出口走到走廊南端，站格按 E。", "南翼踩 1，观察光门链，辨认 B。", "穿 X，B → Z；穿 Z 与上半身汇合。", "已完成；R 可重新开始。" };
        GUI.Label(new Rect(26, 90, 660, 64), Time.time < feedbackUntil ? feedback : tasks[Stage], bodyStyle);
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
