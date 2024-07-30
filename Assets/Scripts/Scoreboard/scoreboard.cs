using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.PropertyVariants.TrackedProperties;

public class Scoreboard : MonoBehaviour
{
    private UIManager uIManager; 
    
    private GameObject _scoreboard;

    private GameObject _scoreboardEntry;

    [Header("Config")]

    [SerializeField] private float _spaceBetweenEntries;
    [SerializeField] private float _initialX;
    [SerializeField] private float _initialY;


    private Dictionary<ulong, GameObject> _scoreboardEntries;

    // Start is called before the first frame update
    void Start()
    {
        uIManager = UIManager.instance;
        Debug.Log(uIManager);
        Debug.Log(uIManager.scoreboardUI);
        _scoreboard = uIManager.scoreboardUI;
        _scoreboardEntry = uIManager.scoreboardEntry;
        _scoreboardEntries = new Dictionary<ulong, GameObject>();
    }

    // Update is called once per frame
    void Update()
    {
        //todo not call every frame
        Debug.Log(PlayerScoreboard.ScoreboardData);
        foreach (PlayerData data in PlayerScoreboard.ScoreboardData.Values)
        {
            updatePlayer(data);
        }
        //sort the entries

        //TODO

        int i = 0;
        //order if needed
        foreach (GameObject entry in _scoreboardEntries.Values)
        {
            setEntry(entry, i);
            i++;
        }
    }

    void updatePlayer(PlayerData playerData)
    {
        //given player data, update it
        //first, if we dont have a scoreboard entry, create it
        Debug.Log(playerData);
        if (!_scoreboardEntries.ContainsKey(playerData.playerID))
        {
            _scoreboardEntries.Add(playerData.playerID, Instantiate(_scoreboardEntry, _scoreboard.transform));
        }
        //then put the info we have from player data in the entry
        GameObject entry = _scoreboardEntries[playerData.playerID];
        entry.SetActive(true);
        updateEntry(playerData, entry);
    }

    private void updateEntry(PlayerData playerData, GameObject entry)
    {
        //ScoreboardEntry Structure:
        //Player (text)
        //Hole1
        //...
        //Hole9
        TMP_Text hole = entry.transform.GetChild(playerData.currentHole).GetComponent<TMP_Text>();
        hole.text = playerData.strokes.ToString();
    }

    private void setEntry(GameObject entry, int position)
    {
        //set the object to the correct position
        Vector3 newPosition = new Vector3(_initialX, _initialY + (position * _spaceBetweenEntries), 0);
        entry.transform.SetLocalPositionAndRotation(newPosition, Quaternion.identity);
    }
}
