using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class HoleFlagPoleManager : NetworkBehaviour
{
    private Collider holeTrigger;
    private PlayerNetworkData _playerNetworkData;

    private void Start()
    {
        holeTrigger = GetComponent<Collider>();
    }

    public override void OnNetworkSpawn()
    {
        _playerNetworkData = GameObject.FindWithTag("StateManager").GetComponent<PlayerNetworkData>();
    }

    private void OnTriggerEnter(Collider other)
    {
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
