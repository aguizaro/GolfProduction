using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// Using implementation from:
// https://www.youtube.com/watch?v=aUi9aijvpgs&ab_channel=ShapedbyRainStudios
// 
// 
public class DataPersistenceManager : MonoBehaviour
{
    private GameData gameData;
    private List<IDataPersistence> dataPersistenceObjects;

    // Assigns an instance of this manager singleton and allows access to be public but edits to be private
    public static DataPersistenceManager instance { get; private set; }

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than one Data Persistence Manager in the scene");
        }
        instance = this;
    }

    private void Start()
    {
        this.dataPersistenceObjects = FindAllDataPersistenceObjects();
        LoadGameData();
    }

    public void NewGameData()
    {
        this.gameData = new GameData();
    }

    public void LoadGameData()
    {
        if (this.gameData == null) 
        {
            Debug.Log("No save data found, creating new data.");
            NewGameData();
        }

        foreach (IDataPersistence dataPersistenceObject in dataPersistenceObjects)
        {
            dataPersistenceObject.LoadData(gameData);
        }
    }

    public void SaveGameData()
    {
        foreach (IDataPersistence dataPersistenceObject in dataPersistenceObjects)
        {
            dataPersistenceObject.SaveData(ref gameData);
        }
    }

    // Returns a list of all game objects that use the IDataPersistence interface
    private List<IDataPersistence> FindAllDataPersistenceObjects()
    {
        IEnumerable<IDataPersistence> dataPersistenceObjects = FindObjectsOfType<MonoBehaviour>().OfType<IDataPersistence>();
        
        return new List<IDataPersistence>(dataPersistenceObjects);
    }
}
