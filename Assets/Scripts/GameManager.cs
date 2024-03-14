using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDataManager : MonoBehaviour
{
    public int strokes;
    public bool hitBall;

    // Constructor
    public PlayerDataManager()
    {
        strokes = 0;
        hitBall = false;
    }
}

public class GameManager : MonoBehaviour
{
    public static GameManager instance { get; private set; }

    // Hole data
    int currentHole = 1;
    int maxPlayers = 6;

    Dictionary<string, PlayerDataManager> players = new Dictionary<string, PlayerDataManager>();

    void Awake() => instance = this;

    public void AddPlayer(string id)
    {
        if (!players.ContainsKey(id)) { 
            if (players.Count <= maxPlayers) {players[id] = new PlayerDataManager(); Debug.Log("Player Added to manager"); }
            else { Debug.LogWarning("Cannot add player. Maximum players reached."); }
        }
        else { Debug.LogWarning("Player with ID " + id + " already exists."); }
    }

    public void RemovePlayer(string id)
    {
        if (players.ContainsKey(id)) { players.Remove(id); }
        else { Debug.LogWarning("Player with ID " + id + " not found."); }
    }

    public void IncrementPlayerStrokes(string id)
    {
        if (players.ContainsKey(id)) { players[id].strokes++; }
        else { Debug.LogWarning("Player with ID " + id + " not found."); }
    }

    // Returns -1 when a player was not found
    public int GetPlayerStrokes(string id)
    {
        if (players.ContainsKey(id)) { return players[id].strokes; }
        else { Debug.LogWarning("Player with ID " + id + " not found."); return -1; }
    }
}
