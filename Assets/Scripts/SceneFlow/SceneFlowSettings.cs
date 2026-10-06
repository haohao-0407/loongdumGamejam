using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Loongdum.SceneFlow
{
    [CreateAssetMenu(menuName = "Loongdum/Scene Flow Settings")]
    public sealed class SceneFlowSettings : ScriptableObject
    {
        public const string ResourcePath = "SceneFlow/SceneFlowSettings";

        [SerializeField] private LevelCatalog catalog;
        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private VisualTreeAsset uiDocument;
        [Tooltip("Optional bundled font for the target platforms. Otherwise a local CJK font is used.")]
        [SerializeField] private FontAsset uiFont;

        public LevelCatalog Catalog => catalog;
        public PanelSettings PanelSettings => panelSettings;
        public VisualTreeAsset UIDocument => uiDocument;
        public FontAsset UIFont => uiFont;

#if UNITY_EDITOR
        public void Configure(LevelCatalog levelCatalog, PanelSettings panel, VisualTreeAsset document)
        {
            catalog = levelCatalog;
            panelSettings = panel;
            uiDocument = document;
        }
#endif
    }
}
