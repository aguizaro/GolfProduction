using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseManager : MonoBehaviour, IDataPersistence
{
    // TODO: Implement a boolean toggle to say whether to start from the pause menu or the options menu

    public DataPersistenceManager dataManager;

    // Options Menu Variables
    float tempValue = 0;

    public void Awake()
    {
        dataManager.LoadGameData();
    }

    public void LoadData(GameData data)
    {
        this.tempValue = data.tempValue;
    }

    public void SaveData(ref GameData data)
    {
        data.tempValue = this.tempValue;
    }

    public void SetTempSlider(float value)
    {
        this.tempValue = value;
    }

    public void ApplySettings()
    {
        dataManager.SaveGameData();
    }

    public void ExitMenu()
    {
        Destroy(gameObject);
    }
}
