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
    public int score;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerPos);
        serializer.SerializeValue(ref playerRot);
        serializer.SerializeValue(ref isSwinging);
        serializer.SerializeValue(ref isCarrying);
        serializer.SerializeValue(ref score);
    }
}
// ------------------------------------------------------------------------------------------------------------

public class PlayerNetworkData : NetworkBehaviour
{
    private PlayerData _currentPlayerData;

    // Local player data
    private NetworkVariable<PlayerData> _networkPlayerData = new NetworkVariable<PlayerData>(new PlayerData
    {
        playerPos = Vector3.zero,
        playerRot = Quaternion.identity,
        isCarrying = false,
        isSwinging = false,
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
    public void StorePlayerState(PlayerData data, ulong senderID)
    {
        if (IsOwner)
        {
            //Debug.Log("Storing player state for " + senderID + "\npos: " + data.playerPos + " rot: " + data.playerRot);
            StorePlayerStateServerRpc(data, senderID);
        }
        else
        {
            _currentPlayerData = _networkPlayerData.Value;
            //Debug.LogWarning("Player data changed for " + OwnerClientId + " to " + _currentPlayerData.playerPos);
        }
    }
    // only non-owners should use this to get the latest player state
    public PlayerData GetPlayerState()
    {
        return _currentPlayerData;
    }

    // server rpcs ------------------------------------------------------------------------------------------------------------

    [ServerRpc]
    private void StorePlayerStateServerRpc(PlayerData data, ulong senderID)
    {
        //Debug.LogWarning("StorePlayerStateServerRpc:" + OwnerClientId + " is storing player state for " + senderID + "\npos: " + data.playerPos + " rot: " + data.playerRot);
        _networkPlayerData.Value = data;
    }
}
