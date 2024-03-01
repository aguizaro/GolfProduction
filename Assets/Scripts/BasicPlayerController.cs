using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Threading.Tasks;
using System;

public class BasicPlayerController : NetworkBehaviour
{

    // VERY BASIC CLIENT AUTHORATIATIVE MOVE
    // THE NETWORK OBJECT ATTACHED TO THIS COMPONENT MUST OVERRIDE BASE NETWORKTRANSOFRM OR USE A CLIENTNETWORKTRANSFORM


    // Storing player data over the network ------------------------------------------------------------------------------------------------------------
    // each instance of gameobject has their own _playerData network variable
    private NetworkVariable<PlayerData> _playerData = new NetworkVariable<PlayerData>(new PlayerData
    {
        playerPos = Vector3.zero,
        playerRot = Quaternion.identity,
        isCarrying = false,
        isSwinging = false,
        score = 0
    }, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

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

    public float moveSpeed = 13.0f;
    public float turnSpeed = 70.0f;
    public float rotationSpeed = 1000f;

    private PlayerNetworkData _playerNetworkData;
    private Rigidbody rb;


    public override void OnNetworkSpawn()
    {
        // Lock and hide cursor only for the local player
        if (IsLocalPlayer)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // grab player's rigidbody component on spawn
        rb = gameObject.GetComponent<Rigidbody>();

        _playerNetworkData = GameObject.FindWithTag("StateManager").GetComponent<PlayerNetworkData>();
    }


    void Update()
    {
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        float rotationInput = Input.GetAxis("Mouse X");

        if (IsOwner) // owner move self and set data
        {
            PlayerMovement(moveHorizontal, moveVertical, rotationInput);
        }
        else
        {
            Debug.Log("non-owner: " + OwnerClientId + " asking server for owner's state");

            PlayerData ownerState = _playerNetworkData.GetPlayerState(OwnerClientId);
            transform.SetPositionAndRotation(ownerState.playerPos, ownerState.playerRot);

            Debug.Log("non-owner: " + OwnerClientId + " recieved:\nPos: " + transform.position + "Rot: " + transform.rotation.eulerAngles);
        }
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
        _playerData.Value = new PlayerData
        {
            playerPos = transform.position,
            playerRot = transform.rotation,
            isCarrying = false, // check for this later
            isSwinging = false,
            score = 0
        };

        //send current owner's state to the server
        _playerNetworkData.StorePlayerState(_playerData.Value, OwnerClientId);

    }



}

