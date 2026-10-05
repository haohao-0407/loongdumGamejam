# 白盒玩家动画驱动 交接与评审说明

> 工程：`D:\unity\Vampire Hunt\loongdumGamejam`（Unity 6000.6.4f1 / URP / Linear）
> 场景：`Assets/Scenes/Whitebox1.unity`
> 角色：`whiteboxplayer`
> 目的：让策划侧接入的精灵动画能够跟随移动状态播放，并在向左移动时翻转朝向。
> 状态：功能已实现，策划已 Play 验收通过；**代码尚未提交，本文件用于主程序 review**。

---

## 0. 一句话总结

新增 **1 个 105 行的脚本** `WhiteboxPlayerAnimator.cs`，只做一件事：**读**移动数据 → 写 Animator 参数 + 写 `SpriteRenderer.flipX`。
它**不参与移动**，`WhiteboxPlayerMovement.cs` **一行未改**，两者通过 `CharacterController.velocity` 单向耦合。

---

## 1. 改动清单

`git status` 口径（HEAD = `a531873 添加了粒子效果`）：

### 1.1 本次待 review 的改动

| 文件 | 类型 | 规模 | 内容 |
| --- | --- | --- | --- |
| `Assets/Scripts/WhiteboxPlayerAnimator.cs` | **新增** | 105 行 | 移动 → 动画 + 朝向翻转的桥接脚本 |
| `Assets/Scripts/WhiteboxPlayerAnimator.cs.meta` | 新增 | 8 行 | guid `8aeedda7308364d45975108106995f5d` |
| `Assets/Animation/AS_Legs/5TW_AMC.controller` | 修改 | +323 / −0 | 加 2 个参数 + 接成 12 条 transition 的完全图 |
| `Assets/Scenes/Whitebox1.unity` | 修改 | +280 / −30 | 玩家实例挂上桥接脚本并绑定引用（**详见 §9，此文件混有其他改动**） |

### 1.2 未改动（明确声明）

| 文件 | 状态 |
| --- | --- |
| `Assets/Scripts/WhiteboxPlayerMovement.cs` | **未修改**，guid `7b4c1c16b4c44cc45ae56f7d088b6488` |
| `Assets/Scripts/PlayerCollisionSmoke.cs` | 未修改 |
| `Assets/Prefabs/whiteboxplayer.prefab` | **未修改**（组件是加在场景实例上的，见 §7.1） |
| 所有 4 个 `.anim` 动画剪辑 | 未修改 |
| 未新增任何 asmdef / 包依赖 / 第三方资源 | — |

### 1.3 上一条 commit（`a531873`，浮尘部分，供对照）

已由策划自行提交，包含：`Assets/VFX/AmbientDust/{Textures/DustMote.png, Materials/M_AmbientDust.mat, Prefabs/AmbientDust.prefab}` 及 `Whitebox1.unity` 的挂载。该批改动**不含脚本**，详见 §8。

---

## 2. 需求与约束

策划原话：「不知道程序是怎么设定角色移动的，我需要监听角色移动的值来控制动画播放」，随后追加「向左移动人物需要翻转」。

由此确定的硬约束：

1. **不能改移动脚本**。`WhiteboxPlayerMovement` 是 `sealed`，且 `moveSpeed` 是 `[SerializeField] private`，**没有任何对外输出**（无 event、无 public 字段、无属性）。
2. **移动脚本不能依赖动画脚本**。反过来让动画脚本去读移动脚本才是最省事的解——但那样移动脚本就要开口子，属于「为了表现污染逻辑」。
3. **单局性能预算极低**（白盒阶段，后续要铺场面）。

⇒ 结论：**用引擎已有的、每帧都在更新的物理量作为数据源**，即 `CharacterController.velocity`。
它天然满足：① 无需改任何现有代码；② 已包含碰撞修正（撞墙自动归 0）；③ 是 Unity 常识级别的稳定 API。

---

## 3. 新增代码

### 3.1 全文（`Assets/Scripts/WhiteboxPlayerAnimator.cs`）

