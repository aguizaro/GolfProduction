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
    
    //probably could get this from UI manager\
    [Header("Imports")]
    [SerializeField] private GameObject _scoreboard;

    [SerializeField] private GameObject _scoreboardEntry;

    [Header("Config")]

    [SerializeField] private float _spaceBetweenEntries;


    private Dictionary<ulong, GameObject> _scoreboardEntries;

    // Start is called before the first frame update
    void Start()
    {
        for (var i = 0; i <6; i++) {
            //todo space them
            //Instantiate(_scoreboardEntry);
        }

        //initialize
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void updatePlayer(PlayerData playerData) {
        //given new player data, update it
    }

    void setEntry(GameObject entry, int position) {
        //todo set the object to the correct position
    }
}
