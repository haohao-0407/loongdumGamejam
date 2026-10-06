漫画废墟地面 / Manga Ruins Ground
生成方式：内置 image_gen。参考用户提供图片中的黑白漫画环境线稿。
贴图实际分辨率：1254 × 1254，RGB，无透明。
风格：浅灰混凝土断板、黑色裂缝、碎石、细排线和点描。

用于 Unity Terrain：
1. 选择 Terrain，打开 Paint Terrain > Paint Texture。
2. Edit Terrain Layers > Add Layer，添加 TL_MangaRuinsGround.terrainlayer。
3. Terrain Settings > Material 指定 Materials/M_MangaRuinsTerrain.mat。
4. 默认 Tile Size 为 6 × 6 米；减小尺寸会让碎石更细、更密。
5. 第一个 Terrain Layer 会作为基础覆盖；已有其他 Layer 时用画笔涂绘。

预览：
Preview/PF_MangaRuinsTerrain_Preview.prefab 是已配置的 18 × 18 米独立 Terrain。
该预览引用本包的专用 TerrainData。复制预制体实例后进行地形雕刻前，请先复制 TerrainData。
已有场景和 Terrain 的绘制数据不在本次修改范围内。

普通网格地面：
使用 Materials/M_MangaRuinsGround_Mesh.mat。UV 0–1 对应一张贴图，按网格尺寸调节平铺。

导入与材质：
Repeat；sRGB；Mip Maps；Aniso 8；保留实际 NPOT 尺寸；未压缩以保留黑白细线。
金属度 0，光滑度来源 ConstantOnly，固定光滑度 0。避免 RGB 贴图的 Alpha 被读取为全光滑。
法线与高度图未从黑色排线推算，保留平面漫画效果。
本贴图支持重复铺设，经过 Unity 2×2 视觉检查。AI 生成边缘并非逐像素相等。
生成提示词见 GenerationPrompt.txt。
Terrain_Preview.png 使用中性光照和 Scene View 相机验证材质；Terrain_WithComicInk.png 保留项目的 Comic Ink 全屏效果。
项目当前 Comic Ink 会将地面细线转为较浅的点描和色阶；两个预览均保存在 Captures/MangaRuinsGround。

