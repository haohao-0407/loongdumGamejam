using MineHeart.GamePlay;
using UnityEditor;
using UnityEngine;

namespace MineHeart.EditorTools.CursorHotspot
{
    /// <summary>
    /// UICursorView 的编辑器工具：在原贴图上选热点，并可视化调整等比例显示大小。
    /// 不调用 Cursor.SetCursor，不修改贴图导入设置，也不需要挂在任何场景物体上。
    /// </summary>
    [CustomEditor(typeof(UICursorView))]
    [CanEditMultipleObjects]
    internal sealed class UICursorViewEditor : UnityEditor.Editor
    {
        private const int PreviewControlHint = 172903641; // 固定的 IMGUI 控件提示，用于捕获一次完整的点击或拖动。
        private const int SizeControlHint = 172903642;
        private SerializedProperty _cursorTexture; // 现有组件的鼠标贴图引用。
        private SerializedProperty _pressedCursorTexture;
        private SerializedProperty _cursorHotspot; // 现有组件的热点坐标，不另外保存一份配置。
        private SerializedProperty _useCustomCursorSize;
        private SerializedProperty _cursorHeightPixels;
        private SerializedProperty _cursorMode;
        private int _dragControlId; // 当前由本预览捕获的鼠标控件，避免拖动影响其他 Inspector 字段。
        private int _dragUndoGroup = -1; // 把一次连续拖动合并成一步撤销。
        private Vector2 _resizeStartMouse;
        private int _resizeStartHeight;
        private float _resizePointScale;
        private float _resizeAspect;

        private void OnEnable()
        {
            _cursorTexture = serializedObject.FindProperty("cursorTexture");
            _pressedCursorTexture = serializedObject.FindProperty("pressedCursorTexture");
            _cursorHotspot = serializedObject.FindProperty("cursorHotspot");
            _useCustomCursorSize = serializedObject.FindProperty("useCustomCursorSize");
            _cursorHeightPixels = serializedObject.FindProperty("cursorHeightPixels");
            _cursorMode = serializedObject.FindProperty("cursorMode");
        }

        private void OnDisable()
        {
            FinishDrag();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 保留原 Inspector 的所有配置和今后新增的字段，不重新实现运行时配置界面。
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            DrawPropertiesExcluding(serializedObject, "m_Script", "useCustomCursorSize", "cursorHeightPixels", "cursorMode");

            if (_cursorTexture == null || _pressedCursorTexture == null || _cursorHotspot == null || _useCustomCursorSize == null ||
                _cursorHeightPixels == null || _cursorMode == null)
                EditorGUILayout.HelpBox("未找到鼠标配置字段，请等待 UICursorView 和工具都编译完成。", MessageType.Error);
            else
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("光标热点与显示大小", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useCustomCursorSize);
                if (!_useCustomCursorSize.hasMultipleDifferentValues && _useCustomCursorSize.boolValue)
                    EditorGUILayout.LabelField("实际渲染方式：ForceSoftware（显示大小由本工具控制）", EditorStyles.wordWrappedLabel);
                else
                    EditorGUILayout.PropertyField(_cursorMode);
                DrawHotspotPicker();
            }

