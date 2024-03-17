using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.VisualScripting;


// Storing player data over the network ------------------------------------------------------------------------------------------------------------
public struct PlayerData : INetworkSerializable
{
    public Vector3 playerPos;
    public Quaternion playerRot;
    public bool isSwinging;
    public bool isCarrying;
    public int completedHoles;
    public int strokes;
    public int score;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerPos);
        serializer.SerializeValue(ref playerRot);
        serializer.SerializeValue(ref isSwinging);
        serializer.SerializeValue(ref isCarrying);
        serializer.SerializeValue(ref completedHoles);
        serializer.SerializeValue(ref strokes);
        serializer.SerializeValue(ref score);
    }
}

public struct PlayerParams : INetworkSerializable
{
    public Vector3 playerPos;
    public Quaternion playerRot;
    public bool isSwinging;
    public int strokes;
    //public bool isCarrying;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerPos);
        serializer.SerializeValue(ref playerRot);
        serializer.SerializeValue(ref isSwinging);
        serializer.SerializeValue(ref strokes);
        //serializer.SerializeValue(ref isCarrying);
    }
}

// ------------------------------------------------------------------------------------------------------------

public class PlayerNetworkData : NetworkBehaviour
{
    private PlayerData _currentPlayerData;
    private Dictionary<ulong, PlayerData> _players = new Dictionary<ulong, PlayerData>();

    // Local player data
    private NetworkVariable<PlayerData> _networkPlayerData = new NetworkVariable<PlayerData>(new PlayerData
    {
        playerPos = Vector3.zero,
        playerRot = Quaternion.identity,
        isCarrying = false,
        isSwinging = false,
        completedHoles = 0,
        strokes = 0,
        score = 0,
    }, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);


    // Update local variable when network variable updates  ------------------------------------------------------------------------------------------------------------

    public override void OnNetworkSpawn()
    {
        _networkPlayerData.OnValueChanged += OnPlayerDataChanged;
    }
    public override void OnDestroy()
    {
        _networkPlayerData.OnValueChanged -= OnPlayerDataChanged;
    }

    private void OnPlayerDataChanged(PlayerData prevData, PlayerData newData)
    {
        _currentPlayerData = newData;

        //Debug.LogWarning("OnPlayerDataChanged: Player data changed for " + OwnerClientId + " to " + _currentPlayerData.playerPos);
    }

    // public functions ------------------------------------------------------------------------------------------------------------

    // only owners should use this to send data to the server
    public void StorePlayerState(PlayerParams data, ulong senderID)
    {
        if (IsOwner)
        {
            PlayerData newData = new PlayerData()
            {
                playerPos = data.playerPos,
                playerRot = data.playerRot,
                isCarrying = false,
                isSwinging = data.isSwinging,
                completedHoles = _networkPlayerData.Value.completedHoles,
                strokes = data.strokes,
                score = _networkPlayerData.Value.score,
            };

            Debug.Log("Stroke in data: " + data.strokes);
            Debug.Log("Strokes in network data: " + newData.strokes);
            //Debug.Log("Storing player state for " + senderID + "\npos: " + data.playerPos + " rot: " + data.playerRot);
            StorePlayerStateServerRpc(newData, senderID);
            StoreToPlayerDictionary(newData, senderID);
        }
        else
        {
            _currentPlayerData = _networkPlayerData.Value;
            //Debug.LogWarning("Player data changed for " + OwnerClientId + " to " + _currentPlayerData.playerPos);
        }
    }

    public void IncrementStrokeCount(ulong senderID)
    {
        if (!IsOwner) return;
        IncrementStrokeCountServerRpc(senderID);
    }

    public void UpdateCompletedHoleCount(int holeCount, ulong senderID)
    {
        UpdateCompletedHoleCountServerRpc(holeCount, senderID);
    }

    // only non-owners should use this to get the latest player state
    public PlayerData GetPlayerState()
    {
        return _currentPlayerData;
    }

    public PlayerParams GetPlayerParams()
    {
        return new PlayerParams()
        {
            playerPos = _currentPlayerData.playerPos,
            playerRot = _currentPlayerData.playerRot,
            isSwinging = _currentPlayerData.isSwinging
        };
    }

    public void StoreToPlayerDictionary(PlayerData data, ulong senderID)
    {
        if (_players.ContainsKey(senderID)) { _players[senderID] = data; }
        else { _players.Add(senderID, data); }
    }

    // server rpcs ------------------------------------------------------------------------------------------------------------

    [ServerRpc]
    private void StorePlayerStateServerRpc(PlayerData data, ulong senderID)
    {
        //Debug.LogWarning("StorePlayerStateServerRpc:" + OwnerClientId + " is storing player state for " + senderID + "\npos: " + data.playerPos + " rot: " + data.playerRot);
        _networkPlayerData.Value = data;
    }

    [ServerRpc]
    public void IncrementStrokeCountServerRpc(ulong senderID)
    {
        PlayerData updatedData = new PlayerData()
        {
            playerPos = _networkPlayerData.Value.playerPos,
            playerRot = _networkPlayerData.Value.playerRot,
            isCarrying = _networkPlayerData.Value.isCarrying,
            isSwinging = _networkPlayerData.Value.isSwinging,
            completedHoles = _networkPlayerData.Value.completedHoles,
            strokes = _networkPlayerData.Value.strokes + 1,
            score = _networkPlayerData.Value.score
        };

        _networkPlayerData.Value = updatedData;

        Debug.Log("In Server RPC: " + _networkPlayerData.Value.strokes);
    }

    [ServerRpc]
    public void UpdateCompletedHoleCountServerRpc(int holeCount, ulong senderID)
    {
        PlayerData updatedData = new PlayerData()
        {
            playerPos = _networkPlayerData.Value.playerPos,
            playerRot = _networkPlayerData.Value.playerRot,
            isCarrying = _networkPlayerData.Value.isCarrying,
            isSwinging = _networkPlayerData.Value.isSwinging,
            completedHoles = holeCount,
            strokes = _networkPlayerData.Value.strokes,
            score = _networkPlayerData.Value.score
        };

        _networkPlayerData.Value = updatedData;
    }
}
