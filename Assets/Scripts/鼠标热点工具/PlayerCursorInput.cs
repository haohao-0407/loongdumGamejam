using UnityEngine;
using UnityEngine.InputSystem;

namespace MineHeart.GamePlay
{
    /// <summary>
    /// 只把 Alt 按住或松开的状态转发成 UIManager 的临时鼠标请求。
    /// 不直接调用 Cursor API，不认识暂停管理器，也不处理镜头、角色或面板显示。
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class PlayerCursorInput : MonoBehaviour
    {
        [Header("UI 管理入口")]
        [InspectorName("UI 管理器")]
        [Tooltip("指定场景中始终启用的 UIManager；可以挂在独立玩家输入物体上，不必与暂停管理器同物体")]
        [SerializeField] private UIManager uiManager; // 接收 Alt 临时鼠标请求的 UI 管理器。

        [Header("鼠标显示输入")]
        [InspectorName("显示鼠标操作")]
        [Tooltip("指定 System/ShowCursor；Button 类型，绑定 Alt，Interactions 留空")]
        [SerializeField] private InputActionReference showCursorAction; // 独立 System 输入组中的操作，暂停时仍然可用。

        private InputAction _showCursorAction; // 缓存已经解析的输入操作，不在每帧查找。
        private bool _enabledActionHere; // 是否由本组件启用了操作，停用时只撤销自己的启用。
        private bool _initialStateCheckBeforeEnable; // 输入原来的首次状态检查设置，停用时恢复。
        private bool _subscribed; // 是否成功订阅并开始工作，防止配置失败时错误清理共享输入。

        /// <summary>解析鼠标显示输入并检查它不会随 Player 玩法输入一起被暂停。</summary>
        private void Awake()
        {
            _showCursorAction = showCursorAction != null ? showCursorAction.action : null;

            if (_showCursorAction == null || _showCursorAction.type != InputActionType.Button ||
                (_showCursorAction.actionMap != null && _showCursorAction.actionMap.name == "Player"))
            {
                Debug.LogError("请给 PlayerCursorInput 指定独立 System 输入组中的 Button 类型 ShowCursor 操作。", this);
                enabled = false;
            }
        }

        /// <summary>连接 UIManager 并订阅按下和松开，同时处理启用时已经按着 Alt 的情况。</summary>
        private void OnEnable()
        {
            if (_showCursorAction == null)
                return;

            if (uiManager == null)
                uiManager = UIManager.Instance;

            if (uiManager == null)
            {
                Debug.LogError("PlayerCursorInput 找不到 UIManager，请先配置全局 UIManager。", this);
                enabled = false;
                return;
            }

            _initialStateCheckBeforeEnable = _showCursorAction.wantsInitialStateCheck;
            _showCursorAction.wantsInitialStateCheck = true;
            _showCursorAction.performed += HandleCursorInputChanged;
            _showCursorAction.canceled += HandleCursorInputChanged;
            _subscribed = true;
            _enabledActionHere = !_showCursorAction.enabled;

            if (_enabledActionHere)
                _showCursorAction.Enable();

            SubmitCursorRequest(Application.isFocused);
        }

        /// <summary>撤销自己的 Alt 请求并解除订阅，不会撤销暂停菜单或其他界面的请求。</summary>
        private void OnDisable()
        {
            if (!_subscribed)
                return;

            _showCursorAction.performed -= HandleCursorInputChanged;
            _showCursorAction.canceled -= HandleCursorInputChanged;
            if (uiManager != null)
                uiManager.SetCursorRequest(this, false);

            if (_enabledActionHere)
                _showCursorAction.Disable();

            _showCursorAction.wantsInitialStateCheck = _initialStateCheckBeforeEnable;
            _enabledActionHere = false;
            _subscribed = false;
        }

        /// <summary>校准按住状态，不使用游戏时间，因此暂停期间也能正确释放 Alt 请求。</summary>
        private void Update()
        {
            SubmitCursorRequest(Application.isFocused);
        }

        /// <summary>按下或松开立即转发请求，不依赖 Hold 交互的长按延迟。</summary>
        private void HandleCursorInputChanged(InputAction.CallbackContext context)
        {
            SubmitCursorRequest(Application.isFocused);
        }

        /// <summary>切换窗口时释放临时请求，回到游戏后再根据当前按键状态恢复。</summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            SubmitCursorRequest(hasFocus);
        }

        /// <summary>以本组件为请求者提交 Alt 状态；相同状态重复提交不会产生重复计数。</summary>
        private void SubmitCursorRequest(bool hasFocus)
        {
            if (_subscribed && uiManager != null)
                uiManager.SetCursorRequest(this, hasFocus && _showCursorAction.IsPressed());
        }
    }
}
