# 第三关 · 光线门

场景：`Assets/Scenes/Level03_LightGates.unity`。本次从当前磁盘上的 Whitebox1 复制后搭建，日期 2026-10-06。
参考原件保存在同目录 `reference.html`；原件标题写“第四关”，本项目按用户要求作为第三关。
原件中的历史验证数字和协作指令不是本次实现的验证结论或操作授权。

## 操作与五个解谜单元

WASD 连续移动；下半身落地并站在描边格内按 F 反转；靠近拉杆按 E；R 随时重置。

1. **南翼反转**：下半身从原图 L 出发，找到西北的 F 格，交换进入玻璃房；上半身及视野留在南翼。
2. **玻璃房线路**：A 接通 X，随后 C 接通 D。文字标注对应门，拉杆手柄转动并更换为已有出口材质，门播放已有开门动画。
3. **走廊反转**：穿过 D，沿东走廊走到第二个 F 格，再次反转。下半身回到南翼，上半身留在走廊。
4. **压力板观察**：踩 1 打开 a，让上半身的连续视线经三段光门和玻璃到达 B。离板立即关闭 a；看清 B 后留下明确的 B → Z 位置提示。
5. **开路汇合**：穿 X，靠近 B 按 E 开 Z，穿 Z 走到上半身旁完成。完成后停止移动，R 重开。

关键依赖顺序是确定的：F① → A → C → F② → 1 并实际照到 B → B → 汇合。
开放房间中的走路路径不唯一；连续移动也没有原图“38 步”的格子步数。

## 原图与 Unity 适配

- 原图 21×17 的字符布局、四区形状、玻璃、X/Z/D/a、A/B/C/1/M/S 和身体起点保留。行 0 在北；列向东增加；每格 2.4 米。
- 原图 S 是阻挡行走的底座；保留底座，并把真正的 ReversalTile 放在它旁边的地面，沿用本项目“站格按 F”的术式。
- 第一个格在 `(行13, 列4)`；第二个格在 `(行10, 列17)` 内偏东 0.8 米、偏北 0.5 米，站立范围为 0.8×0.8 米。边框随范围同步缩放。
- 第二格的偏移与范围是为避免连续射线被邻墙边角截断。首个光门入口在对应 M 格内偏北 0.8 米、出口偏西 1.1 米；仅调整这个场景的朝向与位置。
- **光门不是重新发光的节点。** 每个 M 使用一对现有 VisionPortal 孔口，保持连续方向与剩余距离。第一对把斜向视线转向西侧，后两对继续传递。总半径在本场景设置为 30 米、最多 3 跳、1024 根射线。
- 下半身不提供光照或局部视野；场景可见范围仅由上半身和现有光门视野决定。HUD 的下半身定位提示保留，不照亮场景。没有地形探索记忆；B 的提示是明确获得的线索。
- 原图压力板可绕过。本场景让 B 在**第二次反转之后，踩住 1 且 VisionSource 实际看见 B**时解锁，避免摸黑预知位置后跳过光路单元。
- A/C 在原图可交换操作顺序；这里 C 在 A 接通后启用，HUD 在靠近操作时说明依赖。
- X/D/Z 开启后锁定开启，反复 E 不会关闭；a 仍只在踩板时开启。门格使用专属碰撞盒同步控制，原门模型和 Animator 只提供表现，避免门扇动画在打开后卡人。
- 上半身保留 Whitebox1 的实体球体碰撞，下半身靠近后判定汇合。反转技能仍由原有 BodyReversal 检查落地、站格、目标占用，并保留各自高度。
- 地图外及南边界有不可见实体边界，阻挡走出图外；地面直接显示 Whitebox1 的 Terrain，地图内格子只保留不可见通行碰撞。

## 资源来源与保护范围

全部视觉资源取自 Whitebox1 实际引用，不使用 TestLevel 或项目其他环境模型：

