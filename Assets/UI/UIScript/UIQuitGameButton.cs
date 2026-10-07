using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.Button))]
public sealed class UIQuitGameButton : MonoBehaviour
{
    private UnityEngine.UI.Button button;

    private void Awake()
    {
        button = GetComponent<UnityEngine.UI.Button>();
    }

    private void OnEnable()
    {
        button.onClick.AddListener(QuitGame);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(QuitGame);
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