```csharp
using UnityEngine;

/// <summary>
/// Bridges planar movement to the Animator and flips the sprite to face the
/// direction of travel. Read-only with respect to movement: it samples
/// <see cref="CharacterController.velocity"/> and never writes to the controller.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public sealed class WhiteboxPlayerAnimator : MonoBehaviour
{
    public const int StateIdle = 0;
    public const int StateSide = 1;
    public const int StateForward = 2;
    public const int StateBehind = 3;

    private static readonly int MoveStateParam = Animator.StringToHash("MoveState");
    private static readonly int SpeedParam = Animator.StringToHash("Speed");

    [Tooltip("Planar speed below this counts as idle, in m/s.")]
    [SerializeField, Min(0f)] private float idleSpeedThreshold = 0.1f;

    [Tooltip("Share of the planar speed that must go along Z before the walk counts as forward/behind instead of side. 0.6 means diagonals read as forward/behind.")]
    [SerializeField, Range(0.1f, 1f)] private float forwardShareThreshold = 0.6f;

    [Tooltip("Sideways speed (m/s) required before the sprite flips. Walking straight forward or backward keeps the current facing.")]
    [SerializeField, Min(0f)] private float flipXThreshold = 0.1f;

    [Tooltip("Sprite to mirror. Leave empty to auto-grab a SpriteRenderer on this object or its children.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private CharacterController controller;
    private Animator animator;

    /// <summary>Last state pushed to the Animator. Read-only, handy for debugging.</summary>
    public int CurrentState { get; private set; }

    /// <summary>Last planar speed pushed to the Animator. Read-only, handy for debugging.</summary>
    public float CurrentSpeed { get; private set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void LateUpdate()
    {
        // controller.velocity already accounts for collision correction, so walking
        // into a wall reads as ~0 and the state falls back to idle.
        Vector3 velocity = controller.velocity;
        Vector2 planar = new Vector2(velocity.x, velocity.z);
        float speed = planar.magnitude;

        int state = StateIdle;

        if (speed > idleSpeedThreshold)
        {
            // +1 = away from the camera (W), -1 = towards the camera (S).
            float forwardShare = planar.y / speed;

            if (Mathf.Abs(forwardShare) >= forwardShareThreshold)
            {
                state = forwardShare > 0f ? StateBehind : StateForward;
            }
            else
            {
                state = StateSide;
            }
        }

        CurrentState = state;
        CurrentSpeed = speed;

        animator.SetInteger(MoveStateParam, state);
        animator.SetFloat(SpeedParam, speed);

        UpdateFacing(planar);
    }

    /// <summary>
    /// The art faces right, so travelling left mirrors the sprite. The threshold
    /// keeps straight forward/backward travel from toggling the flip every frame.
    /// </summary>
    private void UpdateFacing(Vector2 planar)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (Mathf.Abs(planar.x) <= flipXThreshold)
        {
            return;
        }

        spriteRenderer.flipX = planar.x < 0f;
    }
}
```

### 3.2 设计取舍（评审重点）

