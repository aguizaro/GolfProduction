using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class HoleFlagPoleManager : NetworkBehaviour
{
    private Collider holeTrigger;
    private PlayerNetworkData _playerNetworkData;
    private bool isActive = false;

    private void Start()
    {
        holeTrigger = GetComponent<Collider>();
    }

    public void Activate()
    {
        _playerNetworkData = GameObject.FindWithTag("StateManager").GetComponent<PlayerNetworkData>();
        isActive = true;

        Debug.Log("HoleFlagPoleManager activated for " + OwnerClientId + " isOwner: " + IsOwner);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return; //prevent updates until player is fully activated

        if (other.CompareTag("Ball"))
        {
            //if (IsServer) 
            if (IsOwner)
            {
                PlayerData oldData = _playerNetworkData.GetPlayerState();
                oldData.completedHoles++;

                ulong playerID = other.gameObject.GetComponent<NetworkObject>().OwnerClientId;
                _playerNetworkData.UpdateCompletedHoleCount(oldData.completedHoles, playerID);

                other.gameObject.GetComponent<NetworkObject>().Despawn(); // Despawn golf ball
            }
        }
    }
}
