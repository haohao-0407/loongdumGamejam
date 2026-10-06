using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>Scene-local wiring; shared movement, reversal and vision remain authoritative.</summary>
[DisallowMultipleComponent]
public sealed class Level04Flow : MonoBehaviour
{
    public CharacterController player;
    public VisionSource source;
    public BodyReversal reversal;
    public Camera levelCamera;
    public Transform[] levers; // A, B, C.
    public Animator[] doors; // X, D, Y, Z, a.
    public Collider[] barriers;
    public Renderer[] leverHandles;
    public Renderer plateMarker;
    public Material idleMarker;
    public Material activeMarker;
    public Vector3 lowerSpawn;
    public Vector3 upperSpawn;
    public bool AOpened { get; private set; }
    public bool BObserved { get; private set; }
    public bool BOpened { get; private set; }
    public bool COpened { get; private set; }
    public bool PlateHeld { get; private set; }
    public bool Completed { get; private set; }
    public int Stage { get; private set; }
    private WhiteboxPlayerMovement movement;
    private Vector3 lastUpper;
    private string feedback;
    private float feedbackUntil;
    private GUIStyle textStyle;
    private GUIStyle titleStyle;

    private static float Distance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    private void Start() => ResetLevel();
    private void OnEnable() => RenderPipelineManager.endCameraRendering += Observe;
    private void OnDisable() => RenderPipelineManager.endCameraRendering -= Observe;
    private void LateUpdate()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame) ResetLevel();
        if (Completed) return;
        if (player.transform.position.y < -4f) { ResetLevel(); return; }
        if (Distance(lastUpper, source.transform.position) > .1f)
        {
            lastUpper = source.transform.position;
            if (Stage == 0 && Distance(lastUpper, Level04Layout.FirstTile) < 1.5f)
            {
                Stage = 1;
                Say("进入中央观察室。光留在庭院；靠近 A 按 E，同时打开 D 与 X。");
            }
            else if (AOpened && Distance(lastUpper, Level04Layout.SecondTile) < .8f)
            {
                Stage = Mathf.Max(Stage, 3);
                Say("眼睛留在东廊。腿回到庭院；穿 X，向北踩 1，打开光闸 a。");
            }
        }
        // Match the square plate's visible footprint, including its corners.
        var offset = player.transform.position - Level04Layout.Find('1');
        bool held = player.isGrounded && Mathf.Abs(offset.x) <= .825f && Mathf.Abs(offset.z) <= .825f;
        if (held != PlateHeld)
        {
            PlateHeld = held; SetDoor(4, held);
            plateMarker.sharedMaterial = held ? activeMarker : idleMarker;
            Say(held ? "1 已压下：a 开启，视线穿过门户。" : "离开 1：a 关闭；已辨认的 B 线索保留。");
        }
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame) Interact();
        if (COpened && player.isGrounded && Distance(player.transform.position, source.transform.position) < 1.1f)
        {
            Completed = true; Stage = 5;
            movement.enabled = false; reversal.enabled = false;
            Say("隔窗归途完成：上下半身汇合。按 R 重新开始。");
        }
    }
    private void Observe(ScriptableRenderContext context, Camera camera)
    {
        if (!Application.isPlaying || camera != levelCamera || BObserved || !PlateHeld || Stage < 3) return;
        if (!source.IsPointVisible(Level04Layout.Find('B', source.transform.position.y))) return;
        BObserved = true; Stage = 4;
        Say("已辨认 B → Y。离板后向南找 B；进入西控制室，C 才能打开归路 Z。");
    }
    public bool Interact()
    {
        if (Completed || !player.isGrounded) return false;
        int nearest = -1; float best = 2.6f;
        for (int i = 0; i < levers.Length; i++)
        {
            float d = Distance(player.transform.position, levers[i].position);
            if (d < best) { best = d; nearest = i; }
        }
        if (nearest < 0) { Say("走到拉杆旁再按 E。门由对应机关控制。"); return false; }
        var start = player.transform.position; start.y = source.transform.position.y;
        var end = levers[nearest].position; end.y = start.y;
        foreach (var hit in Physics.RaycastAll(start, (end - start).normalized,
            Mathf.Max(0, Vector3.Distance(start, end) - .45f), Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(player.transform)
                || hit.collider.transform.IsChildOf(source.transform)
                || hit.collider.transform.IsChildOf(levers[nearest])) continue;
            Say("机关被实体隔开，先走到它旁边。"); return false;
        }
        if (nearest == 0 && !AOpened)
        {
            AOpened = true; SetDoor(0, true); SetDoor(1, true); MarkLever(0);
            Stage = Mathf.Max(Stage, 2);
            Say("A 已接通：D 与 X 开启。穿 D，向南找到东廊的 F②。"); return true;
        }
        if (nearest == 1 && !BOpened)
        {
            if (!BObserved) { Say("B → Y 的线路尚未辨认：先把眼睛留在东廊，再踩 1。"); return false; }
            BOpened = true; SetDoor(2, true); MarkLever(1);
            Say("B 已接通：Y 开启。向北进入西控制室，找到 C → Z。"); return true;
        }
        if (nearest == 2 && !COpened)
        {
            COpened = true; SetDoor(3, true); MarkLever(2);
            Say("C 已接通：Z 开启。沿北侧归廊向东，再向南与上半身汇合。"); return true;
        }
        Say("线路已接通，门保持开启。"); return false;
    }
    private void SetDoor(int index, bool open)
    {
        doors[index].SetBool("DoorOpen", open); barriers[index].enabled = !open;
    }
    private void MarkLever(int index)
    {
        leverHandles[index].sharedMaterial = activeMarker;
        leverHandles[index].transform.localRotation = Quaternion.Euler(0, 0, -35);
    }
    private void Say(string message) { feedback = message; feedbackUntil = Time.time + 8f; }
    public void ResetLevel()
    {
        if (movement == null) movement = player.GetComponent<WhiteboxPlayerMovement>();
        player.enabled = false;
        player.transform.position = lowerSpawn; source.transform.position = upperSpawn;
        player.enabled = true; movement.enabled = true; reversal.enabled = true;
        AOpened = BObserved = BOpened = COpened = PlateHeld = Completed = false;
        Stage = 0; lastUpper = upperSpawn;
        for (int i = 0; i < doors.Length; i++) SetDoor(i, false);
        foreach (var handle in leverHandles)
        {
            handle.sharedMaterial = idleMarker; handle.transform.localRotation = Quaternion.identity;
        }
        plateMarker.sharedMaterial = idleMarker; Physics.SyncTransforms();
        Say("上半身在中央观察室。走到庭院 F①，落地后按 F 交换进入。");
    }
    private void OnGUI()
    {
        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            titleStyle = new GUIStyle(textStyle) { fontSize = 23, fontStyle = FontStyle.Bold };
        }
        textStyle.normal.textColor = titleStyle.normal.textColor = Color.white;
        GUI.Box(new Rect(12, 12, Mathf.Min(720, Screen.width - 24), 160), "");
        GUI.Label(new Rect(26, 20, 650, 32), "第四关 · 隔窗归途    " + Mathf.Min(Stage + 1, 5) + " / 5", titleStyle);
        GUI.Label(new Rect(26, 56, 650, 30), "WASD 移动   F 站格反转   E 邻近拉杆   R 重置", textStyle);
        string[] tasks = { "庭院 F① → 中央观察室。", "靠近 A，同时打开 D 与 X。",
            "穿 D，向南找到东廊 F②；把眼睛留在这里。", "庭院穿 X，向北踩 1；看清 B。",
            BOpened ? "进入 Y，操作 C，穿 Z 与上半身汇合。" : "向南找 B → Y，然后进入西控制室。", "已完成；R 可重新开始。" };
        GUI.Label(new Rect(26, 90, 660, 64), Time.time < feedbackUntil ? feedback : tasks[Stage], textStyle);
        var p = levelCamera.WorldToScreenPoint(player.transform.position + Vector3.up);
        GUI.Label(new Rect(p.x - 12, Screen.height - p.y - 25, 35, 35), "▼", titleStyle);
        if (BObserved && !BOpened)
        {
            var m = levelCamera.WorldToScreenPoint(Level04Layout.Find('B', .7f));
            GUI.Box(new Rect(m.x - 44, Screen.height - m.y - 30, 104, 36), "");
            GUI.Label(new Rect(m.x - 40, Screen.height - m.y - 26, 110, 35), "B → Y", textStyle);
        }
    }
}
