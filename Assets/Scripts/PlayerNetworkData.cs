using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using static BasicPlayerController;
using Unity.Netcode.Components;

public class PlayerNetworkData : NetworkBehaviour
{

    //this will be a dictionary of player states for each player
    // the key is the OwnerClientID of the player
    //  if this instance is not the server, this dictionary will be empty and isActive false
    Dictionary<ulong, PlayerData> serverSidePlayerStates = new();
    private bool isActive = false;


    public override void OnNetworkSpawn()
    {
        if (IsServer) { isActive = true; }
    }

    // only owners should use this to send data to the server
    public void StorePlayerState(PlayerData data, ulong senderID)
    {
        if (!isActive) return;

        //check if this position is legal - not done yet

        //store player data
        if (serverSidePlayerStates.TryGetValue(senderID, out PlayerData senderData))
        {
            serverSidePlayerStates[senderID] = data; // update player state for the client that sent this data if it exists
        }
        else
        {
            serverSidePlayerStates.Add(senderID, data); //create new entry for this player otherwise
        }
    }


    // non-owner instances of a player should call this to get the owner's player state
    public PlayerData GetPlayerState(ulong ownerclientID)
    {
        //if the non-server runs this 

        // return player state if found in dict, otherwise return default player state
        return (serverSidePlayerStates.TryGetValue(ownerclientID, out PlayerData data)) ? data : new PlayerData
        {
            playerPos = Vector3.zero,
            playerRot = Quaternion.identity,
            isCarrying = false,
            isSwinging = false,
            score = 0
        };
    }
}
