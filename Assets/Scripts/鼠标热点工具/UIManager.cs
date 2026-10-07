using System;
using System.Collections.Generic;
using UnityEngine;

namespace MineHeart.GamePlay
{
    /// <summary>
    /// 场景内的 UI 入口：统一开关面板，并汇总各个界面和 Alt 输入的鼠标请求。
    /// 不负责读取按键、暂停时间、操控角色或计算镜头；实际鼠标外观交给 UICursorView。
    /// </summary>
    [DefaultExecutionOrder(-1100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UICursorView))]
    public sealed class UIManager : MonoBehaviour
    {
        private UICursorView _cursorView; // 同物体上的唯一鼠标表现组件，只有它写入 Unity 的鼠标状态。
        private readonly HashSet<UnityEngine.Object> _cursorRequestOwners = new HashSet<UnityEngine.Object>(); // 当前仍需要鼠标的请求者；同一对象重复申请不会重复计数。
        private static readonly Predicate<UnityEngine.Object> DestroyedOwnerPredicate = IsDestroyedOwner; // 缓存清理回调，避免每帧创建委托。

        public static UIManager Instance { get; private set; } // 当前场景唯一的 UI 管理入口，与暂停管理器相互独立。
        public bool IsCursorRequested => _cursorRequestOwners.Count > 0; // 只要 Alt 或任意交互面板还有请求，就继续显示鼠标。

        /// <summary>供攻击和镜头输入查询 UI 是否占用鼠标，也包含退出 UI 后尚未松开的点击。</summary>
        public static bool BlocksPointerGameplayInput =>
            Instance != null && Instance._cursorView != null &&
            Instance._cursorView.BlocksPointerGameplayInput;

        /// <summary>缓存同物体的鼠标组件，不在每帧查找组件。</summary>
        private void Awake()
        {
            _cursorView = GetComponent<UICursorView>();
        }

        /// <summary>登记唯一入口，并把此前保存的界面请求同步给鼠标组件。</summary>
        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("场景中只能有一个 UIManager。", this);
                enabled = false;
                return;
            }

            Instance = this;
            RefreshCursorDemand();
        }

        /// <summary>释放全局入口；保留请求登记，临时重新启用时仍能恢复已打开界面的需求。</summary>
        private void OnDisable()
        {
            if (Instance != this)
                return;

            Instance = null;
            if (_cursorView != null)
                _cursorView.SetInteractionRequested(false);
        }

        /// <summary>清理已销毁却未及时释放的请求者，不依赖游戏时间倍率。</summary>
        private void Update()
        {
            if (_cursorRequestOwners.RemoveWhere(DestroyedOwnerPredicate) > 0)
                RefreshCursorDemand();
        }

        /// <summary>按请求者登记或释放鼠标；释放一个对象不会影响其他对象仍持有的请求。</summary>
        public void SetCursorRequest(UnityEngine.Object owner, bool requested)
        {
            if (ReferenceEquals(owner, null) || (requested && owner == null))
                return;

            bool changed = requested
                ? _cursorRequestOwners.Add(owner)
                : _cursorRequestOwners.Remove(owner); // 只有集合变化时才需要更新最终鼠标状态。

            if (changed)
                RefreshCursorDemand();
        }

        /// <summary>打开指定面板；面板上的 UIPanelCursorRequest 会自动申请鼠标，不会自动暂停游戏。</summary>
        public void OpenPanel(GameObject panelRoot)
        {
            if (isActiveAndEnabled && panelRoot != null)
                panelRoot.SetActive(true);
        }

        /// <summary>关闭指定面板并清理它的 UI 焦点；鼠标请求由面板组件自动释放。</summary>
        public void ClosePanel(GameObject panelRoot)
        {
            if (!isActiveAndEnabled || panelRoot == null)
                return;

            UnityEngine.EventSystems.EventSystem eventSystem =
                UnityEngine.EventSystems.EventSystem.current; // 当前场景的 UI 焦点与点击入口。

            if (eventSystem != null && eventSystem.currentSelectedGameObject != null &&
                eventSystem.currentSelectedGameObject.transform.IsChildOf(panelRoot.transform))
                eventSystem.SetSelectedGameObject(null);

            panelRoot.SetActive(false);
        }

        /// <summary>只把汇总结果交给鼠标组件，不在管理器中直接调用 Cursor API。</summary>
        private void RefreshCursorDemand()
        {
            if (Instance == this && isActiveAndEnabled)
                _cursorView.SetInteractionRequested(IsCursorRequested);
        }

        /// <summary>识别 Unity 已销毁的请求者，让漏掉的释放不会把鼠标永久留在交互模式。</summary>
        private static bool IsDestroyedOwner(UnityEngine.Object owner)
        {
            return owner == null;
        }
    }
}
