# 手绘漫画渲染

当前 `PC_Renderer` 的 `Hand Drawn Comic Ink` 已开启，`PixelArtRendererFeature` 已关闭。
Game 视图保持漫画效果，Scene 视图默认关闭漫画预览。
需要在 Scene 中预览时，在 `Hand Drawn Comic Ink` 的 `Cameras` 下勾选 `Apply To Scene View`。

## 画面构成

- 用深度和法线提取物体轮廓、墙面转折，叠加轻微偏移的复线与断笔。
- 将亮部保留为纸白，将中间明暗转换为不规则印刷网点，暗部补充交叉排线。
- 对球体等弯曲暗部增加墨色，保留亮部高光。
- 视野外呈现深灰底；与视野遮罩共用遮挡和传送门计算，避免描边暴露被遮挡的物体。
- 纸纹、网点和线条偏移不随时间随机变化。图案锚定在屏幕上。

顺序为：后处理 → 视野遮罩 → 烟雾 → 漫画渲染 → UI。
烟雾保留原来在视野外也能显示的行为，并一起转换为黑白风格。

## 调整

选择 `Assets/Settings/PC_Renderer.asset`，展开 `Hand Drawn Comic Ink`。

| 参数 | 用途 |
| --- | --- |
| Outline Width | 主描边宽度，目前为 1.6 |
| Line Wobble / Sketch Strength | 线条不规则程度与复线强度 |
| Halftone Spacing | 网点间距，越小越细密，目前为 2.8 |
| Halftone Strength / Hatching Strength | 网点与排线强度 |
| Curved Surface Ink | 球体等弯曲暗部的墨色强度 |
| Black Point / White Point | 适配场景光照与材质明暗；较小的 White Point 会增加留白 |
| Paper Color / Ink Color / Background Color | 纸、墨、视野外底色 |
| Paper Grain | 纸纹强度 |
| Apply To Scene View | Scene 视图预览开关，默认关闭；不影响 Game 视图 |
| Vision Mask Feature | 当前视野遮罩组件引用；跟随该组件的开关 |

线宽、网点和排线以 720 像素画面高度为参考，按实际渲染分辨率缩放。
保持摄像机抗锯齿为 None，可保留当前清晰的细网点。
要切回像素风，关闭漫画组件，再开启 `PixelArtRendererFeature`。

## 实现文件

- `Assets/Render/ComicInkRendererFeature.cs`：URP RenderGraph 全屏渲染组件。
- `Assets/Render/Resources/ComicInk.shader`：黑白明暗、钢笔线、网点、排线与纸纹。
- `Assets/Shaders/VisionMaskCommon.hlsl`：视野遮罩与漫画组件共用的可见性计算。

参考图保存于 `SourceArt/Rendering/ComicInkReference.png`。
画面与运行验证截图保存于 `Captures/ComicRendering/`。

已验证 Unity 脚本与着色器编译、当前摄像机画面，以及运行时碰撞烟雾的生成与显示。
烟雾检查产生了 1 个粒子系统、10 个粒子，运行 Console 无错误或警告。

## 玻璃提示材质

`Assets/Materials/Glass.mat` 使用 `Loongdum/Comic Glass` 着色器。
现有 `Glass.prefab` 和场景中共用该材质的三处玻璃会自动使用新效果。
透明底色上绘制稀疏的双斜线和细边框，斜线带浅色衬边，便于在网点地板上辨认。
绘制走普通透明物体通道，再经过视野遮罩和漫画处理；玻璃保持正常深度遮挡。

选中材质可调整：

| 参数 | 用途 |
| --- | --- |
| Glass Tint 的 Alpha | 玻璃底色透明度，目前为 0.035 |
| Stroke Angle | 斜线角度，目前为 45 度 |
| Stroke Group Spacing / Stroke Row Spacing | 斜线组与行的间距 |
| Pair Spacing / Stroke Length | 双线间距与单条斜线长度 |
| Stroke Width / Backing Width | 墨线与浅色衬边的宽度 |
| Hand Drawn Wobble | 静态笔触起伏，不随时间闪动 |
| Border Width | 细边框宽度，设为 0 可关闭 |
| Cull | 当前立方体玻璃使用 Back；单面网格需要双面显示时选择 Off |

标记以物体局部坐标锚定，线宽与间距按米计，物体缩放时不会把斜线一同拉粗。
边框依赖每个面 0–1 的 UV；当前立方体和标准 Quad 可以直接使用。
着色器文件为 `Assets/Shaders/ComicGlass.shader`，无需额外纹理或渲染组件。
已验证着色器编译、三处材质引用、运行时远景与近景；Console 无错误或警告。
预览保存于 `Captures/ComicGlass/runtime.png` 与 `Captures/ComicGlass/closeup.png`。