| 决策 | 原因 | 备选方案及否掉的理由 |
| --- | --- | --- |
| 数据源 = `CharacterController.velocity` | 移动脚本用的是 `controller.Move()`，`velocity` 即它自己算出来的位移速度；**不新增任何接口** | ① 改移动脚本加 `public Vector2 PlanarVelocity` → 侵入别人的类；② 用 `InputSystem` 直接读键 → 与实施层脱钩，撞墙/被击退时会播错；③ 自己存上一帧位置求差 → 重复造轮子且 LostFocus 时会炸 |
| 帧序 = `LateUpdate()` | 移动在 `Update()` 里 `Move()`。`Update` 全部跑完才进 `LateUpdate`，所以这里读到的**就是本帧的移动结果**，无 1 帧延迟 | `Update()` + 提高脚本执行顺序 → 需要维护排序，脆弱 |
| 朝向 = `SpriteRenderer.flipX` | 只影响渲染，**不动 Transform**，不会影响 `CharacterController` 的胶囊体与已烘焙的碰撞 | `Transform.localScale.x *= -1` → 会镜像碰撞体与子物体（浮尘也会被镜像）；本项目根节点上已有 `scale.x = -1`，**不能再用这条路**（见 §7.2） |
| 状态用 `int`（4 档硬切分）而非 `Vector2` + BlendTree | 目前只有 4 个方向的独立序列帧，没有过渡帧素材。int 完全图切换时延 0.1s 已能盖住跳变 | `Vector2` + `2D Freeform Directional` Blend Tree → 会给 4 个方向的动画做**插值混合**，序列帧素材混合会糊成重影 |
| 阈值全部 `[SerializeField]` 暴露 | 符合策划自己调参的分工 | 写死常量 → 每次调都要改代码 |
| `flipXThreshold = 0.1` 单独加一道门槛 | 只按 Z 轴走时 `planar.x` 是浮点噪声级抖动，直接判 `x < 0` 会导致**贴图每帧左右闪** | 无门槛 → 必现闪烁 |
| 参数名用 `Animator.StringToHash` 静态缓存 | 避免每帧字符串哈希 | 直接传字符串 → 每帧一次哈希，虽然不大但没必要 |

### 3.3 逐段职责

| 行 | 内容 | 说明 |
| --- | --- | --- |
| 8–10 | `[DisallowMultipleComponent]` + 两个 `[RequireComponent]` | 防止重复挂；两个依赖（`CharacterController` / `Animator`）在玩家根节点上都已存在，所以**不会**往场景里偷偷加组件 |
| 13–16 | 4 个状态码常量 | 与 `Animator` 里 `MoveState` 的取值一一对应，避免魔法数字 |
| 24–28 | `forwardShareThreshold = 0.6` | **取 0.6 的理由**：纯斜向（45°）的 `forwardShare ≈ 0.707 > 0.6`，会判定为「前/后」；只有明显偏横向（约 ±53° 以内）才判「侧向」。这样 8 向输入只有 4 个动画时，斜向不会错误地播侧走 |
| 42–51 | `Awake()` 缓存引用 | `GetComponent` 只在 `Awake` 走一次 |
| 53–85 | 核心状态判定 | 见 §3.2 |
| 91–104 | 朝向翻转 | 提前 return，避免无意义的赋值（`flipX` 是属性，赋值会触发渲染器 dirty） |

---

## 4. Animator 接线（`5TW_AMC.controller`）

### 4.1 参数

| 参数 | 类型 | 默认 | 用途 |
| --- | --- | --- | --- |
| `MoveState` | Int | 0 | transition 条件，唯一被状态机消费的参数 |
| `Speed` | Float | 0 | 由脚本每帧写入，**当前状态机未使用**（预留：将来给走路速度缩放 `Animator.speed`） |

### 4.2 状态

| 状态 | 剪辑 | 关键帧 | 时长(s) | 循环 |
| --- | --- | --- | --- | --- |
| `5TW_Idle` | `5TW_Idle.anim` | 1 | 0.017 | 是 |
| `5TW_Walk` | `5TW_Walk.anim` | 7 | 0.850 | 是 |
| `5TW_ForwardWalk` | `5TW_ForwardWalk.anim` | 5 | 0.683 | 是 |
| `5TW_BehindWalk` | `5TW_BehindWalk.anim` | 5 | 1.350 | 是 |

- 默认状态（Entry）：`5TW_Idle`
- 4 个剪辑均为 `m_PPtrCurves` 驱动 `attribute: m_Sprite`、`path: ""`、`classID: 212`（SpriteRenderer）⇒ **动画打的是「与 Animator 同物体」的那个 SpriteRenderer**，即玩家根节点上那一个。这条链路与 §5 的引用绑定必须一致，见 §7.3。

### 4.3 Transition（完全图，12 条）

全部：`Has Exit Time = false`、`Has Fixed Duration = true`、`Duration = 0.1`、条件仅一条 `MoveState Equals <n>`。

