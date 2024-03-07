using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine.PlayerLoop;


public class BasicPlayerController : NetworkBehaviour
{

    public float moveSpeed = 12.0f;
    private PlayerData _startState;

    private PlayerData _currentPlayerState;

    // each instance of gameobject has their own _playerData network variable
    /**private NetworkVariable<PlayerData> _playerData = new NetworkVariable<PlayerData>(new PlayerData
    {
        playerPos = Vector3.zero,
        playerRot = Quaternion.identity,
        isCarrying = false,
        isSwinging = false,
        score = 0,
    }, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);*/
    // ------------------------------------------------------------------------------------------------------------

    public float rotationSpeed = 1000f;

    private PlayerNetworkData _playerNetworkData;
    private Rigidbody rb;


    public override void OnNetworkSpawn()
    {
        _playerNetworkData = GameObject.FindWithTag("StateManager").GetComponent<PlayerNetworkData>();
        rb = gameObject.GetComponent<Rigidbody>(); // grab player's rigidbody component on spawn

        if (!IsOwner)
        {
            return;
        }

        // Lock and hide cursor only for the local player
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        transform.position = new Vector3(Random.Range(390, 400), 69.1f, Random.Range(318, 320)); //set starting random place near first hole
        Debug.Log("Client: " + OwnerClientId + " starting position" + transform.position);

        _currentPlayerState = new PlayerData
        {
            playerPos = transform.position,
            playerRot = transform.rotation,
            isCarrying = false, // check for this later
            isSwinging = false,
            score = 0,
        };

        _playerNetworkData.StorePlayerState(_currentPlayerState, OwnerClientId);
        _startState = _currentPlayerState;
    }

    void Update()
    {
        if (!IsOwner)
        {
            if (_playerNetworkData == null) // redundant check due to OnNetworkSpawn not reliably setting _playerNetworkData
            {
                _playerNetworkData = GameObject.FindWithTag("StateManager").GetComponent<PlayerNetworkData>();
            }
            Debug.LogWarning("Non owner: " + OwnerClientId + " reading player state: " + _currentPlayerState.playerPos + " rot: " + _currentPlayerState.playerRot);
            _currentPlayerState = _playerNetworkData.GetPlayerState();
            return; //only owners can update player state
        }

        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        float rotationInput = Input.GetAxis("Mouse X");

        PlayerMovement(moveHorizontal, moveVertical, rotationInput);

        //check if player has fallen through terrain - reset to start state
        //if (transform.position.y <= )
    }

    private void PlayerMovement(float moveHorizontal, float moveVertical, float rotationInput)
    {

        Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical);
        movement = movement.normalized * moveSpeed * Time.deltaTime;

        rb.MovePosition(transform.position + transform.TransformDirection(movement));

        float rotationAmount = rotationInput * rotationSpeed * Time.deltaTime;
        Quaternion deltaRotation = Quaternion.Euler(0f, rotationAmount, 0f);
        rb.MoveRotation(rb.rotation * deltaRotation);

        // current state of this player (owner)
        _currentPlayerState = new PlayerData
        {
            playerPos = transform.position,
            playerRot = transform.rotation,
            isCarrying = false, // check for this later
            isSwinging = false,
            score = 0,
        };

        Debug.LogWarning("In BasicPlayerController.cs sending to PlayerNetworkData.cs\nOwner: " + OwnerClientId + "\npos: " + _currentPlayerState.playerPos + " rot: " + _currentPlayerState.playerRot);
        _playerNetworkData.StorePlayerState(_currentPlayerState, OwnerClientId);
    }

    // Reset player state to starting state
    public void ResetPlayerState()
    {
        transform.SetPositionAndRotation(_startState.playerPos, _startState.playerRot);

        _currentPlayerState = _startState;
        _playerNetworkData.StorePlayerState(_currentPlayerState, OwnerClientId);
    }


}








