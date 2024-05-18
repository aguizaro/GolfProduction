using UnityEngine;
using UnityEngine.UI;

public class ToggleFullscreen : MonoBehaviour
{
    private Button fullscreenButton;

    void Awake()
    {
        fullscreenButton = GetComponent<Button>();
        if (fullscreenButton == null)
        {
            Debug.LogError("ToggleFullscreen script is attached to a GameObject without a Button component.");
            return;
        }
        fullscreenButton.onClick.AddListener(Toggle);
    }
    void Toggle()
    {
        Screen.fullScreen = !Screen.fullScreen;
    }
}
