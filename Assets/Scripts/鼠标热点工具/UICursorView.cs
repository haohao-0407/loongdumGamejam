using UnityEngine;
using UnityEngine.InputSystem;

namespace MineHeart.GamePlay
{
    /// <summary>
    /// 全局鼠标的表现组件：只由 UIManager 提交最终交互需求，统一管理图案、可见性和锁定。
    /// 不认识暂停管理器或 Alt 按键，也不直接控制角色或 Cinemachine。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class UICursorView : MonoBehaviour
    {
        public const int MaximumCursorDimension = 1024; // 限制运行时临时贴图的最大边长，避免误拖产生巨型贴图。

        [Header("鼠标交互")]
        [InspectorName("玩法鼠标锁定模式")]
        [Tooltip("没有界面请求鼠标时使用的锁定方式；默认 Locked，锁在窗口中心并隐藏")]
        [SerializeField] private CursorLockMode gameplayLockMode = CursorLockMode.Locked; // 正常游戏中使用的鼠标锁定模式。

        [Header("自定义鼠标外观")]
        [InspectorName("鼠标贴图")]
        [Tooltip("可选透明背景 Texture2D；开启 Read/Write。保留高分辨率原图，显示大小可单独调整；留空使用默认鼠标")]
        [SerializeField] private Texture2D cursorTexture; // 全局共用的鼠标图案，所有交互界面使用同一张配置。

        [InspectorName("按下时的鼠标贴图")]
        [Tooltip("可选；鼠标可交互时，左键按住使用此图，松开恢复普通图。开启 Read/Write，与普通图保持相同画布尺寸和图案位置；共用大小与热点，留空不换图")]
        [SerializeField] private Texture2D pressedCursorTexture;

        [InspectorName("点击热点（像素）")]
        [Tooltip("实际点击点，从贴图左上角起算，X 向右、Y 向下；箭头填写箭头尖的位置")]
        [SerializeField] private Vector2 cursorHotspot; // 鼠标图片上用于真正点击的像素坐标。

        [InspectorName("鼠标渲染方式")]
        [SerializeField] private CursorMode cursorMode = CursorMode.Auto; // Auto 优先使用硬件鼠标，不支持时使用软件鼠标。

        [InspectorName("自定义光标大小")]
        [Tooltip("开启后使用 ForceSoftware，避免 Auto 把显示大小适配回系统光标尺寸；原贴图和导入设置不会被修改")]
        [SerializeField] private bool useCustomCursorSize = true;

        [InspectorName("光标高度（屏幕像素）")]
        [Tooltip("按贴图原比例自动计算宽度。热点仍按原贴图像素保存，运行时自动同步缩放")]
        [Range(1, MaximumCursorDimension)]
        [SerializeField] private int cursorHeightPixels = 64;

        private static UICursorView _activeOwner; // 真正接管 Cursor API 的唯一组件，避免两个组件争抢全局鼠标。
        private bool _ownsCursor; // 是否成功取得全局鼠标控制权。
        private bool _interactionRequested; // UIManager 汇总后的鼠标需求，不区分它来自 Alt 还是某个界面。
        private bool _lastInteractionRequested; // 上次应用的需求，用于记录离开交互模式的那一帧。
        private bool _hasFocus; // 游戏窗口是否有焦点，失焦时释放鼠标给系统。
        private bool _pointerPressOwnedByUI; // 在 UI 交互中仍按着的点击，完全松开前不能转成攻击。
        private int _returnToGameplayFrame = -1; // 交互结束的帧号，该帧仍拒绝攻击和镜头输入。
        private CursorLockMode _lockBeforeEnable; // 接管鼠标之前的锁定模式，停用组件时恢复。
        private bool _visibleBeforeEnable; // 接管鼠标之前的可见状态，停用组件时恢复。
        private bool _appearanceDirty = true; // Inspector 改动仅标记，下一次 Update 再安全调用 Cursor API。
        private CursorTextureCache _normalTextureCache; // 普通和按下分别缓存，点击不重新采样或销毁贴图。
        private CursorTextureCache _pressedTextureCache;
        private Texture2D _normalDisplayTexture;
        private Texture2D _pressedDisplayTexture;
        private Vector2 _displayHotspot;
        private CursorMode _displayCursorMode;
        private bool _showingPressedCursor; // 只在普通/按下状态改变时调用 Cursor.SetCursor。

        private struct CursorTextureCache
        {
            public Texture2D Texture; // 本组件生成、停用时释放的临时贴图。
            public Texture2D Source;
            public Vector2Int SourceSize;
            public Vector2Int DisplaySize;
            public bool IsSRGB;
            public uint SourceUpdateCount;
        }

        /// <summary>界面占用、窗口失焦、未完成的 UI 点击以及返回玩法的同帧，都不允许鼠标驱动战斗和镜头。</summary>
        public bool BlocksPointerGameplayInput =>
            _ownsCursor && (!_hasFocus || _interactionRequested ||
                _pointerPressOwnedByUI || Time.frameCount == _returnToGameplayFrame);

        /// <summary>取得唯一的鼠标控制权，保存环境并应用 UIManager 已提交的需求。</summary>
        private void OnEnable()
        {
            if (_activeOwner != null && _activeOwner != this)
            {
                Debug.LogError("场景中只能启用一个 UICursorView，所有界面应共享同一个 UIManager。", this);
                enabled = false;
                return;
            }

            _activeOwner = this;
            _ownsCursor = true;
            _lockBeforeEnable = Cursor.lockState;
            _visibleBeforeEnable = Cursor.visible;
            _hasFocus = Application.isFocused;
            _lastInteractionRequested = false;
            _pointerPressOwnedByUI = false;
            _returnToGameplayFrame = -1;
            ApplyCursorAppearance();
            RefreshCursorState(true);
        }

        /// <summary>归还鼠标状态；不恢复时间、不修改任何界面的打开状态。</summary>
        private void OnDisable()
        {
            if (!_ownsCursor)
                return;

            _ownsCursor = false;
            _activeOwner = null;
            Cursor.lockState = _lockBeforeEnable;
            Cursor.visible = _visibleBeforeEnable;

            ClearCursorAppearance();
            _pointerPressOwnedByUI = false;
        }

        private void OnValidate()
        {
            cursorHeightPixels = Mathf.Clamp(cursorHeightPixels, 1, MaximumCursorDimension);
            _appearanceDirty = true; // OnValidate 可能来自非主线程，不在这里读像素或创建贴图。
        }

        /// <summary>检查 UI 点击是否已松开，即使 timeScale 为零也保持鼠标交互可用。</summary>
        private void Update()
        {
            if (_ownsCursor && _appearanceDirty)
                ApplyCursorAppearance();
            RefreshCursorState();
            RefreshPressedAppearance();
        }

        /// <summary>接收管理器的最终鼠标需求，需求变化时立即更新，不等下一帧。</summary>
        public void SetInteractionRequested(bool requested)
        {
            if (_interactionRequested == requested)
                return;

            _interactionRequested = requested;
            if (_ownsCursor)
            {
                RefreshCursorState();
                RefreshPressedAppearance();
            }
        }

        /// <summary>失焦时解锁；重新获得焦点时按当前界面需求恢复鼠标状态。</summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!_ownsCursor)
                return;

            _hasFocus = hasFocus;
            RefreshCursorState(true);
            RefreshPressedAppearance();
        }