| # | From | To | 条件 |
| --- | --- | --- | --- |
| 1 | Idle | Walk | `MoveState == 1` |
| 2 | Idle | ForwardWalk | `MoveState == 2` |
| 3 | Idle | BehindWalk | `MoveState == 3` |
| 4 | Walk | Idle | `MoveState == 0` |
| 5 | Walk | ForwardWalk | `MoveState == 2` |
| 6 | Walk | BehindWalk | `MoveState == 3` |
| 7 | ForwardWalk | Idle | `MoveState == 0` |
| 8 | ForwardWalk | Walk | `MoveState == 1` |
| 9 | ForwardWalk | BehindWalk | `MoveState == 3` |
| 10 | BehindWalk | Idle | `MoveState == 0` |
| 11 | BehindWalk | Walk | `MoveState == 1` |
| 12 | BehindWalk | ForwardWalk | `MoveState == 2` |

**为什么用完全图而不是 Any State：**

- Any State 切回自身状态时会**先 Exit 再 Re-enter**，序列帧会重头播，表现为「走路时每隔几帧抽搐一下」。
- 完全图里「目标 == 当前」的 transition 不存在，条件变化但目标状态相同时**不触发任何跳转**，序列帧连续。
- Any State 在本控制器中确认为 **0 条**（`m_AnyStateTransitions: []`）。
- 代价：状态数 N 时 transition 数 = N(N−1)。目前 4 个状态 = 12 条。**若将来加到 6 个方向（8 向走），会变成 30 条**，届时建议改成 `Vector2` 参数 + Blend Tree，或改回 Any State + `Can Transition To Self = false`。

### 4.4 Animator 组件上的相关设置（玩家实例）

| 字段 | 值 | 备注 |
| --- | --- | --- |
| `m_Controller` | `5TW_AMC.controller` | guid `2acae2ceac2999741b6ee0202cc5731a` |
| `m_ApplyRootMotion` | 0 | **必须为 0**（位移由 `CharacterController` 负责，开了会双重位移） |
| `m_UpdateMode` | 0 (Normal) | |
| `m_CullingMode` | 0 (Always Animate) | 单角色无所谓；将来角色多了可改 Cull Update Transforms |

---

## 5. 场景接线（`Whitebox1.unity`）

```
whiteboxplayer  [PrefabInstance 1705014472612846426 → Assets/Prefabs/whiteboxplayer.prefab]
├─ Transform                       localScale = (-1, 1, 1)   ← 见 §7.2
├─ [X] MeshFilter                  ← 在场景实例上被移除（白盒方块）
├─ [X] MeshRenderer                ← 在场景实例上被移除
├─ CharacterController             ← 来自 prefab
├─ WhiteboxPlayerMovement          ← 来自 prefab，未改动
├─ PlayerCollisionSmoke            ← 来自 prefab
├─ SpriteRenderer      (新增)      m_Sprite = Assets/Animation/AS_Legs/1.png
│                                  m_FlipX = 0
│                                  Material = URP Sprite-Unlit-Default
├─ Animator            (新增)      Controller = 5TW_AMC.controller
└─ WhiteboxPlayerAnimator (本次新增)
       idleSpeedThreshold    = 0.1
       forwardShareThreshold = 0.6
       flipXThreshold        = 0.1
       spriteRenderer        → 上面那个 SpriteRenderer
```

要点：

1. 桥接组件挂在**玩家根节点**，与 `CharacterController` / `Animator` 同物体 ⇒ `RequireComponent` 与 `GetComponent` 全部命中。
2. `spriteRenderer` 字段是**显式赋值**的（不是靠 `Awake` 兜底），因为动画剪辑的 `path: ""` 指向根节点 —— 两者必须指向同一个渲染器。
3. **组件是加在场景的预制体实例上的（prefab override），不是加在 `whiteboxplayer.prefab` 上**。详见 §7.1。

---

## 6. 验证证据

