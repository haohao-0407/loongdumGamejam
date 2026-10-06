# 双墙传递视野

日期：2026-10-06。新增可配置组件与独立测试；没有在第三关布置，也没有改变第三关布局、机关或已确认的规则。

## 行为

- 两面墙始终是实体墙，挡角色，也截断各自背后的本地视线。组件不会移动或传送玩家，不新增视野源，下半身不照亮场景。
- 上半身实际看到 A 的正面时，落在该墙面范围内的射线从 B 的背面继续；看到 B 的正面时，对称地从 A 背面继续。正面按墙的本地 +Z 定义，背面为 -Z。背面观察不触发。
- 按墙面朝向映射入射方向和横向位置。并非整面墙被看见一点后就全部发光：被前方障碍挡住的那一部分不会在远端出现。
- 路径预算为入口之前的距离加出口之后的距离；两墙之间的空间距离不计入。出口不重置半径，不发出独立圆形光。
- 入口墙本体可见；本地墙后阴影不被照亮。出口后的其他墙和关闭光闸继续挡光。
- 沿用 VisionSource 的 Portal Vision、Max Portal Hops、Ray Count 和最多 8 个出口视野限制。每次双墙传递占一跳，能和现有普通门户串接；路径重复访问限制继续由现有算法负责。

## 使用

预制体：`Assets/Prefabs/PairedVisionWalls.prefab`。拖入使用 VisionSource 的场景；根对象的 PairedVisionWalls 已连接 Wall A、Wall B 两个 BoxCollider。

1. 分别移动两个墙对象，调整 Y 轴旋转；选中根组件时，Gizmo 连线表示配对，箭头指向每面墙的正面。
2. 两墙须竖直、使用正缩放、没有剪切，且有效宽高相等。可分别调整位置、厚度和朝向；修改尺寸时一起修改两墙宽高。
3. BoxCollider 必须启用且非 Trigger。墙使用 VisionObstacle 层/标签，VisionSource 的障碍过滤须识别这些墙；不能把它们改成透光的普通碰撞。
4. 墙高须覆盖 `视野源 Y + Sight Height`。组件根据 BoxCollider 的 center、size 和 transform 放置面，不依据渲染网格估算。
5. Face Clearance 是入口/出口平面在碰撞体表面外的世界距离，默认 0.01 米，允许 0.002–0.05 米；避免把平面放入实体墙。
6. 运行时禁用组件仅停止视野传递，两墙碰撞保留。禁用任一墙、解除引用、宽高不匹配或倾斜时，两向传递均停止。

Edit Mode 与 Play Mode 均自动建立和更新面，和 VisionSource 的编辑态 Game 预览保持一致；重载场景或退出 Play 后仍能显示远端视野。预制体资产本身不生成临时对象，只在已加载场景中的实例生成。内部四个临时 VisionPortal 只用于两向入口/出口，使用 DontSave；无需手工编辑，也不会保存到预制体或场景或标记场景未保存。`RefreshGeometry()` 也可用于显式预览和独立验证。不能把同一墙同时分配给多个配对组件。

## 独立测试

打开 `Assets/Scenes/Tests/PairedVisionWalls_Test.unity`，点击 Play。左侧上半身前方是 A，右侧是 B；A 背后的测试块保持隐藏，B 背后的测试块被传递视野照到。运行中取消根对象 PairedVisionWalls 的 Enabled，右侧视野消失，两墙仍保留。重新启用恢复传递。

测试场景复用当前 Whitebox1 实际墙体 Cube/Lit、入口/出口标记材质、Terrain、上半身、相机旋转与镜头、灯光、Volume 和漫画渲染。墙为配对示例调整宽高；没有新增材质、网格、shader、依赖，也没有加入 Build Settings。

菜单 `Tools / Loongdum / Paired Vision Walls / Validate` 在临时附加场景做真实 Physics/视野查询，结束销毁测试对象、关闭临时场景并恢复原活动场景，不保存任何游玩场景。覆盖 25 项：两向传递、墙本体/本地阴影、Whitebox1 关闭 Keep Obstacle Visible 时的表现、剩余距离、实体碰撞、入口/出口遮挡、部分遮挡、旋转、单面、距离外失效、禁用/恢复、无效尺寸/倾斜、移动后无旧视野、跳数、串联及普通门户回归。

本次 25 项全部通过。独立测试场景 Play Mode 真实渲染查询得到：启用时 PortalViewCount=1，远端测试点可见、本地墙后不可见、入口墙可见；禁用后 PortalViewCount=0，远端不可见、两墙碰撞仍启用；重启组件后恢复。查看了正常 ScreenCapture 的 enabled.png 和 disabled.png。0 缺失脚本。未执行 Player Build，未在第三关跑通关验证。

### 编辑态预览修正（2026-10-06）

初版只在 Play 中生成门户，最终停止并重载测试场景后，VisionSource 仍在编辑态绘制本地视野，双墙却没有门户，因此实际 Game 画面失去远端视野。此前 Play 验证成功不足以证明最终编辑态预览正常。

组件现在在编辑态和 Play 自动更新，并重建缺失的链接和入口注册；排除未加载的预制体资产。本次重新通过25项隔离检查，另外真实执行「编辑态 → Play → 停止 → Play → 停止并重载」五个状态检查，均为4个所属临时门户、1个出口视野、远端可见、入口墙可见、本地阴影不可见、场景无未保存改动。`CheckLoadedDemo()` 可在独立测试场景 Game 画面完成绘制后复查这些状态。

最终直接读取编辑态 Game 视图的渲染纹理并导出 `edit-after-reload.png`，仅校正 Direct3D 的纹理上下方向；没有重新绘制或合成场景。实际图片已查看，重载后的最终画面保留右侧远端视野。摘要为 `lifecycle-validation.json`。编辑态保留4个本组件所属的临时门户是正常预览所需，不是未清理的游玩残留。

验证摘要和截图：`Captures/PairedVisionWalls/validation.json`、`play-validation.json`、`enabled.png`、`disabled.png`。

## 文件与影响

- `Assets/Scripts/PairedVisionWalls.cs`：配对、面位置、生命周期、无效配置和 Gizmo。
- `Assets/Scripts/VisionSource.cs`：仅内部增加新组件入口墙本体保留逻辑；公共接口保持不变。普通门户仍在入口孔口截断视野，已单独回归。计算入口墙 footprint 不会额外放开其背后区域。
- `Assets/Prefabs/PairedVisionWalls.prefab`：两墙及复用材质的正反面标记；无临时孔口。
- `Assets/Editor/PairedVisionWallsValidation.cs`、测试场景及对应 .meta：独立验证与演示。

Whitebox1、第三/第四关、ProBuilder 配置的 SHA256 均保持本次开始值，第二关文件未作本次修改。没有修改公共接口、普通 VisionPortal、共享材质、Packages、ProjectSettings；未暂存、提交或推送。
编译无错误；Console 有三条既有弃用警告，来自 CollapsedBuildingSceneBuilder 和第二关验证文件的 FindFirstObjectByType，未越界修复。

采样仍使用现有水平射线精度，极窄或接近墙边的配置需人工看图。第一版要求等宽等高、仅正面进入；同一墙多重配对不受支持。
