using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SettingsData
{
    //********* Settings Data *********//

    // Accessibility
    public string language;
    public bool oneHandMode;

    // Game
    public float cameraSensitivity;
    public float volume;

    public SettingsData() 
    {
        // Initialize Game Settings Data
        this.language = "English";
        this.oneHandMode = false;

        this.cameraSensitivity = 0;
        this.volume = 0;
    }

    public SettingsData Copy()
    {
        return new SettingsData
        {
            language = this.language,
            oneHandMode = this.oneHandMode,
            cameraSensitivity = this.cameraSensitivity,
            volume = this.volume,
        };
    }
}
