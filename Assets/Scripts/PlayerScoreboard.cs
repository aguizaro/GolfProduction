using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;

public class PlayerScoreboard : NetworkBehaviour
{
    // this dictionary is updated by the game manager (meaning you need a serverRPC to access the data, kinda sucks for now but i will try to find a better way to do this)
    private Dictionary<ulong, PlayerData> _scoreboardData = new Dictionary<ulong, PlayerData>();

    // --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    //this is the dictionary that players(owners) can access to get the current scoreboard data
    private Dictionary<ulong, PlayerData> ScoreboardData = new Dictionary<ulong, PlayerData>();


    // This public function is reserved for Game Manager (incoming data) -----------------------------------------------------------------------------------------------------------------
    public void UpdateScoreboardData(Dictionary<ulong, PlayerData> dataDict)
    { // this function is called by the game manager to update the scoreboard data (this sets the _scoreboardData dictionary from the server side)
        ClearScoreboardClientRpc();
        _scoreboardData = dataDict;

        Debug.Log($"Scoreboard data updated with {dataDict.Count} players");

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

        if (ScoreboardData.ContainsKey(data.playerID))
        {
            ScoreboardData[data.playerID] = data;
            //Debug.Log($"client: {NetworkManager.Singleton.LocalClientId} - Scoreboard data updated for player: {data.playerID} - {data.playerColor} - {data.currentHole} - {data.strokes} - {data.score} - {data.enemiesDefeated}");
        }
        else
        {
            ScoreboardData.Add(data.playerID, data);
            //Debug.Log($"client: {NetworkManager.Singleton.LocalClientId} - Scoreboard data created for player: {data.playerID} - {data.playerColor} - {data.currentHole} - {data.strokes} - {data.score} - {data.enemiesDefeated}");
        }

        // call event to update scoreboard UI
        UpdateScoreboardUI();
    }

    private void UpdateScoreboardUI()
    {
        // call event to update scoreboard UI here
        //UIManager.instance.UpdateScoreboardUI(ScoreboardData);

        Debug.Log($"client: {NetworkManager.Singleton.LocalClientId} - has {ScoreboardData.Count} players on scoreboard");

        foreach (var player in ScoreboardData.Values)
        {
            Debug.Log($"client: {NetworkManager.Singleton.LocalClientId} - contains scoreboard data for player: {player.playerID} - {player.playerColor} - {player.currentHole} - {player.strokes} - {player.score} - {player.enemiesDefeated}");
        }


    }

    // Public functions that player (owners) can access -------------------------------------------------------------------------------------------------------------------
    public Dictionary<ulong, PlayerData> GetScoreboardData()
    {
        if (!IsOwner) { Debug.Log("You are not the owner of this player, you cannot access this data"); return null; }
        return ScoreboardData;
    }


}
