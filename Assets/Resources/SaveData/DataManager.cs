using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager instance { get; private set; }
    private SettingsData settingsData;

    private void Awake()
    {
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

    public void LoadData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "SettingsData.json");
        if (File.Exists(filePath))
        {
            string jsonData = File.ReadAllText(filePath);
            settingsData = JsonUtility.FromJson<SettingsData>(jsonData);
            if (settingsData == null)
            {
                Debug.LogError("Failed to load settings data. Creating new data.");
                NewSettingsData();
            }
            else
            {
                Debug.Log("Settings data loaded successfully.");
            }
        }
        else
        {
            Debug.Log("No save data found, creating new data.");
            NewSettingsData();
        }
    }

    public void SaveData()
    {
        if (settingsData == null)
        {
            Debug.LogError("SettingsData is null. Cannot save data.");
            return;
        }

        string jsonData = JsonUtility.ToJson(settingsData, true);
        string folderPath = Application.streamingAssetsPath;
        string filePath = Path.Combine(folderPath, "SettingsData.json");

        // Ensure the directory exists
        Directory.CreateDirectory(folderPath);

        File.WriteAllText(filePath, jsonData);

        Debug.Log("Settings data saved to: " + filePath);
    }

    public SettingsData GetSettingsData()
    {
        return settingsData.Copy();
    }

    public void SetSettingsData(SettingsData new_settings_data)
    {
        settingsData = new_settings_data;
    }

    private void OnApplicationQuit()
    {
        settingsData.playTimes += 1;
        SaveData();
    }
}