            // SerializedProperty 自动处理 Undo、场景脏标记和 Prefab Override，不直接改组件私有字段。
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHotspotPicker()
        {
            if (_cursorTexture.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox("所选组件的鼠标贴图不同，请单独选一个组件编辑热点。", MessageType.Info);
                return;
            }

            Texture2D texture = _cursorTexture.objectReferenceValue as Texture2D;
            if (texture == null)
            {
                EditorGUILayout.HelpBox("先把鼠标图片拖到上面的“鼠标贴图”，这里就能选热点和调整显示大小。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"导入贴图：{texture.width} × {texture.height} 像素");
            DrawPressedTextureHints(texture);
            DrawCursorSizeControls(texture);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("热点编辑预览（放大选点）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "左键点击或拖动选热点。红十字表示热点；放大到足够大小时会显示像素格。" +
                "原点在贴图左上角，X 向右，Y 向下。这里的放大只方便选点，不会改变游戏光标大小。",
                MessageType.Info);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("运行中修改大小或热点会在下一帧应用。退出 Play 后这些修改会被还原；需要保存请在非运行状态配置。", MessageType.Warning);

            // 预览按原图比例完整显示；所有输入换算使用实际图片矩形，外围留白不会被当作像素。
            float previewHeight = Mathf.Clamp(EditorGUIUtility.currentViewWidth - 48f, 160f, 384f);
            Rect previewArea = GUILayoutUtility.GetRect(1f, previewHeight, GUILayout.ExpandWidth(true));
            Rect imageRect = GetImageRect(previewArea, texture);
            int controlId = GUIUtility.GetControlID(PreviewControlHint, FocusType.Passive, imageRect);
            HandlePreviewInput(controlId, imageRect, texture);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(previewArea, new Color(0.13f, 0.13f, 0.13f, 1f));
                DrawCheckerboard(imageRect);
                Color previousColor = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(imageRect, texture, ScaleMode.StretchToFill, true);
                GUI.color = previousColor;
                DrawPixelGrid(imageRect, texture);

                if (!_cursorHotspot.hasMultipleDifferentValues && IsValidHotspot(_cursorHotspot.vector2Value, texture))
                    DrawHotspotMarker(imageRect, texture, _cursorHotspot.vector2Value);
            }

            if (_cursorHotspot.hasMultipleDifferentValues)
                EditorGUILayout.LabelField("当前热点：多个不同值，点击预览会统一设置所选组件。");
            else
            {
                Vector2 hotspot = _cursorHotspot.vector2Value;
                EditorGUILayout.LabelField($"当前热点：X = {hotspot.x:0.##}，Y = {hotspot.y:0.##}");
                if (!IsValidHotspot(hotspot, texture))
                    EditorGUILayout.HelpBox("热点超出了贴图范围，请点击预览重新选择。", MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("设为左上角"))
                SetHotspot(Vector2.zero);
            if (GUILayout.Button("设为中心"))
                SetHotspot(new Vector2(texture.width / 2, texture.height / 2));
            EditorGUILayout.EndHorizontal();

            if (!texture.isReadable)
                EditorGUILayout.HelpBox("预览不要求贴图可读，但运行时缩放光标要求可读。请在贴图导入设置中开启 Read/Write 后 Apply。", MessageType.Warning);
        }

        private void DrawPressedTextureHints(Texture2D normalTexture)
        {
            if (_pressedCursorTexture.hasMultipleDifferentValues)
                return;

            Texture2D pressedTexture = _pressedCursorTexture.objectReferenceValue as Texture2D;
            if (pressedTexture == null)
                return;

            EditorGUILayout.LabelField("普通与按下外观共用下面的显示大小和点击热点。", EditorStyles.wordWrappedLabel);
            if (pressedTexture.width != normalTexture.width || pressedTexture.height != normalTexture.height)
                EditorGUILayout.HelpBox($"按下图为 {pressedTexture.width} × {pressedTexture.height}，与普通图尺寸不同。请让两张图的画布尺寸、导入后的尺寸和图案位置一致；否则运行时不会使用按下图。", MessageType.Warning);
            if (!pressedTexture.isReadable)
                EditorGUILayout.HelpBox("按下图未开启 Read/Write，请到该贴图导入设置中勾选后 Apply；否则运行时保持普通外观。", MessageType.Warning);
        }

        private void DrawCursorSizeControls(Texture2D texture)
        {
            if (_useCustomCursorSize.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox("所选组件的大小控制模式不同，请先统一勾选“自定义光标大小”。", MessageType.Info);
                return;
            }

            if (!_useCustomCursorSize.boolValue)
            {
                EditorGUILayout.HelpBox("当前保留原生光标模式，Auto 的显示尺寸可能由系统适配。勾选“自定义光标大小”后可在这里可视化缩放。", MessageType.Info);
                return;
            }

            int maximumHeight = UICursorView.GetMaximumCursorHeight(texture);
            EditorGUI.showMixedValue = _cursorHeightPixels.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int height = EditorGUILayout.IntSlider("光标高度（屏幕像素）",
                _cursorHeightPixels.intValue, 1, maximumHeight);
            if (EditorGUI.EndChangeCheck())
                SetCursorHeight(height, texture);
            EditorGUI.showMixedValue = false;

            EditorGUILayout.BeginHorizontal();
            DrawSizePreset(32, texture);
            DrawSizePreset(48, texture);
            DrawSizePreset(64, texture);
            DrawSizePreset(96, texture);
            DrawSizePreset(128, texture);
            if (GUILayout.Button("原尺寸"))
                SetCursorHeight(texture.height, texture);
            EditorGUILayout.EndHorizontal();

            if (_cursorHeightPixels.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox("所选组件的大小不同。调整滑杆或点击尺寸按钮即可统一，热点仍按各自配置保留。", MessageType.Info);
                return;
            }

            Vector2Int size = UICursorView.CalculateDisplaySize(texture, _cursorHeightPixels.intValue);
            EditorGUILayout.LabelField($"游戏显示大小：{size.x} × {size.y} 像素（等比例）");
            EditorGUILayout.LabelField("拖动下方右下角的蓝色方块可缩放。缩放不会修改原图，热点自动同步。", EditorStyles.wordWrappedLabel);

            Rect area = GUILayoutUtility.GetRect(1f, 224f, GUILayout.ExpandWidth(true));
            float pointScale = Mathf.Min(1f / EditorGUIUtility.pixelsPerPoint,
                Mathf.Min(Mathf.Max(1f, area.width - 32f) / size.x, (area.height - 32f) / size.y));
            Rect imageRect = new Rect(area.x + 16f, area.y + 16f, size.x * pointScale, size.y * pointScale);
            Rect handleRect = new Rect(imageRect.xMax - 6f, imageRect.yMax - 6f, 12f, 12f);
            int controlId = GUIUtility.GetControlID(SizeControlHint, FocusType.Passive, area);
            HandleSizeInput(controlId, handleRect, texture, pointScale);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(area, new Color(0.13f, 0.13f, 0.13f, 1f));
                DrawCheckerboard(imageRect);
                Color previousColor = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(imageRect, texture, ScaleMode.StretchToFill, true);
                GUI.color = previousColor;
                Color borderColor = new Color(0.3f, 0.65f, 1f, 1f);
                EditorGUI.DrawRect(new Rect(imageRect.x, imageRect.y, imageRect.width, 1f), borderColor);
                EditorGUI.DrawRect(new Rect(imageRect.x, imageRect.yMax - 1f, imageRect.width, 1f), borderColor);
                EditorGUI.DrawRect(new Rect(imageRect.x, imageRect.y, 1f, imageRect.height), borderColor);
                EditorGUI.DrawRect(new Rect(imageRect.xMax - 1f, imageRect.y, 1f, imageRect.height), borderColor);
                if (!_cursorHotspot.hasMultipleDifferentValues && IsValidHotspot(_cursorHotspot.vector2Value, texture))
                {
                    Vector2 hotspot = UICursorView.CalculateDisplayHotspot(_cursorHotspot.vector2Value,
                        new Vector2Int(texture.width, texture.height), size);
                    DrawHotspotMarker(imageRect, new Vector2(size.x, size.y), hotspot);
                }
                EditorGUI.DrawRect(handleRect, borderColor);
                EditorGUI.DrawRect(new Rect(handleRect.x + 3f, handleRect.y + 3f, 6f, 6f), Color.white);
            }

            float previewZoom = pointScale * EditorGUIUtility.pixelsPerPoint;
            EditorGUILayout.LabelField(previewZoom >= 0.999f
                ? "尺寸预览：1:1（按 Editor 的屏幕像素密度换算）"
                : $"尺寸预览：{previewZoom:0.##} 倍，因 Inspector 空间不足而缩小；游戏仍使用上面的实际像素尺寸。",
                EditorStyles.wordWrappedLabel);
        }

        private void DrawSizePreset(int height, Texture2D texture)
        {
            using (new EditorGUI.DisabledScope(height > UICursorView.GetMaximumCursorHeight(texture)))
            {
                if (GUILayout.Button(height.ToString()))
                    SetCursorHeight(height, texture);
            }
        }

        private void SetCursorHeight(int height, Texture2D texture)
        {
            _cursorHeightPixels.intValue = Mathf.Clamp(height, 1, UICursorView.GetMaximumCursorHeight(texture));
            Repaint();
        }

        private void HandleSizeInput(int controlId, Rect handleRect, Texture2D texture, float pointScale)
        {
            Event current = Event.current;
            EventType eventType = current.GetTypeForControl(controlId);
            if (eventType == EventType.MouseDown && current.button == 0 && handleRect.Contains(current.mousePosition))
            {
                BeginDrag(controlId, "调整光标显示大小");
                _resizeStartMouse = current.mousePosition;
                _resizeStartHeight = UICursorView.CalculateDisplaySize(texture, _cursorHeightPixels.intValue).y;
                _resizePointScale = pointScale;
                _resizeAspect = texture.width / (float)texture.height;
                current.Use();
            }
            else if (eventType == EventType.MouseDrag && GUIUtility.hotControl == controlId && current.button == 0)
            {
                // 把鼠标位移投影到等比例缩放方向，横向、纵向或斜向拖动都能调整同一个尺寸。
                Vector2 direction = new Vector2(_resizeAspect, 1f);
                float deltaHeight = Vector2.Dot(current.mousePosition - _resizeStartMouse, direction) /
                    (direction.sqrMagnitude * _resizePointScale);
                SetCursorHeight(Mathf.RoundToInt(_resizeStartHeight + deltaHeight), texture);
                current.Use();
            }
            else if (eventType == EventType.MouseUp && GUIUtility.hotControl == controlId && current.button == 0)
            {
                FinishDrag();
                current.Use();
            }
            else if (eventType == EventType.Ignore && _dragControlId == controlId)
                FinishDrag();
        }

        private static Rect GetImageRect(Rect area, Texture2D texture)
        {
            float availableWidth = Mathf.Max(1f, area.width - 16f);
            float availableHeight = Mathf.Max(1f, area.height - 16f);
            float scale = Mathf.Min(availableWidth / texture.width, availableHeight / texture.height);
            Vector2 size = new Vector2(texture.width * scale, texture.height * scale);
            return new Rect(area.center - size * 0.5f, size);
        }

        private void HandlePreviewInput(int controlId, Rect imageRect, Texture2D texture)
        {
            Event current = Event.current;
            EventType eventType = current.GetTypeForControl(controlId);
            if (eventType == EventType.MouseDown && current.button == 0 && imageRect.Contains(current.mousePosition))
            {
                BeginDrag(controlId, "设置鼠标热点");
                SetHotspot(GetPixel(current.mousePosition, imageRect, texture));
                current.Use();
            }
            else if (eventType == EventType.MouseDrag && GUIUtility.hotControl == controlId && current.button == 0)
            {
                SetHotspot(GetPixel(current.mousePosition, imageRect, texture));
                current.Use();
            }
            else if (eventType == EventType.MouseUp && GUIUtility.hotControl == controlId && current.button == 0)
            {
                FinishDrag();
                current.Use();
            }
            else if (eventType == EventType.Ignore && _dragControlId == controlId)
                FinishDrag();
        }

        private static Vector2 GetPixel(Vector2 mousePosition, Rect imageRect, Texture2D texture)
        {
            // IMGUI 的 Y 也是向下增加，与 Cursor.SetCursor 一致，不需要倒转 Y。
            int x = Mathf.FloorToInt((mousePosition.x - imageRect.x) / imageRect.width * texture.width);
            int y = Mathf.FloorToInt((mousePosition.y - imageRect.y) / imageRect.height * texture.height);
            return new Vector2(Mathf.Clamp(x, 0, texture.width - 1), Mathf.Clamp(y, 0, texture.height - 1));
        }

        private void SetHotspot(Vector2 hotspot)
        {
            _cursorHotspot.vector2Value = hotspot;
            Repaint();
        }

        private void BeginDrag(int controlId, string undoName)
        {
            GUIUtility.hotControl = controlId;
            _dragControlId = controlId;
            Undo.IncrementCurrentGroup();
            _dragUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
        }

        private void FinishDrag()
        {
            if (_dragControlId != 0 && GUIUtility.hotControl == _dragControlId)
                GUIUtility.hotControl = 0;
            _dragControlId = 0;

            if (_dragUndoGroup >= 0)
                Undo.CollapseUndoOperations(_dragUndoGroup);
            _dragUndoGroup = -1;
        }

        private static bool IsValidHotspot(Vector2 hotspot, Texture2D texture)
        {
            return hotspot.x >= 0f && hotspot.x <= texture.width - 1f &&
                hotspot.y >= 0f && hotspot.y <= texture.height - 1f;
        }

        private static void DrawCheckerboard(Rect rect)
        {
            const float tileSize = 16f; // 只在有界的 Inspector 预览中绘制，不读取或复制贴图像素。
            EditorGUI.DrawRect(rect, new Color(0.28f, 0.28f, 0.28f, 1f));
            int columns = Mathf.CeilToInt(rect.width / tileSize);
            int rows = Mathf.CeilToInt(rect.height / tileSize);
            for (int row = 0; row < rows; row++)
            {
                for (int column = row % 2; column < columns; column += 2)
                {
                    float x = rect.x + column * tileSize;
                    float y = rect.y + row * tileSize;
                    EditorGUI.DrawRect(new Rect(x, y, Mathf.Min(tileSize, rect.xMax - x), Mathf.Min(tileSize, rect.yMax - y)),
                        new Color(0.43f, 0.43f, 0.43f, 1f));
                }
            }
        }

        private static void DrawPixelGrid(Rect rect, Texture2D texture)
        {
            float pixelSize = rect.width / texture.width;
            if (pixelSize < 4f)
                return; // 缩小时不画密集网格，避免遮挡图案。

            Color gridColor = new Color(1f, 1f, 1f, 0.15f);
            for (int x = 1; x < texture.width; x++)
                EditorGUI.DrawRect(new Rect(rect.x + x * pixelSize, rect.y, 1f, rect.height), gridColor);
            for (int y = 1; y < texture.height; y++)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + y * pixelSize, rect.width, 1f), gridColor);
        }

        private static void DrawHotspotMarker(Rect rect, Texture2D texture, Vector2 hotspot)
        {
            DrawHotspotMarker(rect, new Vector2(texture.width, texture.height), hotspot);
        }

        private static void DrawHotspotMarker(Rect rect, Vector2 imageSize, Vector2 hotspot)
        {
            // 标记画在选中像素的中心，(0,0) 仍然表示原图左上角第一个像素。
            float x = rect.x + (hotspot.x + 0.5f) / imageSize.x * rect.width;
            float y = rect.y + (hotspot.y + 0.5f) / imageSize.y * rect.height;
            EditorGUI.DrawRect(new Rect(x - 9f, y - 2f, 18f, 4f), Color.black);
            EditorGUI.DrawRect(new Rect(x - 2f, y - 9f, 4f, 18f), Color.black);
            Color markerColor = new Color(1f, 0.25f, 0.18f, 1f);
            EditorGUI.DrawRect(new Rect(x - 9f, y - 1f, 18f, 2f), markerColor);
            EditorGUI.DrawRect(new Rect(x - 1f, y - 9f, 2f, 18f), markerColor);
        }
    }
}
