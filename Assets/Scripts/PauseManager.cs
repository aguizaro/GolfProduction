using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    // TODO: Implement a boolean toggle to say whether to start from the pause menu or the options menu

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

    // Volume updates as slider is moved
    public void SetTempSlider(float value)
    {
        this.volume = value;
    }

    public void ExitMenu()
    {
        Destroy(gameObject);
    }
}
