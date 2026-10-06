# 拉杆开关门（LeverDoorSwitch）

玩家进入拉杆范围后，按 Input System 中配置的 `Player/Interact` 动作即可开门，再按一次关门。当前 `Assets/InputSystem_Actions.inputactions` 的绑定为键盘 **E** 和手柄 **buttonNorth**；修改绑定或应用运行时重绑定后，拉杆会读取更新后的动作。

拉杆在按钮按下时立即切换一次，按住不会连续切换；当前 Interact 动作的 `Hold` 设置不要求拉杆长按。

## 配置

1. 在拉杆根物体上挂 `Assets/Scripts/LeverDoorSwitch.cs`。
2. 在 `Doors` 数组中拖入目标门的 **Animator**。可使用 `Assets/Prefabs/Door.prefab`；Animator 必须包含 `Door Parameter` 指定的 bool 参数，默认是 `DoorOpen`。
3. 在 `Player` 中拖入玩家的 **CharacterController**；留空时自动查找场景中的 `WhiteboxPlayerMovement`。其他玩家控制器需要手动指定该引用。
4. 设置 `Reach Distance`，默认 **2.6 米**。`Lever` 留空时以挂脚本的物体为范围中心，选中物体可看到黄色范围球。无需额外的 Trigger Collider。
5. `Interact Action` 可以留空，使用项目全局 Input Actions 的 `Player/Interact`；也可以选择指定的动作。若玩家带有 `PlayerInput`，脚本使用该玩家的动作副本，并跟随其启用状态和动作图切换。
6. 保持 `One Shot` **关闭**即可反复开关。旧组件如果已经保存了 `One Shot = true`，需要在 Inspector 中取消勾选。

门上的 `DoorAnimatorToggle` **可以保留**：它现在只负责门动画，提供 `IsOpen`、`SetOpen(bool)` 和 `Toggle()`，不再监听全局 E 键。无需修改 Door prefab 或 Animator Controller。

## 可选字段

| 字段 | 说明 |
|---|---|
| `Handle` | 拉杆手柄，开门时从摆放时的旋转角度转动，关门时恢复原角度 |
| `Thrown Euler` | 手柄转动量，默认 `(0, 0, -35)` |
| `Handle Renderer` / `Active Material` | 开门时换为激活材质，关门时恢复原材质 |
| `Barriers` | 独立挡路碰撞块，开门时禁用，关门时启用；门板碰撞随动画正常移动时可留空 |
| `One Shot` | 默认关闭；开启后玩家只允许拨动一次 |
| `Require Grounded` | 默认关闭；开启后玩家必须落地才能交互 |
| `Require Line Of Sight` | 默认关闭；开启后玩家与拉杆之间不能有实体遮挡 |

初始开关状态取第一扇门的 `DoorOpen` 值，并同步到其他门、碰撞块和手柄。一个拉杆可以控制多扇门。

## 验证与脚本调用

- 范围外按 Interact：门保持原状态。
- 进入范围后按 Interact：门打开；松开后再按：门关闭。
- 按住默认的 Interact 按钮：只拨动一次。
- 离开范围后按 Interact：门保持原状态。
- 将 Interact 改绑到其他键或使用手柄：交互和屏幕提示跟随动作绑定。

其他脚本可调用 `TryInteract()`，该方法会检查玩家距离和交互限制并返回是否成功；`CanInteract` 可用于自定义交互提示。需要直接控制状态时调用 `SetThrown(true/false)`，该方法不检查玩家距离和 `One Shot`。单独控制门时调用 `DoorAnimatorToggle.SetOpen(true/false)` 或 `Toggle()`。

如果仍使用 `Level03Flow` / `Level04Flow` 等关卡脚本控制同一拉杆，接入此组件时应只保留一套交互入口，避免两个脚本同时处理按键。
