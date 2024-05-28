using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;

public class PlayerScoreboard : NetworkBehaviour
{
    // this dictionary is reserved for Game Manager use only - will be empty for clients
    private Dictionary<ulong, PlayerData> _scoreboardData = new Dictionary<ulong, PlayerData>();

    // This public function is reserved for Game Manager (incoming data) -----------------------------------------------------------------------------------------------------------------
    public void UpdateScoreboardData(Dictionary<ulong, PlayerData> dataDict)
    { // this function is called by the game manager to update the scoreboard data (this sets the _scoreboardData dictionary from the server side)
        ClearScoreboardClientRpc();
        _scoreboardData = dataDict;

        foreach (var data in _scoreboardData.Values)
        {
            UpdatePlayerDictionaryClientRpc(data);
        }
    }

    // RPCs to update the client side scoreboard data ----------------------------------------------------------------------------------------------------------------------------

    [ClientRpc]
    private void ClearScoreboardClientRpc()
    {
        if (!IsOwner) return;
        ScoreboardData.Clear();
    }

    [ClientRpc]
    private void UpdatePlayerDictionaryClientRpc(PlayerData data)
    { // this function is used to update all player owners with updated player data dictionary
        if (!IsOwner) return;

        if (ScoreboardData.ContainsKey(data.playerID)) ScoreboardData[data.playerID] = data;
        else ScoreboardData.Add(data.playerID, data);

        UpdateScoreboardUI();
    }

    // --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    //this is the dictionary is reserved for players(owners) - will be empty for non owners
    public static Dictionary<ulong, PlayerData> ScoreboardData = new Dictionary<ulong, PlayerData>();

    private void UpdateScoreboardUI()
    {
        // UI Manager can update scoreboard UI here (This will be called every time the scoreboard data is updated)
        //  - you can either pass the whole ScoreboardData dictionary to the UI Manager or use a foreach loop on the dictionary to pass individual player data

        // Debug.Log("Scoreboard data updated: player data entries: " + ScoreboardData.Count);
        // foreach (var data in ScoreboardData.Values)
        // {
        //     Debug.Log($"Player ID: {data.playerID} - Player Color: {data.playerColor} - Current Hole: {data.currentHole} - Strokes: {data.strokes} - Enemies Defeated: {data.enemiesDefeated} - Score: {data.score}");
        // }

    }

    // Public functions that player (owners) can access -------------------------------------------------------------------------------------------------------------------
    public Dictionary<ulong, PlayerData> GetScoreboardData()
    {
        if (!IsOwner) { Debug.LogWarning("You are not the owner of this player, you cannot access the data"); return null; }
        return ScoreboardData;
    }


}
