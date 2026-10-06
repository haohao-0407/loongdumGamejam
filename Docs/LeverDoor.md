# 独立拉杆门（LeverDoorSwitch）

组件：[Assets/Scripts/LeverDoorSwitch.cs](C:/GameJam/loongdumGamejam/Assets/Scripts/LeverDoorSwitch.cs)

**门用的还是项目原来那套**：`Assets/Prefabs/Door.prefab` + `Assets/Animation/Door.controller`（Animator 布尔参数 `DoorOpen`）。脚本只负责「靠近拉杆按 F → 把门打开」，逻辑对齐 `Level03Flow` / `Level04Flow` 里的拉杆（原版是 2.6 米内按 E）。

拉杆和门都可以随便摆，互相之间没有位置要求，只要把引用连上。

## 摆放步骤

### 1. 放门

1. Project 里把 `Assets/Prefabs/Door.prefab` 拖进场景，摆到要挡路的位置。门宽不够就调它的 `localScale`（`Level04Builder` 里门宽 = 格子大小，用 `localScale.x = 格子边长`）。
2. **删掉门上的 `Door Animator Toggle` 组件**。留着的话按 E 会直接开关这扇门，和拉杆重复。脚本发现没删会在 Console 里警告。
3. 记住门根物体上的 **Animator** —— 接线要用它（不是 GameObject）。

### 2. 放挡路碰撞块（可选，但推荐）

`Level03/04` 里门板自己的碰撞是关掉的，另外放一块 Cube（它们叫 `Closed X`）堵住门洞，开门时禁用这块。你只要在门洞位置放个 Cube、调好大小、`MeshRenderer` 可关可不关，把它的 `BoxCollider` 拖进脚本的 `Barriers` 就行。

### 3. 放拉杆

拉杆就是两个 Cube，和 `Level03/04` 里一模一样：

```
Lever A                       ← 空物体，脚本挂这里
├─ Base    Cube   Scale (0.7, 0.5, 0.7)    Local Position (0, 0.25, 0)
└─ Handle  Cube   Scale (0.12, 0.8, 0.12)  Local Position (0, 0.7, 0)
```

- 材质：`Base` 用 `Assets/Levels/Level02/Materials/Lever.mat`，`Handle` 用 `Assets/Materials/VisionPortalEntry.mat`
- 想省事：打开 `Assets/Scenes/Level03_LightGates.unity` 或 `Level04_WindowReturn.unity`，Hierarchy 里选中 `Lever A` 整个 `Ctrl+C`，回到你的场景 `Ctrl+V`，再挪位置 —— 这就是"用现成的拉杆"。

### 4. 挂脚本接线

选中 `Lever A` → Add Component → **Lever Door Switch**：

| 字段 | 拖什么 |
|---|---|
| `Lever` | 留空 = 用自己；也可以拖 `Lever A` |
| `Reach Distance` | 玩家站多近能拨动，默认 2.6 米 |
| `Activation Key` | 默认 **F** |
| `Handle` | `Handle` 子物体 |
| `Handle Renderer` | `Handle` 的 MeshRenderer |
| `Active Material` | 拨动后手柄换成的材质，比如 `Assets/Materials/VisionPortalExit.mat` |
| `Doors` | 门的 **Animator**，Size 填门数 |
| `Door Parameter` | 保持 `DoorOpen` |
| `Barriers` | 第 2 步那块碰撞块，可空 |
| `Player` | 留空会自动找场景里的白盒玩家 |

### 5. 测试

Play → 走到拉杆旁，屏幕下方出现「F 拨动拉杆开门」→ 按 **F**：手柄转 -35° 并换成激活材质，门的 `DoorOpen` 置 true，挡路块被禁用，门打开。

## 参数

| 字段 | 默认 | 说明 |
|---|---|---|
| `Reach Distance` | 2.6 | 触发距离（米），和 `Level03/04` 一致 |
| `Activation Key` | F | 拨动按键 |
| `Thrown Euler` | (0, 0, -35) | 拨动后手柄旋转量，和 `Level03/04` 一致 |
| `One Shot` | true | 只能拨一次；关掉就可以反复开关门 |
| `Require Grounded` | true | 玩家落地才能拨动 |
| `Require Line Of Sight` | false | 打开后，玩家和拉杆之间被实体挡住就不允许操作（`Level03/04` 里是开着的） |

也可以从别的脚本直接调 `SetThrown(true)` 开门。

## 注意

- 门必须是 `Door.prefab`（或任何带 `DoorOpen` 布尔的 Animator）；换成别的门模型就得自己保证 Animator 上有个叫 `DoorOpen` 的 bool 参数。
- 一个拉杆可以同时开多扇门（`Doors` 数组填几就开几扇），`Level04` 里拉杆 A 就是同时开 D 和 X。
- 如果 Play 时提示"机关被实体隔开"，把 `Require Line Of Sight` 关掉，或者检查玩家和拉杆之间是不是夹着墙/门板。
