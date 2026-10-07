using UnityEngine;

namespace MineHeart.GamePlay
{
    /// <summary>
    /// 挂在需要鼠标的交互面板根节点：显示时申请鼠标，隐藏或销毁时释放自己的请求。
    /// 暂停菜单、背包等可以复用；血条、准星等常驻 HUD 不挂这个组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIPanelCursorRequest : MonoBehaviour
    {
        [Header("UI 管理入口")]
        [InspectorName("UI 管理器")]
        [Tooltip("指定始终启用的 UIManager；留空时使用当前场景的唯一实例")]
        [SerializeField] private UIManager uiManager; // 本面板共享的全局鼠标请求管理器，不引用暂停管理器。

        /// <summary>面板实际显示时登记请求；父物体关闭再重新打开也会走同一生命周期。</summary>
        private void OnEnable()
        {
            if (uiManager == null)
                uiManager = UIManager.Instance;

            if (uiManager == null)
            {
                Debug.LogError("交互面板找不到 UIManager，请创建并保持全局 UIManager 启用。", this);
                return;
            }

            uiManager.SetCursorRequest(this, true);
        }

        /// <summary>面板隐藏、组件停用或对象销毁时，只释放本面板的请求，不隐藏其他界面的鼠标。</summary>
        private void OnDisable()
        {
            if (uiManager != null)
                uiManager.SetCursorRequest(this, false);
        }
    }
}
