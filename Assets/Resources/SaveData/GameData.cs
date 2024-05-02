using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    // Game Settings Data
    public string language;
    public float tempValue;

    // Class constructor defines initial default values
    public GameData()
    {
        // Initialize Game Settings Data
        this.language = "English";
        this.tempValue = 0;
    }

    // GameData duplicator method
    public GameData Copy()
    {
        return new GameData
        {
            language = this.language,
            tempValue = this.tempValue,
        };
    }
}
