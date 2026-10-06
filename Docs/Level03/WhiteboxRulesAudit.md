# 第三关与 Whitebox1 规则对照

日期：2026-10-06。基准是当前磁盘上的 `Assets/Scenes/Whitebox1.unity` 及其实际组件源码；通过 Unity Preview Scene 只读检查，没有保存基准场景。外部 HTML 只是参考布局，其历史指令和验证结论不作为授权或证据。

## 已一致

- 第三关的依赖中，没有 Whitebox1 未引用的额外视觉资源（排除第三关场景及自己的脚本）。
- WASD 连续移动，速度 5，CharacterController 的高度、半径、台阶和皮肤宽度与 Whitebox1 一致；没有格子移动或跳跃功能。
- F 反转使用原有 BodyReversal：仅下半身落地且站格，交换 X/Z，各自保留高度，目标有实体障碍则拒绝。
- 下半身不提供局部视野；上半身 VisionSource 提供连续水平视野。门户使用原有 VisionPortal，单面、成对传递方向和剩余距离，不重新发光，不传送玩家。
- 玻璃复用实际材质和实体碰撞，Default 层、不标记视野障碍，挡腿不挡视野；墙使用 VisionObstacle 层/标记遮挡视野。
- 主光、点光、漫画渲染、角色和动画资源均来自 Whitebox1。

## 本次修正

- 第三关曾将上半身 SphereCollider 改为 Trigger，和 Whitebox1 的实体碰撞不同。已恢复实体碰撞，构建器不再覆盖该设置。
- 实体上半身会把下半身挡在约 0.95 米外，原本小于 0.9 米的汇合条件无法触发。第三关汇合距离改为 1.1 米，允许碰撞间距，不改共享碰撞或技能代码。

修改后编译通过，Console 无错误或警告。模拟键盘通过现有 Input System、移动与反转组件走完两个反转、A/C、压力板观察 B、开 Z 和汇合：51 个路点、2087 帧、约 24.9 秒，Completed=true、Stage=5，完成后移动停止。该验证无直接角色传送，但不替代人工手感验收；未执行 Player Build。

## 已确认保留的第三关规则

| 项目 | Whitebox1 当前行为 | 第三关当前行为 | 完全照搬的影响 |
| --- | --- | --- | --- |
| 视野参数 | 半径 10 米、512 射线、最多 1 跳 | 半径 30 米、1024 射线、最多 3 跳 | 现有三段光路不能照到 B；保留观察锁会无法通关 |
| E 与门 | DoorAnimatorToggle 直接响应 E，不判断附近或机关条件，可反复开关 | 邻近拉杆 E；A/C/B 连接对应门；路门单向打开；a 随板打开关闭 | E 会绕过机关条件，破坏当前解谜依赖 |
| 关门与视野 | 门扇实体碰撞位于 Default，无视野障碍标记；挡腿但不挡视野 | 原门扇碰撞禁用，使用整格实体/视野障碍；关门挡腿、挡视野 | a 不再截断视线，压力板的光路作用失效；门扇碰撞也需要重新验证路线 |

用户已明确确认：以上三项差异以第三关为准，不做修改。保留 30 米、1024 射线、最多 3 跳；保留邻近拉杆及机关条件控制门；保留关门同时挡腿、挡视野。这三项属于已认可的第三关规则，后续不再为对齐 Whitebox1 而修改。A/C 顺序锁、B 的观察条件、压力板、HUD 及重置是第三关专属流程，Whitebox1 中没有对应机关可直接对照。

Whitebox1 和 ProBuilder 设置的 SHA256 与交接记录一致；暂存区为空。未修改共享资源、Packages 或 ProjectSettings。

## 地面美术修正

此前“所有资源均来自 Whitebox1”的依赖检查不足以保证用法一致：第三关用墙体 Cube/Lit 铺设了 212 块可见地板，并把 Terrain 下移 0.2 米；Whitebox1 实际地面直接显示 Terrain。这会盖住原地面表现并引入格子缝隙。

已移除第三关地板的 MeshRenderer/MeshFilter，保留 212 个通行碰撞；Terrain 恢复 Whitebox1 的原位置 `(-250, 0, -250)`，仍引用同一 WhiteBoxTerrain.asset 和 URP TerrainLit.mat。未修改 TerrainData、材质或 shader。构建器同步生成仅有碰撞的地板对象。墙、玻璃、门、角色、反转格和门户的网格/材质引用与 Whitebox1 一致，布局尺寸按第三关保留。

实际运行截图为 `Captures/Level03/terrain-ground-corrected.png`，通过正常 ScreenCapture 帧截图获取并检查；没有使用此前发生 PlayerLoop 递归的插件合成截图方法。三项已确认的第三关规则保持不变。

修改后脚本编译无错误。编译阶段发现第三关以外的 CollapsedBuildingSceneBuilder.cs:347 有一条 FindFirstObjectByType 的弃用警告，未修改该文件。随后模拟键盘完成 51 个路点、两个反转、机关/观察和汇合，约 25 秒，Completed=true；结束时 Console 未返回错误或警告。已退出 Play Mode，场景已保存且无未保存改动。未执行 Player Build。
