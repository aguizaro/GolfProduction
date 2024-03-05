using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;


public class BasicPlayerController : NetworkBehaviour
{

    public float moveSpeed = 12.0f;
    //public float turnSpeed = 70.0f;
    //public float jumpSpeed = 8.0f;
    //public float gravity = 20.0f;
    //public float groundRaycastDistance = 1.1f; // Adjust this value based on your character's height

    private bool isActive = true;

    //private Vector3 moveDirection = Vector3.zero;
    //public CharacterController controller;
    //private float verticalSpeed = 0f;

    // VERY BASIC CLIENT AUTHORATIATIVE MOVE
    // THE NETWORK OBJECT ATTACHED TO THIS COMPONENT MUST OVERRIDE BASE NETWORKTRANSOFRM OR USE A CLIENTNETWORKTRANSFORM

    // each instance of gameobject has their own _playerData network variable
    private NetworkVariable<PlayerData> _playerData = new NetworkVariable<PlayerData>(new PlayerData
    {
        playerPos = Vector3.zero,
        playerRot = Quaternion.identity,
        isCarrying = false,
        isSwinging = false,
        score = 0
    }, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    // ------------------------------------------------------------------------------------------------------------

    public float rotationSpeed = 1000f;

    private PlayerNetworkData _playerNetworkData;
    private Rigidbody rb;


    public override void OnNetworkSpawn()
    {
        Debug.Log("OnNetworkSpawn() called");

        if (!IsOwner)
        {
            isActive = false;
            return;
        }
        // Lock and hide cursor only for the local player

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // grab player's rigidbody component on spawn
        rb = gameObject.GetComponent<Rigidbody>();

        _playerNetworkData = GameObject.FindWithTag("StateManager").GetComponent<PlayerNetworkData>();
        transform.position = new Vector3(Random.Range(311, 317), 103.163002f, Random.Range(85, 96)); //set starting position
        Debug.Log("Client: " + OwnerClientId + " starting position" + transform.position);

        _playerData.Value = new PlayerData
        {
            playerPos = transform.position,
            playerRot = transform.rotation,
            isCarrying = false, // check for this later
            isSwinging = false,
            score = 0
        };
        _playerNetworkData.StorePlayerState(_playerData.Value, OwnerClientId);
    }
    //only here for testing
    /*private void Start()
    {
        _playerNetworkData = GameObject.FindWithTag("StateManager").GetComponent<PlayerNetworkData>();
        transform.position = new Vector3(Random.Range(311, 317), 103.163002f, Random.Range(85, 96)); //set starting position
        Debug.Log("starting pos: " + transform.position);
    }*/

    void Update()
    {
        if (!isActive) return;

        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        float rotationInput = Input.GetAxis("Mouse X");

        if (IsOwner) // owner move self and set data
        {
            PlayerMovement(moveHorizontal, moveVertical, rotationInput);
        }
        else
        {
            PlayerData ownerState = _playerNetworkData.GetPlayerState(OwnerClientId);
            transform.SetPositionAndRotation(ownerState.playerPos, ownerState.playerRot);
        }
    }

    /*void PlayerMovement(float horizontalInput, float verticalInput, float mouseX)
    {
        moveDirection = new Vector3(horizontalInput, 0.0f, verticalInput);
        moveDirection = transform.TransformDirection(moveDirection);
        moveDirection *= moveSpeed;

        // Apply gravity
        if (controller.isGrounded)
        {
            verticalSpeed = 0f;
        }
        else
        {
            verticalSpeed -= gravity * Time.deltaTime;
        }

        // Jumping
        if (controller.isGrounded && Input.GetButton("Jump"))
        {
            verticalSpeed = jumpSpeed;
        }

        // Move the player along the ground
        moveDirection.y = verticalSpeed;

        // Perform ground raycast to adjust player position
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, groundRaycastDistance))
        {
            float targetHeight = hit.point.y + controller.height / 2f;
            if (targetHeight > transform.position.y)
            {
                transform.position = new Vector3(transform.position.x, targetHeight, transform.position.z);
            }
        }

        // Move the controller
        controller.Move(moveDirection * Time.deltaTime);
        // Rotate the player horizontally
        transform.Rotate(0, mouseX, 0);

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
    }*/

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