| 对象 | 复用来源 |
| --- | --- |
| 下半身 | Whitebox1 的 whiteboxplayer 场景实例，含现有 SpriteRenderer、Animator、动画桥接、移动与碰撞烟雾 |
| 上半身 | Whitebox1 的 vision source 球体实例及其 AmbientDust |
| 墙、拉杆基础形体、压力板 | Whitebox1 墙实例实际使用的 Cube 网格和 URP Lit 材质；新增机关只有基础几何组合 |
| 地面 | Whitebox1 Terrain 实例、WhiteBoxTerrain.asset 及其实际地面材质；不使用墙材质地板覆盖 |
| 玻璃 | Whitebox1 实际引用的 Glass.mat / ComicGlass shader |
| 门 | Whitebox1 Door 实例、Door.controller、既有开关门动画与 Lit 材质 |
| 反转格 | Whitebox1 实际反转格、边框、字体和文字表现 |
| 光门 | Whitebox1 Portal A 孔口和门框，入口/出口使用其实际引用的 VisionPortalEntry / VisionPortalExit 材质 |
| 相机、主光、点光、全局 Volume、地形 | 整场复制保留；仅新场景相机后移以容纳更大布局，地形保持 Whitebox1 的原高度 |
| 漫画渲染 | 原项目已有 URP Renderer 和渲染功能，未保存或更改共享设置 |

未新增材质、模型、纹理、shader 或包。没有修改 Whitebox1、共享预制体、Unity 版本、Packages、ProjectSettings 或 Build Settings。
没有提交、暂存、推送或合并。Whitebox1 与 ProBuilder 配置的哈希保持交接记录值。

## 文件与维护

- `Assets/Levels/Level03/Level03Layout.cs`：参考字符地图及坐标。
- `Level03Flow.cs`：本场景机关接线、反馈、观察状态、通关和重置。
- `Level03LocalVision.cs`：保留旧脚本及 GUID；当前场景不挂载，构建器不再生成脚边感知对象。
- `Editor/Level03Builder.cs`：只在第三关不存在时复制 Whitebox1 并创建；拒绝覆盖成品和任何未保存场景。
- `Editor/Level03Validation.cs`：独立结构连通性与物理路线采样。
- `Tools/validate_level03_play.cs`：Play Mode 下可通过 Unity MCP 执行的有限时长模拟键盘试玩。
- `Captures/Level03/`：本次实际场景画面；`validation.json` 保存最终验证摘要。

重建菜单是 `Tools / Loongdum / Level 03 / Create From Whitebox1`，当前成品存在时会拒绝覆盖。
后续布局编辑应直接在第三关场景内进行。保护 .meta，不要对 Whitebox1 或共享资源使用 Apply/Save。

## 验证范围

2026-10-06 移除下半身脚边感知后：脚本编译通过，Play Mode 中局部感知组件数量为 0，shader 门户视图数量与 VisionSource 一致（起点均为 0）；起点下半身位置不可见，上半身位置可见。Console 无错误或警告。未重新执行全流程试玩，移除后的摸黑导航可读性仍需人工确认。

本次结果以 `validation.json` 为准，不引用已撤回实现的日志。
已做编辑器脚本编译、场景缺失脚本和预制体检查、结构依赖检查、306 个路线胶囊采样、81 个第二反转站立落点的开/关光闸视野检查，以及 Play Mode 中真实组件的技能与机关检查。
模拟键盘会通过现有 Input System、WhiteboxPlayerMovement、BodyReversal 和 Level03Flow 操作；不等同于人工键盘和手感验收。
未执行独立 Player Build。最终仍建议人工确认：两个反转格的识别、E 操作距离、阅读反馈节奏和不同屏幕尺寸下的画面可读性。

截图工具问题：最后调用 Unity MCP 的合成截图时，插件 `ScreenshotUtility.cs:197` 触发了 Unity 的 PlayerLoop 递归错误。
堆栈没有指向本关脚本；未修改插件或依赖。`start-screen.png` 是最后实际合成画面，确认 HUD 白字可读。
部分早期截图是中间调试布局或旧 GUI 缓存，不作为最终画面验收依据。`06-layout.png` 用于查看最终结构，拍摄时仅在运行态临时关闭了视野遮罩和地形显示。
