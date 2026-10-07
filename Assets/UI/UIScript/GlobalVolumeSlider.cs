using UnityEngine;
using UnityEngine.UI;

public class GlobalVolumeSlider : MonoBehaviour
{
    [SerializeField] private Slider slider;

    private void Awake()
    {
        if (slider == null)
            slider = GetComponent<Slider>();

        // 初始化滑条
        slider.minValue = 0f;
        slider.maxValue = 1f;

        slider.value = AudioListener.volume;

        slider.onValueChanged.AddListener(SetVolume);
    }

    private void SetVolume(float value)
    {
        AudioListener.volume = value;
    }

    private void OnDestroy()
    {
        slider.onValueChanged.RemoveListener(SetVolume);
    }
}