        /// <summary>唯一的可见性与锁定更新路径，并保留 UI 点击直到它完全结束。</summary>
        private void RefreshCursorState(bool forceApply = false)
        {
            if (!_ownsCursor)
                return;

            bool pointerPressed = Mouse.current != null &&
                (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed); // 只读取鼠标点击状态用于防穿透，不在这里读取 Alt。

            if (_lastInteractionRequested && !_interactionRequested)
                _returnToGameplayFrame = Time.frameCount;

            if (pointerPressed && (_interactionRequested || _lastInteractionRequested ||
                Time.frameCount == _returnToGameplayFrame))
                _pointerPressOwnedByUI = true;
            else if (!pointerPressed)
                _pointerPressOwnedByUI = false;

            if (forceApply || _interactionRequested != _lastInteractionRequested)
            {
                bool showCursor = !_hasFocus || _interactionRequested; // 窗口失焦时把鼠标交还系统；游戏内由界面请求决定。
                Cursor.lockState = showCursor ? CursorLockMode.None : gameplayLockMode;
                Cursor.visible = showCursor;
            }

            _lastInteractionRequested = _interactionRequested;
        }

        /// <summary>准备两种外观并应用共用的尺寸与热点；只在启用或配置变化时缩放贴图。</summary>
        private void ApplyCursorAppearance()
        {
            _appearanceDirty = false;
            if (cursorTexture == null)
            {
                ClearCursorAppearance();
                return;
            }

            if (!cursorTexture.isReadable)
            {
                Debug.LogWarning("鼠标贴图不可读：请在原贴图的导入设置中开启 Read/Write 后 Apply。", this);
                ClearCursorAppearance();
                return;
            }

            Vector2Int sourceSize = new Vector2Int(cursorTexture.width, cursorTexture.height);
            Vector2Int displaySize = useCustomCursorSize
                ? CalculateDisplaySize(cursorTexture, cursorHeightPixels)
                : sourceSize;

            try
            {
                _normalDisplayTexture = useCustomCursorSize
                    ? GetScaledCursorTexture(cursorTexture, displaySize, ref _normalTextureCache)
                    : cursorTexture;
                _pressedDisplayTexture = PreparePressedTexture(sourceSize, displaySize);
                _displayHotspot = CalculateDisplayHotspot(cursorHotspot, sourceSize, displaySize);
                _displayCursorMode = useCustomCursorSize ? CursorMode.ForceSoftware : cursorMode;
                RefreshPressedAppearance(true);
                if (!useCustomCursorSize)
                    ReleaseScaledCursorTexture(ref _normalTextureCache);
            }
            catch (UnityException exception)
            {
                Debug.LogWarning($"应用光标失败：{exception.Message}。请确认鼠标贴图可读，并尝试 Compression = None。", this);
                ClearCursorAppearance();
            }
        }

