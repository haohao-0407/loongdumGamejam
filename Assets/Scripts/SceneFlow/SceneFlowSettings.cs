using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Loongdum.SceneFlow
{
    [CreateAssetMenu(menuName = "Loongdum/Scene Flow Settings")]
    public sealed class SceneFlowSettings : ScriptableObject
    {
        public const string ResourcePath = "SceneFlow/SceneFlowSettings";

        [SerializeField] private LevelCatalog catalog;
        [Tooltip("Use the project's UI/Cancel action to open the settings page.")]
        [FormerlySerializedAs("returnToSelectionAction")]
        [SerializeField] private InputActionReference openSettingsAction;
        [SerializeField] private bool returnOnCompletion = true;
        [Tooltip("整局循环播放的 BGM。留空则不放音乐。")]
        [SerializeField] private AudioClip musicClip;
        [Range(0f, 1f)]
        [SerializeField] private float musicVolume = 0.7f;

        public LevelCatalog Catalog => catalog;
        public InputActionReference OpenSettingsAction => openSettingsAction;
        public bool ReturnOnCompletion => returnOnCompletion;
        public AudioClip MusicClip => musicClip;
        public float MusicVolume => musicVolume;

#if UNITY_EDITOR
        public void Configure(LevelCatalog levelCatalog, InputActionReference settingsAction)
        {
            catalog = levelCatalog;
            openSettingsAction = settingsAction;
        }
#endif
    }
}
