# 第二关 · 够不着的拉杆

当前场景和运行时采用 `level02-integration` 分支的实现。使用 Unity 6000.6.4f1 打开 `Assets/Scenes/Level02_UnreachableLever.unity`，进入 Play；WASD 使用第一关的连续移动，E 操作拉杆，R 重置，F1 切换设计者视图。

这一版本增加了拉杆 B 和门 Z，并让拉杆 A 切换橙色镜面与远端墙的位置。下方旧白盒版本的地图、24 步解法、截图和验证报告属于历史记录，不能用于验收当前版本。

旧场景生成器和验证器依赖旧运行时接口，保留源码但默认停用，由 `LOONGDUM_LEVEL02_LEGACY_TOOLS` 编译条件保护。只有恢复匹配的旧运行时和场景后才可启用；当前直接编辑已合入的场景。

## 历史：旧白盒版本

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

- 上半身固定在 `(行5, 列5)`，使用第一关的连续圆形视野，半径为 **10 个世界单位**。墙和关闭的门遮挡视线，玻璃透视线。
- 下半身从 `(行9, 列1)` 出发。画面由上半身的连续视野遮罩显示，遮罩外不显示地图；离开后没有探索记忆残留。
- 玻璃 G 挡移动、透光。拉杆 A 必须处于角色周围八格以内才可操作；上半身的位置无法够到它。
- 开局 Y 开、X 关。拨动 A 后 X 开、Y 关，再拨可反转。若角色站在即将关闭的门格中，需先离开门框。
- 压力板 1 仅在下半身站在上面时开启光闸 a；离开即关。
- 视线穿过 p，从 P 射出并保留剩余光程。镜面只传递视线，不传送角色。
- 镜面底座与拉杆底座按实体障碍实现，不能穿行。原图没有明确说明这两种格子的通行性；此处根据其“唯一解”及绕过 X 的设计意图补足，否则能直接穿过 P 或 A 抄近路。

坐标采用 HTML 地图数组的 **零基行列**，不依赖原文中个别东、西方向描述。地图原样保存在 `Assets/Levels/Level02/Level02Layout.json`。

## 设计路线与计步

从起点向北 8 步、向东 4 步，到达两扇门之间；按 E。再向东 2 步、向南 2 步踩住压力板，观察 P 下方的短暂照明。向西 4 步、向南 2 步、向东 2 步，与上半身重合。

合计 **24 次移动 + 1 次拉杆操作**，包含起点共经过 25 个路径格。压力板用于提供路线线索；玩家记住路线后不必等待或按额外交互键。照明不会强制角色沿某条路行走。

## 与现有项目的关系

场景使用现有 URP 管线和漫画渲染效果，并复用仓库的 `Glass.mat`。第二关仍以网格规则处理移动和机关；画面视野使用第一关的 `VisionSource` / `VisionPortal`，固定在上半身位置，半径 10 个世界单位。现有第一关和共用移动、视野、渲染脚本未修改。

场景内每个格子都有可编辑的实体，机关碰撞跟随状态切换；当前角色移动由网格规则决定。`LevelTwoModel` 管理规则，`LevelTwoController` 管理输入和显示。棋盘为白盒几何，尚未替换为正式角色或环境美术。

## 重建与验证

- `Tools > Loongdum > Level 02 > Rebuild Scene`：从地图重新生成场景和专用材质，会覆盖该第二关场景的手工摆放。
- `Tools > Loongdum > Level 02 > Validate Rules and Scene`：运行地图一致性、机关、视野源半径、碰撞、重置及穷举路径检查。
- 编辑器批处理入口 `Loongdum.Levels.Editor.LevelTwoSceneBuilder.BuildAndValidate`：生成并验证。
- 编辑器批处理入口 `Loongdum.Levels.Editor.LevelTwoPlayValidation.Run`：通过 Input System 注入键盘事件完成关卡、重置并截图；此入口运行后退出编辑器，仅用于独立批处理验证。

规则和场景验证结果写入 `Logs/Level02-Validation.txt`；运行验证写入 `Logs/Level02-PlayValidation.txt`，截图位于 `Captures/Level02/`。日志目录不参与版本管理。

截图由 Play 模式中的真实摄像机离屏渲染，不含 IMGUI 标题与提示栏。批处理时输入设置使用临时副本，结束后恢复。当前 Unity 编辑器启动搜索索引可能报 `UnityEditor.Search.SearchDatabase` 的 `ArgumentOutOfRangeException`；验证会单独记录此编辑器问题，其余错误仍会使运行验证失败。

### 本次验证结果（2026-10-06）

- Unity 6000.6.4f1 编译和 42 项规则／场景检查通过，见 [Validation-results.txt](Validation-results.txt)。
- Play 模式通过 WASD / 方向键 / E 实际输入通关，24 次移动；R 重置和 F1 设计视图切换通过，见 [Play-validation-results.txt](Play-validation-results.txt)。
- 已查看开局、踩板和设计者全图截图，确认连续视野遮罩与镜面机关可见性。IMGUI 提示栏未包含在离屏截图中，仍需人工查看 Game 视图排版。
- Git LFS 完整性检查通过；仓库基础提交为 `09ddfb31c94c8b8bf0d9be91cbad5e7533302b86`。

![踩住压力板时的路线照明](../../Captures/Level02/02-plate-held.png)
