# Maze 战后废墟地图

交付场景：`Assets/Scenes/Maze_Aftermath.unity`。打开后直接 Play，沿用原 Maze 的 WASD 移动、按压板 F 换位、头脚接触判负与视野遮挡。

## 场景内容

- 从现有 `Maze.unity` 的实际 Transform 提取墙线，而非重新随机生成：5×5 格、单格约 3.75×3.47 米，35 段墙线及入口高台。
- Blender 制作破损混凝土墙、露筋、裂缝铺装、外围建筑残墙、坍塌楼板、断管、道路隔离墩和碎砖。
- Meshy **T2 / image-to-3D** 制作烧毁吉普、钢筋混凝土残骸。共摆放 2 辆吉普、18 组残骸，复用网格与材质。
- 四处低强度余火及循环烟尘粒子；附带独立的色彩、Bloom、ACES Volume。
- 原 Maze 的 40 个碰撞体、角色、视野源、反转格及关卡目标保留在新场景中。旧白盒渲染器隐藏，用 Blender 模型呈现外观。

## 文件

| 内容 | 路径 |
| --- | --- |
| Unity 场景 | `Assets/Scenes/Maze_Aftermath.unity` |
| 可编辑 Blender 文件，贴图已打包 | `SourceArt/MazeAftermath/MazeAftermath.blend` |
| 建筑 GLB | `Assets/Environment/MazeAftermath/Models/MazeAftermath_Architecture.glb` |
| T2 原始 GLB | `Assets/Environment/MazeAftermath/Meshy/` |
| 以最大水平尺寸 1 米归一化的 T2 Prefab | `Assets/Environment/MazeAftermath/Prefabs/` |
| 参考图、地面及墙面纹理 | `Assets/Environment/MazeAftermath/Reference/`、`Textures/` |
| 全景、近景、残骸、游戏截图 | `Captures/MazeAftermath/` |
| 原布局、制作清单、生成任务、提示词与验证记录 | `SourceArt/MazeAftermath/` |

Unity 中 `Maze Aftermath - Environment` 是美术总根节点，下面可单独开关建筑、T2 装饰和烟尘。原来的 `GeneratedMaze` 是保留的玩法碰撞结构。较小的散落碎片只有视觉；T2 残骸和外围建筑有静态 MeshCollider。

## 渲染范围

新相机使用 `Aftermath_Renderer.asset`（PC_RPAsset 的 Renderer 索引 1），关闭该副本的 `Hand Drawn Comic Ink`，保留 `Vision Range Mask`、SSAO 和 `Global Visible Smoke`。PC_RPAsset 只追加了 Renderer 引用，默认索引仍为 0；原 Maze 和用户已有的 `PC_Renderer.asset` 黑白漫画效果保留。要在新场景恢复漫画风格，将 Main Camera 的 Renderer 改为 Default 即可。

## 已完成验证

- 原 Maze 文件 SHA-256 与提取布局时一致，没有覆盖用户原场景。
- 40 个原碰撞体保留；半径 0.34 米、离地 0.85 米的通道探测没有丢失原可通行边，25/25 格连通。
- T2 模型实测：吉普 15,595 三角形，残骸 13,237 三角形；底色和法线 4096²，打包材质贴图 2048²；缺失材质为 0。具体实例世界包围盒见 `validation.json`。
- 在 Play Mode 注入 WASD 键盘输入，经 12 个路径点走到中心反转格，再注入 F。位置交换成功，无角色传送辅助，无意外 Game Over。详见 `play_validation.json`。
- 检查了 Unity 彩色全景、走廊和残骸近景。`Play_AfterSwap.png` 保留运行时视野遮挡；其余美术截图临时关闭视野遮罩以展示全图。
- 最终 Play Mode 控制台检查无 error / warning；未运行独立 Player 构建，也未将新场景加入关卡目录或 Build Settings。

## 制作流程与来源

图片由内置 imagegen 生成并检查后保存到项目；T2 任务只提交 image 模式，显式模型 `meshy-t2`，默认 GLB，纹理 4k。保存的任务 ID 可供查询，避免重复提交。完整图像提示词在 `SourceArt/MazeAftermath/image_prompts.json`。

原有 Poly Haven `concrete_wall_008`（CC0，Charlotte Baglioni / Dario Barresi）提供混凝土法线，原底色作为 Blender 隐藏备选保留；最终墙面和地面底色由 imagegen 生成。原素材链接：<https://polyhaven.com/a/concrete_wall_008>。

制作脚本依次为 `Tools/extract_maze_layout.py`、`build_maze_aftermath.py`、`finish_maze_aftermath_blender.py`、`polish_maze_aftermath_blender.py`，后三个在 Blender 执行。Unity 构建、渲染配置和截图使用同目录中的 `build_maze_aftermath_unity.cs`、`configure_maze_aftermath_renderer.cs`、`capture_maze_aftermath.cs`，通过 `run_unity_code_file.ps1` 调用 MCP。构建脚本会拒绝覆盖已有新场景。

原来的几何油漆/弹痕叠层在 Blender 的 `Aftermath_Patina` 中隐藏保存，可自行启用；最终 Unity GLB 使用纹理弹痕，不导出该叠层。
