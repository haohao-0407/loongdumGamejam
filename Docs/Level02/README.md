# 第二关 · 够不着的拉杆

依据 `level-diagram-source.html` 中的第三个页签（第二关），新增可玩的 Unity 场景。11 × 11 地图逐格保留；第一关和对照图不在本次实现范围内。

## 打开和游玩

使用仓库指定的 **Unity 6000.6.4f1** 打开项目，然后双击：

`Assets/Scenes/Level02_UnreachableLever.unity`

也可以选择菜单 `Tools > Loongdum > Level 02 > Open Scene`，然后点击 Play。关卡已追加到 Build Settings；原有场景顺序保持不变，目前以独立场景进入，未添加关卡选择或第一关结束后的自动跳转。

| 操作 | 按键 |
| --- | --- |
| 四方向、一格一步 | WASD / 方向键；可按住连续移动 |
| 拨动身边的拉杆 | E |
| 从开局重来 | R |
| 设计者全图 / 玩家视野 | F1，仅编辑器和 Development Build |

Scene 编辑视图保留完整地图；进入 Play 自动切回玩家视野。下半身碰到固定的上半身即过关。为避免绕过 X／Y 门，镜面底座和拉杆底座均不可穿行。

## 机关规则

- 上半身固定在 `(行5, 列5)`，沿八个方向照亮 3 格。墙、关闭的门会截断光，阻挡物自身可见。
- 下半身从 `(行9, 列1)` 出发，额外感知上下左右各一格。光照与感知范围以外的地板、墙、机关和文字均不显示；离开后没有探索记忆残留。
- 玻璃 G 挡移动、透光。拉杆 A 必须处于角色周围八格以内才可操作；上半身的位置无法够到它。
- 开局 Y 开、X 关。拨动 A 后 X 开、Y 关，再拨可反转。若角色站在即将关闭的门格中，需先离开门框。
- 压力板 1 仅在下半身站在上面时开启光闸 a；离开即关。
- 光穿过 p，从 P 沿原方向射出，并重新获得 3 格光程，最多连续传递 3 次。它只传递视线。
- 镜面底座与拉杆底座按实体障碍实现，不能穿行。原图没有明确说明这两种格子的通行性；此处根据其“唯一解”及绕过 X 的设计意图补足，否则能直接穿过 P 或 A 抄近路。

坐标采用 HTML 地图数组的 **零基行列**，不依赖原文中个别东、西方向描述。地图原样保存在 `Assets/Levels/Level02/Level02Layout.json`。

## 设计路线与计步

从起点向北 8 步、向东 4 步，到达两扇门之间；按 E。再向东 2 步、向南 2 步踩住压力板，观察 P 下方的短暂照明。向西 4 步、向南 2 步、向东 2 步，与上半身重合。

合计 **24 次移动 + 1 次拉杆操作**，包含起点共经过 25 个路径格。压力板用于提供路线线索；玩家记住路线后不必等待或按额外交互键。照明不会强制角色沿某条路行走。

## 与现有项目的关系

场景使用现有 URP 管线和漫画渲染效果，并复用仓库的 `Glass.mat`。新关卡使用自己的网格状态和逐格显示规则，因为现有 `VisionSource` / `VisionPortal` 使用连续空间、保留剩余视野距离，与这张图中的八方向光线及出口刷新光程不同。现有移动、视野、渲染脚本和原场景不需要改动。

场景内每个格子都有可编辑的实体，机关碰撞跟随状态切换；当前角色移动由网格规则决定。`LevelTwoModel` 管理规则，`LevelTwoController` 管理输入和显示。棋盘为白盒几何，尚未替换为正式角色或环境美术。

## 重建与验证

- `Tools > Loongdum > Level 02 > Rebuild Scene`：从地图重新生成场景和专用材质，会覆盖该第二关场景的手工摆放。
- `Tools > Loongdum > Level 02 > Validate Rules and Scene`：运行地图一致性、机关、光程、隐藏对象、碰撞、重置及穷举路径检查。
- 编辑器批处理入口 `Loongdum.Levels.Editor.LevelTwoSceneBuilder.BuildAndValidate`：生成并验证。
- 编辑器批处理入口 `Loongdum.Levels.Editor.LevelTwoPlayValidation.Run`：通过 Input System 注入键盘事件完成关卡、重置并截图；此入口运行后退出编辑器，仅用于独立批处理验证。

规则和场景验证结果写入 `Logs/Level02-Validation.txt`；运行验证写入 `Logs/Level02-PlayValidation.txt`，截图位于 `Captures/Level02/`。日志目录不参与版本管理。

截图由 Play 模式中的真实摄像机离屏渲染，不含 IMGUI 标题与提示栏。批处理时输入设置使用临时副本，结束后恢复。当前 Unity 编辑器启动搜索索引可能报 `UnityEditor.Search.SearchDatabase` 的 `ArgumentOutOfRangeException`；验证会单独记录此编辑器问题，其余错误仍会使运行验证失败。

### 本次验证结果（2026-10-05）

- Unity 6000.6.4f1 编译和 40 项规则／场景检查通过，见 [Validation-results.txt](Validation-results.txt)。
- Play 模式通过 WASD / 方向键 / E 实际输入通关，24 次移动；R 重置和 F1 设计视图切换通过，见 [Play-validation-results.txt](Play-validation-results.txt)。
- 已查看开局、踩板、离板和设计者全图截图，确认临时照明的出现和消失。IMGUI 提示栏未包含在离屏截图中，仍需人工查看 Game 视图排版。
- Git LFS 完整性检查通过；仓库基础提交为 `09ddfb31c94c8b8bf0d9be91cbad5e7533302b86`。

![踩住压力板时的路线照明](../../Captures/Level02/02-plate-held.png)
