using UnityEngine;

/// <summary>
/// 挂在「脚」（玩家）上：头和脚贴到一起就判定游戏结束，冻住操作并在画面中央显示提示。
/// </summary>
[DisallowMultipleComponent]
public sealed class HeadFeetGameOver : MonoBehaviour
{
    [Tooltip("头（视野源）的 Transform。")]
    [SerializeField] private Transform head;

    [Tooltip("头和脚的距离小于这个值就算撞上，单位米。")]
    [SerializeField, Min(0.05f)] private float contactDistance = 1f;

    [Tooltip("头在一帧里位移超过这个距离就当成刚换过位，短时间内不判定碰撞。")]
    [SerializeField, Min(0.01f)] private float swapJumpThreshold = 0.5f;

    [Tooltip("换位后多少秒内不判定碰撞。")]
    [SerializeField, Min(0f)] private float swapGrace = 0.5f;

    private WhiteboxPlayerMovement movement;
    private FullBodyReversal fullReversal;
    private BodyReversal tileReversal;
    private Vector3 lastHeadPosition;
    private float graceUntil;
    private bool isOver;

    public bool IsOver => isOver;

    public float DistanceToHead =>
        head != null ? Vector3.Distance(transform.position, head.position) : float.PositiveInfinity;

    private void Awake()
    {
        movement = GetComponent<WhiteboxPlayerMovement>();
        fullReversal = GetComponent<FullBodyReversal>();
        tileReversal = GetComponent<BodyReversal>();

        if (head == null)
        {
            Debug.LogError("HeadFeetGameOver 需要指定头（视野源）的 Transform。", this);
            enabled = false;
            return;
        }
        lastHeadPosition = head.position;
    }

    private void LateUpdate()
    {
        if (head == null) return;

        // 头瞬移 = 刚换过位，给一小段宽限，避免换位瞬间误判
        if ((head.position - lastHeadPosition).sqrMagnitude > swapJumpThreshold * swapJumpThreshold)
            graceUntil = Time.time + swapGrace;
        lastHeadPosition = head.position;

        if (isOver || Time.time < graceUntil) return;
        if (DistanceToHead <= contactDistance) GameOver();
    }

    private void GameOver()
    {
        isOver = true;

        // 冻住操作：移动和两种反转术式都关掉，画面停下来
        if (movement != null) movement.enabled = false;
        if (fullReversal != null) fullReversal.enabled = false;
        if (tileReversal != null) tileReversal.enabled = false;

        Vector3 p = transform.position;
        Debug.Log(string.Format(
            "[HeadFeetGameOver] 头和脚撞上了，游戏结束。\n  脚位置 X {0:F2}  Y {1:F2}  Z {2:F2}\n  头位置 X {3:F2}  Y {4:F2}  Z {5:F2}\n  距离 {6:F2} 米",
            p.x, p.y, p.z, head.position.x, head.position.y, head.position.z, DistanceToHead), this);
    }

    private void OnGUI()
    {
        if (!isOver) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 40;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.red;
        GUI.Label(new Rect(0f, Screen.height * 0.5f - 50f, Screen.width, 100f), "游戏结束", style);
    }
}
