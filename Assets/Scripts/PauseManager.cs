using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    // TODO: Implement a boolean toggle to say whether to start from the pause menu or the options menu
    string titleScreenPath = "TitleScreen";

    bool pauseMode = true;
    bool pauseActive = true;
    bool settingsActive = false;

    public GameObject pauseCanvas;
    public GameObject settingsCanvas;

    // Options Menu Variables
    float volume = 0;

    // Options Menu References
    public Slider volumeSlider;


    public void Awake()
    {
        LoadSettings();
    }

    public void LoadSettings()
    {
        // Load settings data
        SettingsData sData = DataManager.instance.GetSettingsData();

        volume = sData.volume;
        volumeSlider.value = volume;
    }

    public void ApplySettings()
    {
        SettingsData sData = DataManager.instance.GetSettingsData();
        sData.volume = volume;

        DataManager.instance.SetSettingsData(sData);
    }

    public void EnableSettingsMode()
    {
        pauseMode = false;
        pauseActive = false;
        settingsActive = true;

        pauseCanvas.SetActive(pauseActive);
        settingsCanvas.SetActive(settingsActive);
    }

    public void ExitMenu()
    {
        Destroy(gameObject);
    }

    //******** Pause Menu UI Triggers ********//

    public void PauseResumeGame()
    {
        ExitMenu();
    }

    public void PauseSettingsActivate()
    {
        pauseActive = false;
        settingsActive = true;

        pauseCanvas.SetActive(pauseActive);
        settingsCanvas.SetActive(settingsActive);
    }

    public void PauseTitleScreen()
    {
        SceneManager.LoadScene(titleScreenPath);
    }

    //******** Settings Menu UI Triggers ********//

    // Volume updates as slider is moved
    public void SetTempSlider(float value)
    {
        this.volume = value;
    }

    public void SettingsBack()
    {
        if (pauseMode)
        {
            pauseActive = true;
            settingsActive = false;

            pauseCanvas.SetActive(pauseActive);
            settingsCanvas.SetActive(settingsActive);
        }
        else
        {
            ExitMenu();
        }
    }
}