        /// <summary>按下图配置失败时只放弃按下外观，仍然保留正常的普通光标。</summary>
        private Texture2D PreparePressedTexture(Vector2Int sourceSize, Vector2Int displaySize)
        {
            if (pressedCursorTexture == null)
            {
                ReleaseScaledCursorTexture(ref _pressedTextureCache);
                return null;
            }

            if (!pressedCursorTexture.isReadable)
            {
                Debug.LogWarning("按下时的鼠标贴图不可读：请开启 Read/Write 后 Apply；本次保持普通鼠标外观。", this);
                ReleaseScaledCursorTexture(ref _pressedTextureCache);
                return null;
            }

            if (pressedCursorTexture.width != sourceSize.x || pressedCursorTexture.height != sourceSize.y)
            {
                Debug.LogWarning($"按下鼠标贴图的导入尺寸必须与普通图一致（{sourceSize.x} × {sourceSize.y}），才能共用热点而不跳位置；本次保持普通鼠标外观。", this);
                ReleaseScaledCursorTexture(ref _pressedTextureCache);
                return null;
            }

            if (pressedCursorTexture == cursorTexture)
            {
                ReleaseScaledCursorTexture(ref _pressedTextureCache);
                return _normalDisplayTexture;
            }

            if (!useCustomCursorSize)
            {
                ReleaseScaledCursorTexture(ref _pressedTextureCache);
                return pressedCursorTexture;
            }

            try
            {
                return GetScaledCursorTexture(pressedCursorTexture, displaySize, ref _pressedTextureCache);
            }
            catch (UnityException exception)
            {
                Debug.LogWarning($"准备按下鼠标贴图失败：{exception.Message}。请尝试 Compression = None；本次保持普通鼠标外观。", this);
                ReleaseScaledCursorTexture(ref _pressedTextureCache);
                return null;
            }
        }

        /// <summary>只显示左键按住状态，不触发按钮点击，也不改变玩家输入或时间倍率。</summary>
        private void RefreshPressedAppearance(bool forceApply = false)
        {
            if (!_ownsCursor || _normalDisplayTexture == null)
                return;

            bool showPressed = _hasFocus && _interactionRequested && _pressedDisplayTexture != null &&
                Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (!forceApply && showPressed == _showingPressedCursor)
                return;

            Cursor.SetCursor(showPressed ? _pressedDisplayTexture : _normalDisplayTexture,
                _displayHotspot, _displayCursorMode);
            _showingPressedCursor = showPressed;
        }

        /// <summary>编辑器和运行时共用同一尺寸算法；保持长宽比，同时限制两条边的最大值。</summary>
        public static Vector2Int CalculateDisplaySize(Texture2D texture, int heightPixels)
        {
            if (texture == null)
                return Vector2Int.zero;

            int height = Mathf.Clamp(heightPixels, 1, GetMaximumCursorHeight(texture));
            int width = Mathf.Clamp(Mathf.RoundToInt(height * (texture.width / (float)texture.height)),
                1, MaximumCursorDimension);
            return new Vector2Int(width, height);
        }

        public static int GetMaximumCursorHeight(Texture2D texture)
        {
            if (texture == null || texture.width <= texture.height)
                return MaximumCursorDimension;
            return Mathf.Max(1, Mathf.FloorToInt(MaximumCursorDimension * (texture.height / (float)texture.width)));
        }

