using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;

public class SpawnManager : NetworkBehaviour
{
    [SerializeField] private GameObject _stateManagerPrefab;
    private BasicPlayerController _playerController;
    private GameObject _stateManager;
    //private PlayerNetworkData _playerNetworkData;
    private bool _isGameActive = false;
    private bool _calledActivate = false;

    public override void OnNetworkSpawn()
    {
        _playerController = GetComponent<BasicPlayerController>();
        if (IsOwner)
        {
            ActivateServerRpc(OwnerClientId);
            _isGameActive = true;
        }
    }

    public override void OnDestroy()
    {
    }

    private void SpawnGameObjects(ulong clientID)
    {
        _stateManager = Instantiate(_stateManagerPrefab);
        _stateManager.GetComponent<NetworkObject>().SpawnWithOwnership(clientID);

    }


    void Update()
    {

        if (_isGameActive)
        {
            if (!_calledActivate && IsOwner)
            {
                //Debug.Log("Activate playercontrol for " + OwnerClientId + " isOwner: " + IsOwner + " isServer: " + IsServer + " isLocalPlayer: " + IsLocalPlayer);
                _playerController.Activate(); //activate respective player controller
                _calledActivate = true;
            }

            return;
        }



    }

    [ClientRpc]
    public void ActivateClientRpc()
    {
        //Debug.Log("ClientRpc called for " + OwnerClientId + " isOwner: " + IsOwner + " isServer: " + IsServer + " isLocalPlayer: " + IsLocalPlayer);
        _isGameActive = true;
    }

    [ServerRpc]
    public void ActivateServerRpc(ulong clientID)
    {
        //Debug.Log("ServerRpc called for " + OwnerClientId + " isOwner: " + IsOwner + " isServer: " + IsServer + " isLocalPlayer: " + IsLocalPlayer);
        SpawnGameObjects(clientID);
        ActivateClientRpc();
    }
}