| 验证项 | 方法 | 结果 |
| --- | --- | --- |
| 脚本编译 | 编辑器 Refresh + 编译错误查询 | 0 error / 0 missing reference |
| 12 条 transition 是否命中 | 在编辑器内**禁用桥接脚本**，用 `Animator.SetInteger("MoveState", n)` + `Animator.Update(0.2f)` 手动推帧，逐次读回 `GetCurrentAnimatorStateInfo(0).shortNameHash` | 6 次切换全部命中目标状态；推帧法是为了绕开 `InputSystem.QueueStateEvent` 会被引擎自动重置、无法长时间驱动的问题 |
| 朝向翻转 | 编辑器内反射调用私有 `UpdateFacing(Vector2)`，输入 6 组向量（左右大值、左右小值、纯前后） | 6/6 符合预期：`|x| ≤ 0.1` 时保持原朝向；`x < -0.1` → `flipX = true`；`x > 0.1` → `flipX = false` |
| 序列帧数据完整性 | 解析 4 个 `.anim`，核对 `attribute: m_Sprite` / `path: ""` / 关键帧数 | 1 / 7 / 5 / 5 帧，与状态预期一致 |
| 端到端手感 | **策划本人 Play 实机操作** | 验收通过（「我操控了一下感觉可以了」） |

---

## 7. 已知问题与风险（请重点评审）

### 7.1 组件加在预制体实例上，不是加在预制体上 ⚠️

`whiteboxplayer` 是 `Assets/Prefabs/whiteboxplayer.prefab` 的实例。`SpriteRenderer` / `Animator` / `WhiteboxPlayerAnimator` 三者都在**场景实例的 `m_AddedComponents`** 里；同时 `MeshFilter` / `MeshRenderer` 在 `m_RemovedComponents` 里。

**后果**：如果后续在别的场景里重新拖一个 `whiteboxplayer.prefab` 出来，**这三个组件不会跟着来**，角色会是个没有贴图和动画的胶囊。

**两个选项（请主程序定）**：

- **A. 保持现状**：白盒阶段只有一个场景，成本最低。等角色定型后一次性把组件与参数搬进 prefab。
- **B. 现在就搬进 prefab**：`whiteboxplayer.prefab` 里加 `SpriteRenderer` + `Animator` + 桥接脚本，删掉 `MeshFilter`/`MeshRenderer`，然后把场景实例上的 override 还原。更干净，但会改动 prefab（策划已验收的当前观感不变，因为 override 会被同样值覆盖）。

> 我倾向 **B**，因为「白盒临时挂在实例上」的债一旦被复制到第二个场景就会被忘掉。但这属于工程结构决策，我不擅自改 prefab。

### 7.2 朝向存在「双重镜像」⚠️

- 玩家根节点 `localScale.x = -1`（同期存在于场景中）
- 脚本又写 `SpriteRenderer.flipX`

两者相乘才是最终朝向。目前表现正确（静止/右移 = 贴图经 scale 镜像后面朝右；左移 = `flipX` 抵消掉镜像，面朝左），**已验证**。

但代价是：**任何一个被改掉，表现就会反**。如果后续有人为了「统一坐标」把 `scale.x` 改回 1，向左走会变成面朝右，而且很难一眼看出是谁的锅。

建议二选一（**不要两个都留**）：

- 保留 `scale.x = -1`，把 `WhiteboxPlayerAnimator.UpdateFacing` 里的判断**反转**（`flipX = planar.x > 0f`）；或
- 把 `scale.x` 改回 `1`，让 `SpriteRenderer.flipX` 独立承担镜像（当前脚本逻辑即可用）。

> 另外提醒：`CharacterController` 挂在带负缩放的根节点上，Unity 官方并不推荐（胶囊体会被镜像）。目前实测无异常，但改回 `scale.x = 1` 同时也顺手消掉这个隐患。

### 7.3 `Speed` 参数目前是死参数

脚本每帧 `SetFloat("Speed", speed)`，但状态机没有任何 transition 或 BlendTree 消费它。

**保留的理由**：将来做「跑起来腿部帧率随时间加快」（`Animator.speed = speed / baseSpeed`）时，参数已经在那儿了。
**风险**：主程序 review 时容易被误判为屎山。若判定不需要，**删掉 `SpeedParam` 与那一行即可**，不影响任何现有功能。

