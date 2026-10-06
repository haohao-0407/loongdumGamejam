# Maze 视野玩法（头 / 脚 / 按压板）

用 `Assets/Levels/Maze/Editor/MazeVisionSetup.cs` 里的编辑器菜单一键搭出来，不手改场景文件。

## 菜单

- **Tools ▸ Maze ▸ 搭建视野玩法（头 / 脚 / 按压板）**
- **Tools ▸ Maze ▸ 清除视野玩法**

必须先打开 `Assets/Scenes/Maze.unity`，否则脚本会弹窗拒绝执行。重复点「搭建」会先删掉上一次生成的 `VisionGameplay` 根节点再重建。

## 它做了什么

全部产物都挂在新建的 `VisionGameplay` 根节点下：

| 对象 | 内容 | 位置 |
|---|---|---|
| 头（视野源） | `vision source.prefab` 实例，`VisionSource` 组件，视野半径 10 米 | 入口高台 `EntranceHighWall` 台面 +1 米 |
| 脚（玩家） | `whiteboxplayer.prefab` 实例 + `FullBodyReversal`（Upper Body 指向头） | 迷宫南侧入口缺口内 1 米，离地 0.6 米靠重力落地 |
| 按压板（反转格） | 一块橙色板面 + `BoxCollider`（2×2 米触发区）+ `ReversalTile` | 迷宫墙体范围的几何中心 |

另外脚本会把 `WallH_*` / `WallV_*` / `PlatformWall_*` / `Entrance*` / `PlatformBase` 打上 **VisionObstacle** 标签，这样墙能挡住视线（`VisionSource` 的遮挡过滤就是按这个标签找的，和 Whitebox1 一致）。

## 围墙高度（俯视全图 / 平视挡视线）

点「搭建」时脚本会顺带把所有围墙（`PlatformWall_*` 外墙 + `WallH_*` / `WallV_*` 迷宫墙）抬到 **1.8 米**，底面不动、只拉高。这个数字是算出来的，不是随手给的：

| 视线高度 | 数值 | 与 1.8 米墙（顶面 1.49 米）的关系 |
|---|---|---|
| 头站在入口高台上 | `EntranceHighWall` 顶 1.29 + `HeadAboveDeck` 1.0 = **2.29 米** | 高于墙顶 0.8 米 → 视线越过围墙，俯视全图 |
| 换位后头落到地面 | 玩家胶囊中心 **0.69 米** | 低于墙顶 0.8 米 → 视线被围墙截住，看不到墙外 |

原来的墙只有 0.891 米高（顶面 0.58 米），比换位后的视线 0.69 米还低，所以能直接看到围墙外面——这就是要抬高的原因。

想单独调整围墙，用菜单 **Tools ▸ Maze ▸ 抬高围墙（俯视可见 / 平视挡视线）**，改高度就改脚本顶部的常量：

| 常量 | 默认 | 说明 |
|---|---|---|
| `WallHeight` | 1.8 | 围墙的世界高度（米），底面不动 |
| `PerimeterOnly` | false | 改成 true 就只抬最外圈 `PlatformWall_*`，迷宫内部墙保持原样 |

这个操作是**幂等**的：按绝对高度算，重复执行不会越抬越高。想恢复原状，把 `WallHeight` 改成 `0.891` 再执行一次即可（就是原来的高度）。

## 为什么这样就能全黑

「只有视野内可见、其余全黑」不是这个脚本实现的，而是项目里本来就有的两条链路：

1. `Assets/Settings/PC_Renderer.asset` 上的 **Vision Range Mask** 渲染特性（URP 的 FullScreenPass，材质是 `Assets/Materials/VisionRangeMask.mat`，shader 是 `Assets/Shaders/VisionRangeMask.shader`）。
2. `Assets/Scripts/VisionSource.cs` 每帧往 shader 里写 `_VisionMaskEnabled`、`_VisionSourcePositionRadius`、遮挡距离纹理等全局量。

