using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;


// Storing player data over the network ------------------------------------------------------------------------------------------------------------
public struct PlayerData : INetworkSerializable
{
    public ulong playerID;
    public Vector3 playerPos;
    public Quaternion playerRot;
    public int currentHole;
    public int strokes;
    public ulong enemiesDefeated;
    public int score;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerID);
        serializer.SerializeValue(ref playerPos);
        serializer.SerializeValue(ref playerRot);
        serializer.SerializeValue(ref currentHole);
        serializer.SerializeValue(ref strokes);
        serializer.SerializeValue(ref enemiesDefeated);
        serializer.SerializeValue(ref score);
    }

}

// ------------------------------------------------------------------------------------------------------------

public class PlayerNetworkData : NetworkBehaviour
{
    private PlayerData _currentPlayerData;

    private NetworkVariable<PlayerData> _networkPlayerData = new NetworkVariable<PlayerData>(new PlayerData(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


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
        Debug.Log("OnPlayerDataChanged: called by owner: " + OwnerClientId + " isOwner: " + IsOwner + " isServer: " + IsServer + "\nfor player: " + newData.playerID + " - strokes: " + _currentPlayerData.strokes + " hole: " + _currentPlayerData.currentHole + " pos:" + _currentPlayerData.playerPos);
        _currentPlayerData = newData;

        if (IsOwner)
        {
            if (prevData.currentHole != newData.currentHole) // check for current hole change
            {
                // check player data for win - or moves ball to next hole
                GetComponent<PlayerShoot>().CheckForWin(newData);
            }
        }

    }

    // public functions ------------------------------------------------------------------------------------------------------------

    // only owners should use this to send data to the server
    public void StorePlayerState(PlayerData data) //senderID will be used later
    {
        if (IsOwner)
        {
            PlayerData newData = new PlayerData()
            {
                playerID = data.playerID,
                playerPos = data.playerPos,
                playerRot = data.playerRot,
                currentHole = data.currentHole,
                strokes = data.strokes,
                enemiesDefeated = data.enemiesDefeated,
                score = data.score
            };

            // send data to server
            StorePlayerStateServerRpc(newData);
        }
        else
        {
            _currentPlayerData = _networkPlayerData.Value;
        }
    }


    // server rpcs ------------------------------------------------------------------------------------------------------------

    [ServerRpc]
    private void StorePlayerStateServerRpc(PlayerData data)
    {
        _networkPlayerData.Value = data;
    }
}
