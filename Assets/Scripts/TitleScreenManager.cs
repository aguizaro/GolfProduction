using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleScreenManager : MonoBehaviour
{
    string serverMenuPath = "UIScene";

    // Main Title Screen Objects
    public GameObject mainTitleScreenCanvas;
    public Button mainStartButton;
    public Button mainSettingsButton;

    public GameObject settingsMenuPrefab;

    // Start is called before the first frame update
    public void StartGame()
    {
        SceneManager.LoadScene(serverMenuPath);
    }

    public void SettingsMenu()
    {
        GameObject settingsMenu = Instantiate(settingsMenuPrefab, transform.position, Quaternion.identity);
        settingsMenu.GetComponent<PauseManager>().EnableSettingsMode();
    }
}
