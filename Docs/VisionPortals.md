# 视野传送门

`Whitebox1` 场景中的 `Vision Portal Pair` 已连接 Portal A 与 Portal B。
可将 `Assets/Prefabs/VisionPortalPair.prefab` 拖入其他使用 VisionSource 的场景。

## 设置

1. 分别移动 Portal A 和 Portal B 到入口与出口位置。
2. 两端的 `VisionPortal.Linked Portal` 指向另一端；预制体已配置好。
3. 调整 `Width`、`Height` 和 Y 轴旋转。门保持竖直，中心高度应使水平视线落在门洞内。
4. Scene 中蓝色箭头指向门的正面。默认视线从正面进入，沿出口的正面方向继续传播。
5. 勾选 `Two Sided` 后可以从两面进入。

传送门按矩形门洞计算交点，无需 Collider。放在墙上时，把入口平面放在墙的近侧表面之前，
出口放在另一处墙面的外侧，避免射线先命中墙或从墙体内部出发。
预制体的彩色门框没有碰撞体；它们用于标示位置和方向。

## 视野规则

- 入口必须在可见范围内，且没有有效障碍挡在前面。
- 门洞只传递实际可见的方向；一半入口被遮住时，出口也只传递对应的一半视野。
- 保留进入方向、门洞横向位置和剩余视野距离，出口不会生成独立的完整圆形视野。
- 视野半径计算入口前与出口后的路径长度；两端之间的空间距离不计入路径。
- 出口的 Tag/Layer 障碍判断、Trigger 忽略规则与原视野一致。
- `VisionSource > Portal Vision` 控制整个功能。
- `Max Portal Hops` 默认 1，可设置 1–4；每个视野源最多计算 8 个出口视野，并阻止同一路径反复访问同一对门。
- `Ray Count` 决定边缘采样精度，极窄门洞可能需要提高射线数量。

`VisionSource.IsPointVisible(position)` 可查询最近一次更新的水平可见性；
`PortalViewCount` 返回当前有效出口视野数量。编辑模式与运行模式都会随门的位置、连接和启用状态更新。

## 验证

本次 Unity 内的 23 项逻辑检查通过，涵盖基础圆形范围、门洞裁剪、剩余距离、入口与出口遮挡、
部分遮挡、单面与双面、旋转、移动、断开连接、链式传递与跳数上限、关闭功能和射线分辨率变化。
主摄像机画面对比保存在 `Temp/VisionPortalValidation/`。
