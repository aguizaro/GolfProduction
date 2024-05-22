using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerScoreboard : NetworkBehaviour
{
    private Dictionary<ulong, PlayerData> _scoreboardData = new Dictionary<ulong, PlayerData>();


    public void AddScoreboardData(Dictionary<ulong, PlayerData> dataDict)
    {
        _scoreboardData = dataDict;

        Debug.Log($"Scoreboard data updated: {_scoreboardData.Count} players in the scoreboard - local player: {NetworkManager.Singleton.LocalClientId}");
        foreach (var data in _scoreboardData.Values)
        {
            Debug.Log($"Found Player: {data.playerID} - {data.playerColor} - {data.currentHole} - {data.strokes} - {data.score} - {data.enemiesDefeated}");
        }
    }
}
