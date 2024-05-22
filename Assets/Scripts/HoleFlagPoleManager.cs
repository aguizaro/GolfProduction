using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


// HoleFlagPoleManager class manages the flag poles at each hole and handles player scoring
// this class is networked and is owned by the server - because of this. Any logic that needs to be ran on clients requires a ClientRpc - onTriggerEnter is only executed on the server

// Still needs: reset flag pole and deactivate on game over / game exit, so players can play again without having to re-launch the game
public class HoleFlagPoleManager : NetworkBehaviour
{
    //these are set by the player controller on activation
    public PlayerNetworkData playerNetworkData;
    public UIManager uiManager;

    private bool isActive = false;

    private List<ulong> _playerIDs = new List<ulong>();


    public void Activate()
    {
        isActive = true;
        //Debug.Log("HoleFlagPoleManager activated for " + OwnerClientId + " isOwner: " + IsOwner + "\n" + "PlayerController: " + playerNetworkData.OwnerClientId + "\n" + "PlayerNetworkData: " + playerNetworkData.OwnerClientId + "\n" + "UIManager: " + uiManager);
    }
    public void Deactivate()
    {
        isActive = false;
        ResetFlagPoles();
    }

    public void OnTriggerEnter(Collider other)
    {
        if (!isActive || !IsOwner) return; //prevent updates until player is fully activated

        if (other.CompareTag("Ball"))
        {
            ulong playerID = other.gameObject.GetComponent<NetworkObject>().OwnerClientId;

            if (!_playerIDs.Contains(playerID))
            {
                _playerIDs.Add(playerID);
                HandlePlayerScoreClientRpc(playerID);
            }
        }
    }

    public void ResetFlagPoles()
    {
        _playerIDs.Clear();
    }

    [ClientRpc]
    private void HandlePlayerScoreClientRpc(ulong playerID)
    {
        if (NetworkManager.Singleton.LocalClientId != playerID) return; //only the player that scored should handle this

        PlayerData currentPlayerData = playerNetworkData.GetPlayerData();
        currentPlayerData.currentHole++;
        playerNetworkData.StorePlayerState(currentPlayerData);

        //maybe we can check for win here since we have a reference to the player network data - currently being done in PlayerNetworkData

    }
}