### 7.4 `Awake()` 的 `GetComponentInChildren` 兜底路径实际未生效

因为场景里 `spriteRenderer` 已被显式赋值，`null` 判断永远走不进去。它的价值只在「将来把脚本拖到别的角色上」。但兜底拿到的可能是**子物体**上的渲染器，而动画剪辑的 `path: ""` 指向**根节点**——两者不一致会表现为「动画能播但翻转不生效」。

**建议**：要么删掉这个兜底（强制显式赋值），要么把它限死在 `GetComponent<SpriteRenderer>()`（同物体）。

### 7.5 方向语义待确认（策划已验收，但代码里没写死保证）

脚本把 `+Z`（W 键，远离相机）映射到 `StateBehind`、`−Z`（S 键）映射到 `StateForward`。也就是说，**W 键播的是名字里带 "Behind" 的那个剪辑**。命名上是「角色背对镜头走路」，对俯视视角是对的，但这一层依赖美术资源的命名习惯。

若实机观感相反（W 键看起来像正面对着镜头走），**把脚本 70 行的两行对调**即可：

```csharp
// state = forwardShare > 0f ? StateBehind : StateForward;     ← 当前
// state = forwardShare > 0f ? StateForward : StateBehind;     ← 反过来
```

### 7.6 4 档硬切分没有过渡帧

`Duration = 0.1` 只是让状态机在 0.1s 内交叉淡入淡出，**序列帧素材本身没有过渡帧**，所以对角切换（Idle→侧走）在 0.1s 内是两张不同姿势的贴图互相叠加。

这是白盒阶段的正常状态，不是 bug。正式接入美术时需要：① 补过渡帧，或 ② 把 `Duration` 提到 0.15~0.2，或 ③ 换成方向 Blend Tree。

### 7.7 未处理的边界

| 情况 | 当前行为 | 是否需要处理 |
| --- | --- | --- |
| `Animator` 被 `enabled = false` | 脚本仍每帧 `SetInteger`，无报错、无效果 | 可接受 |
| 角色被击退 / 走斜坡 | `velocity` 含 Y 分量，脚本只取 XZ，正确 | 已处理 |
| 角色被传送（场景内存在 `Vision Portal Pair` 等位移逻辑） | 单帧 `velocity` 会出现尖峰，可能闪一帧错误动画 | 低优先级；若实机可见，可在 `planar.magnitude` 上再加一个上限钳制 |
| Lost Focus / 帧率骤降 | `velocity` 是引擎内部量，不依赖 `Time.deltaTime` 手动累加，稳定 | 已处理 |

---

## 8. 附带交付：环境浮尘（上一条 commit `a531873`，非本次 review 重点）

为「废墟背景」补的悬浮尘埃。做法是从主工程 `Vampire Hunt` 的 `VH_AmbientDust.prefab` 移植参数，**不引入任何脚本**。

| 文件 | 来源 | 说明 |
| --- | --- | --- |
| `Assets/VFX/AmbientDust/Textures/DustMote.png` | 从主工程复制，**guid 原样保留**（`394e93ce...`） | 已确认 loongdum 侧无 guid 冲突 |
| `Assets/VFX/AmbientDust/Materials/M_AmbientDust.mat` | 由本工程 `Assets/VFX/Samples/Dust/DustMaterial.mat` 改出 | Additive：`_Blend=2`、`_SrcBlend=5`、`_DstBlend=1`、`_ZWrite=0`、`renderQueue=3000` |
| `Assets/VFX/AmbientDust/Prefabs/AmbientDust.prefab` | 代码创建 | 参数与主工程逐字段对拍（44 项），仅 2 项为 API 规范化差异、不影响渲染 |

**已对拍出的真实差异并修复**：`simulationSpace` 必须是 `World`（YAML 里 `moveWithTransform: 1` 的语义是 **1=World / 0=Local**，按 API 直觉写成 `Local` 会得到完全不同的观感）。

