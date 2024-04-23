using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Data Manager class acts as an interface for other classes to read and write specific data to data classes.
// Data is broken up into separate classes (GameData, SettingsData, etc.) to allow relevant data to be accessed
// Reference DataManager methods from any class using "DataManager.instance.methodName()"
public class DataManager : MonoBehaviour
{
    // Assigns an instance of this manager singleton and allows access to be public but edits to be private
    public static DataManager instance { get; private set; }

    private SettingsData settingsData;

    private void Awake()
    {
        // Do not allow for multiple instances of the DataManager
        if (instance != null)
        {
            Debug.LogError("Found more than one Data Persistence Manager in the scene");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        LoadData();
    }

    public void NewSettingsData()
    {
        settingsData = new SettingsData();
    }

    // Loads json data into data class variable (not yet implemented)
    public void LoadData()
    {
        if (settingsData == null) 
        {
            Debug.Log("No save data found, creating new data.");
            NewSettingsData();
        }
    }

    // Saves data class variable to json data (not yet implemented)
    public void SaveData()
    {
    }

    public SettingsData GetSettingsData()
    {
        return settingsData.Copy();
    }

    public void SetSettingsData(SettingsData new_settings_data)
    {
        settingsData = new_settings_data;
    }
}
