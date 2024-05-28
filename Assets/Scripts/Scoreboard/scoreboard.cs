using System.Collections;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.PropertyVariants.TrackedProperties;

public class Scoreboard : MonoBehaviour
{
    private UIManager uIManager = UIManager.instance;
    
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
        _scoreboard = uIManager.scoreboardUI;
        _scoreboardEntry = uIManager.scoreboardEntry;
    }

    // Update is called once per frame
    void Update()
    {
        foreach (PlayerData data in PlayerScoreboard.ScoreboardData.Values)
        {
            updatePlayer(data);
        }

        int i = 0;
        //order if needed
        foreach (GameObject entry in _scoreboardEntries.Values)
        {
            setEntry(entry, i);
            i++;
        }
    }

    void updatePlayer(PlayerData playerData) {
        //given player data, update it
        //first, if we dont have a scoreboard entry, create it
        //then put the info we have from player data in the entry
        //then sort the ent
    }

    void setEntry(GameObject entry, int position) {
        //todo set the object to the correct position
    }
}