**残留配置问题（供参考，非本次代码）**：`Max Particles = 1200`，而 `Rate over Time 110 × Max Lifetime 12s = 1320`，约 9% 粒子会被静默丢弃。主工程同样如此，属**照抄继承**的问题。若要修：`Max Particles → 1400`，或 `Rate → 100`。

另外，浮尘实例在场景中的位置/旋转/缩放与 `startSpeed` 等参数在提交后又被策划在 Inspector 里调过一轮，这部分不是我做的，以场景当前值为准。

---

## 9. ⚠️ 同一个 diff 里混有不属于本次任务的改动

`Assets/Scenes/Whitebox1.unity` 目前的未提交 diff 有 **+280 / −30 行**，但**其中只有一小部分是本次动画任务的产出**。为避免 review 时误判，逐项列明：

| 改动 | 归属 |
| --- | --- |
| 玩家实例新增 `WhiteboxPlayerAnimator` 组件 + 绑定 `spriteRenderer` 引用 | **本次（我）** |
| 玩家实例新增 `SpriteRenderer` 组件 | 策划手工（把白盒方块换成精灵） |
| 玩家实例新增 `Animator` 组件并指定 `5TW_AMC.controller` | 策划手工 |
| 玩家实例移除 `MeshFilter` / `MeshRenderer` | 策划手工 |
| 玩家实例 `localScale = (-1, 1, 1)` | 策划手工（见 §7.2） |
| 玩家实例改名 `1_1` → `whiteboxplayer` | 策划手工 |
| `AmbientDust` 实例的 Transform（scale 3 / rot −90）与粒子参数（shape scale、startSpeed 等） | 策划手工调参（见 §8） |
| `Assets/Animation/AS_Legs/shadow.mat`（未跟踪） | **非本次改动**，来源待确认 |

> 也就是说：**`Assets/Scripts/` 下的新增脚本、`5TW_AMC.controller` 的全部改动、以及场景里「桥接组件 + 它的 4 个字段」，这三处才是我做的。**
> 建议提 review 时用 `git diff -- Assets/Scripts Assets/Animation/AS_Legs/5TW_AMC.controller` 先把范围框出来。

---

## 10. 我没做的事（主动交代边界）

| 没做 | 理由 / 何时需要补 |
| --- | --- |
| 没建 asmdef | 本工程 `Assets/` 下 14 个 `.cs` **全部**落在 `Assembly-CSharp`（0 个 asmdef），单独给一个白盒脚本建 asmdef 反而制造不一致。等脚本上规模时统一分层 |
| 没写单元测试 | 依赖 `CharacterController` 与 `Animator` 两个引擎组件，EditMode 测试要搭 mock，收益低于成本。当前验证方式是编辑器内确定性推帧（§6） |
| 没做事件 / ScriptableObject 解耦 | 只有「移动 → 表现」一条线，引入 SO 事件通道属于过度设计。出现第二个消费者（音效、脚步 VFX）时再抽 |
| 没动 `WhiteboxPlayerMovement.cs` | 明确的约束（§2） |
| 没改 `whiteboxplayer.prefab` | 属于工程结构决策，见 §7.1，等主程序定 |
| 没调任何数值到「最优」 | 按分工，`idleSpeedThreshold` / `forwardShareThreshold` / `flipXThreshold` / transition `Duration` 全部留给策划在 Inspector 调 |

---

## 11. 回退方法

**只回退动画接线、保留浮尘**（推荐，因为浮尘已单独提交）：

```bash
cd "D:/unity/Vampire Hunt/loongdumGamejam"
git checkout -- Assets/Animation/AS_Legs/5TW_AMC.controller
# 场景文件混有其他改动，不要整体 checkout，用编辑器手动删掉
# whiteboxplayer 上的 WhiteboxPlayerAnimator 组件即可
# 脚本文件保持存在不会报错（没有别的地方引用它）
```

**只回退「左移翻转」、保留动画播放**：删掉 `LateUpdate` 里最后一行 `UpdateFacing(planar);` 即可，其余逻辑不受影响。

**只回退某个阈值**：Inspector 里改回去，无需动代码。