        /// <summary>配置始终使用源图左上角像素坐标；缩放后四舍五入到实际显示像素并限制在图内。</summary>
        public static Vector2 CalculateDisplayHotspot(Vector2 hotspot, Vector2Int sourceSize, Vector2Int displaySize)
        {
            float x = Mathf.Clamp(hotspot.x, 0f, sourceSize.x - 1f) * displaySize.x / sourceSize.x;
            float y = Mathf.Clamp(hotspot.y, 0f, sourceSize.y - 1f) * displaySize.y / sourceSize.y;
            return new Vector2(Mathf.Clamp(Mathf.RoundToInt(x), 0, displaySize.x - 1),
                Mathf.Clamp(Mathf.RoundToInt(y), 0, displaySize.y - 1));
        }

        private Texture2D GetScaledCursorTexture(Texture2D source, Vector2Int size, ref CursorTextureCache cache)
        {
            int sourceWidth = source.width;
            int sourceHeight = source.height;
            Vector2Int sourceSize = new Vector2Int(sourceWidth, sourceHeight);
            bool isSRGB = source.isDataSRGB;
            uint sourceUpdateCount = source.updateCount;
            if (cache.Texture != null && cache.Source == source && cache.SourceSize == sourceSize &&
                cache.DisplaySize == size && cache.IsSRGB == isSRGB && cache.SourceUpdateCount == sourceUpdateCount)
                return cache.Texture;

            // 仅在原图或显示尺寸变化时采样；正常 Update 和单独调整热点都不分配像素数组。
            Color32[] sourcePixels = source.GetPixels32();
            Color32[] outputPixels = new Color32[size.x * size.y];
            for (int y = 0; y < size.y; y++)
            {
                float sourceY = (y + 0.5f) * sourceHeight / size.y - 0.5f;
                int floorY = Mathf.FloorToInt(sourceY);
                int y0 = Mathf.Clamp(floorY, 0, sourceHeight - 1);
                int y1 = Mathf.Clamp(floorY + 1, 0, sourceHeight - 1);
                for (int x = 0; x < size.x; x++)
                {
                    float sourceX = (x + 0.5f) * sourceWidth / size.x - 0.5f;
                    int floorX = Mathf.FloorToInt(sourceX);
                    int x0 = Mathf.Clamp(floorX, 0, sourceWidth - 1);
                    int x1 = Mathf.Clamp(floorX + 1, 0, sourceWidth - 1);
                    Color bottom = Color.Lerp(Premultiply(sourcePixels[y0 * sourceWidth + x0]),
                        Premultiply(sourcePixels[y0 * sourceWidth + x1]), sourceX - floorX);
                    Color top = Color.Lerp(Premultiply(sourcePixels[y1 * sourceWidth + x0]),
                        Premultiply(sourcePixels[y1 * sourceWidth + x1]), sourceX - floorX);
                    Color color = Color.Lerp(bottom, top, sourceY - floorY);
                    // 预乘透明度再插值，防止透明像素的黑色 RGB 污染图案边缘。
                    outputPixels[y * size.x + x] = color.a > 0f
                        ? new Color(color.r / color.a, color.g / color.a, color.b / color.a, color.a)
                        : Color.clear;
                }
            }

            ReleaseScaledCursorTexture(ref cache);
            cache.Texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false, !isSRGB)
            {
                name = $"{source.name} (Cursor {size.x}x{size.y})",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            cache.Texture.SetPixels32(outputPixels);
            cache.Texture.Apply(false, false); // Cursor API 仍需要 CPU 可读数据；不创建 Mipmap。
            cache.Source = source;
            cache.SourceSize = sourceSize;
            cache.DisplaySize = size;
            cache.IsSRGB = isSRGB;
            cache.SourceUpdateCount = sourceUpdateCount;
            return cache.Texture;
        }

        private static Color Premultiply(Color color)
        {
            return new Color(color.r * color.a, color.g * color.a, color.b * color.a, color.a);
        }

        private void ClearCursorAppearance()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _normalDisplayTexture = null;
            _pressedDisplayTexture = null;
            _showingPressedCursor = false;
            ReleaseScaledCursorTexture(ref _normalTextureCache);
            ReleaseScaledCursorTexture(ref _pressedTextureCache);
        }

        private void ReleaseScaledCursorTexture(ref CursorTextureCache cache)
        {
            if (cache.Texture != null)
                Destroy(cache.Texture);
            cache = default;
        }
    }
}