所以只要场景里存在一个**启用的** `VisionSource`，全屏遮罩就会打开，视野外（包括天空）会被乘成黑色。删掉或禁用「头」这个对象，画面就恢复正常。

同样一套机制在 `Level03_LightGates.unity` 和 `Whitebox1.unity` 里已经在用，那两个场景的主相机 Clear Flags 都设成了 `Nothing`，配合遮罩效果最干净。Maze 的主相机目前是 `Skybox`，遮罩开启时天空同样会被压黑，不改也可以。

## 验收步骤

1. 打开 Maze 场景，点菜单搭建，`Ctrl+S`。
2. 选中 `Main Camera`，Rotation 设成 `45 / 180 / 0`（或者用之前算好的机位 `1.93 / 29.52 / 31.33`）。
3. 按 Play：
   - 画面应该只剩「头」周围一圈可见，其余全黑；
   - 脚应该落在迷宫入口缺口内的地面上；
   - 走到迷宫中心踩上橙色按压板，画面左下会提示可以按 F；
   - 按 F，脚和头互换位置（`FullBodyReversal` 是 X/Y/Z 三轴互换）。

## 头脚相撞 → 游戏结束

`Assets/Scripts/HeadFeetGameOver.cs`，搭建脚本会自动挂到「脚」（玩家）上，并把 `Head` 指向「头」（视野源）。

判定逻辑：每帧比较头和脚的**距离**（三维距离，从玩家物体位置到头部物体位置），小于 `Contact Distance`（默认 1 米）就判负——立刻停掉移动和两种反转术式，画面中央显示红色的「游戏结束」，Console 里打印双方坐标。

换位保护：头的物体在一帧里位移超过 `Swap Jump Threshold`（默认 0.5 米）就认为刚换过位，接下来 `Swap Grace`（默认 0.5 秒）内不判定碰撞，避免换位瞬间误杀。三轴换位本身只是双方交换坐标、距离不变，正常情况下也不会触发。

参数都可以在 Inspector 里调：

| 字段 | 默认 | 说明 |
|---|---|---|
| `Head` | 头（视野源） | 判定用的另一方 |
| `Contact Distance` | 1.0 | 多近算撞上（米） |
| `Swap Jump Threshold` | 0.5 | 头部瞬移多少米算一次换位 |
| `Swap Grace` | 0.5 | 换位后多少秒内不判定 |

注意：「头」本身是没有模型的（`vision source.prefab` 上只有 `VisionSource` 脚本），所以玩家看不见它。如果觉得撞死得莫名其妙，把 `MazeVisionSetup.cs` 顶部的 `ShowHeadMarker` 改成 `true` 再重新搭建，会给头加一个显眼的青色小球。

## 可调参数

脚本顶部的常量：

| 常量 | 默认 | 说明 |
|---|---|---|
| `VisionRadius` | 10 | 视野半径（米） |
| `HeadAboveDeck` | 1.0 | 头高出高台台面多少米 |
| `PlayerDrop` | 0.6 | 玩家生成时离地高度，靠重力落地 |
| `PadHalfSize` | 1.0 | 按压板触发范围半边长，2.0 即 2×2 米 |

生成之后也可以直接在 Inspector 里改：`VisionSource` 的 `Vision Radius`（更直观）、`Edge Softness`（边缘柔和度）、`Keep Obstacle Visible`（墙本身要不要保持可见）；`FullBodyReversal` 的 `Activation Key` 和 `Snap Lower Body To Ground`。

## 注意

- 脚本用 `Undo.RegisterCreatedObjectUndo` 注册了撤销，误操作可以 `Ctrl+Z`。
- 按压板放在迷宫墙体的几何中心。如果那里恰好被墙占住，在 Scene 视图里把它挪到最近的空地即可（`ReversalTile` 判定只认它自己的 `BoxCollider`）。
- 头在台面上方 1 米，而迷宫墙只有 0.89 米高，所以水平方向的视线射线是从墙上方扫过去的——墙能挡视线的前提是视线高度低于墙顶。想让墙真正挡视线，把 `HeadAboveDeck` 调小（比如 0.2），或者把墙加高。
