using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine.PlayerLoop;
using UnityEngine.Video;
using Unity.Services.Lobbies.Models;

public class BasicPlayerController : NetworkBehaviour
{
    // Movement
    public float moveSpeed = 2f;
    public float sprintMultiplier = 4f;
    public float rotationSpeed = 100f;
    private bool isSprinting = false;
    private float currentSpeed;

    // Physics
    private Rigidbody _rb;
    private PlayerShoot _playerShoot;

    // State Management
    private PlayerParams _startState;
    public PlayerParams _currentPlayerState;
    private PlayerNetworkData _playerNetworkData;
    private RagdollOnOff _ragdollOnOff;
    private bool canMove = true;

    // Animation
    private Animator _animator;
    private GameObject[] _flagPoles;

    // Activation
    private bool _isActive = false;

    public override void OnNetworkSpawn()
    {
        _rb = gameObject.GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _playerShoot = GetComponent<PlayerShoot>();
        _ragdollOnOff = GetComponent<RagdollOnOff>();
        _flagPoles = GameObject.FindGameObjectsWithTag("HoleFlagPole");

        _ragdollOnOff.Activate(); // activate ragdoll

        if (!IsOwner) return;

        transform.position = new Vector3(Random.Range(390, 400), 69.1f, Random.Range(318, 320));
    }


    // Update Loop -------------------------------------------------------------------------------------------------------------
    void FixedUpdate()
    {
        //Debug.Log("player controller: pos: " + transform.position + " rot: " + transform.rotation.eulerAngles + " owner: " + OwnerClientId);

        if (_isActive) Debug.Log("Player controller is active for " + OwnerClientId + " isOwner: " + IsOwner);
        Animate();
        if(canMove)
        {
            Movement();
        }
    }


    // Activation -------------------------------------------------------------------------------------------------------------

    public void Activate()
    {
        Debug.Log("Activating player controller for " + OwnerClientId + " isOwner: " + IsOwner);

        _playerNetworkData = GameObject.FindWithTag("StateManager").GetComponent<PlayerNetworkData>();

        if (!IsOwner) return;

        //transform.position = new Vector3(Random.Range(390, 400), 69.1f, Random.Range(318, 320)); //set starting random place near first hole
        Debug.Log("Client: " + OwnerClientId + " starting position" + transform.position);

        _currentPlayerState = new PlayerParams
        {
            playerPos = transform.position,
            playerRot = transform.rotation,
            //isCarrying = false, // check for this later
            isSwinging = false,
        };

        _playerNetworkData.StorePlayerState(_currentPlayerState, OwnerClientId);
        _startState = _currentPlayerState;

        // activate player movement, animations, shooting and ragdoll
        _isActive = true;
        //_playerShoot.Activate(); // activate shooting
        _playerShoot.Activate(); // activate shooting on client

        //_playerShoot.SpawnProjectile(OwnerClientId); // Immediate spawn ball

        // activate flag poles
        foreach (GameObject flagPole in _flagPoles)
        {
            flagPole.GetComponent<HoleFlagPoleManager>().Activate();
        }

    }

    public void Deactivate()
    {
        _isActive = false;
    }

    // Movement -------------------------------------------------------------------------------------------------------------

    private void Movement()
    {
        if (!IsOwner)
        {
            if (!_isActive) return; //prevent updates until state manager is fully activated

            if (_playerNetworkData == null) // redundant check due to OnNetworkSpawn not reliably setting _playerNetworkData
            {
                _playerNetworkData = GameObject.FindWithTag("StateManager").GetComponent<PlayerNetworkData>();
            }
            Debug.LogWarning("Non owner: " + OwnerClientId + " reading player state: " + _currentPlayerState.playerPos + " rot: " + _currentPlayerState.playerRot);
            _currentPlayerState = _playerNetworkData.GetPlayerParams();
            return; //only owners can update player state
        }

        // Check for pause input
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!UIManager.isPaused) { UIManager.isPaused = true; UIManager.instance.EnablePause(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            else { UIManager.isPaused = false; UIManager.instance.DisablePause(); Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        if (UIManager.isPaused) { return; }
        else { if (!UIManager.instance.titleScreenMode) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } }

        //if (_ragdollActive) return; //prevent movement while ragdoll is active

        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        float rotationInput = Input.GetAxis("Mouse X");
        isSprinting = Input.GetKey(KeyCode.LeftShift) && moveVertical > 0; //sprinting only allowed when moving forward

        PlayerMovement(moveHorizontal, moveVertical, rotationInput);

        //check if player has fallen through terrain - reset to start state
        //if (transform.position.y <= )
    }

    private void PlayerMovement(float moveHorizontal, float moveVertical, float rotationInput)
    {

        float targetSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        // Smoothly interpolate between current speed and target speed
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, 5f * Time.deltaTime);

        // Calculate movement direction
        Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical).normalized;

        movement *= currentSpeed * Time.deltaTime;

        _rb.MovePosition(transform.position + transform.TransformDirection(movement));

        float rotationAmount = rotationInput * rotationSpeed * Time.deltaTime;
        Quaternion deltaRotation = Quaternion.Euler(0f, rotationAmount, 0f);
        _rb.MoveRotation(_rb.rotation * deltaRotation);

        if (!_isActive) return; //prevent updates to state manager until player is fully activated

        // current state of this player (owner)
        _currentPlayerState = new PlayerParams
        {
            playerPos = transform.position,
            playerRot = transform.rotation,
            //isCarrying = false, // check for this later
            isSwinging = false,
            strokes = _currentPlayerState.strokes,
        };

        //Debug.LogWarning("In BasicPlayerController.cs sending to PlayerNetworkData.cs\nOwner: " + OwnerClientId + "\npos: " + _currentPlayerState.playerPos + " rot: " + _currentPlayerState.playerRot);
        _playerNetworkData.StorePlayerState(_currentPlayerState, OwnerClientId);
    }

    // Animation -------------------------------------------------------------------------------------------------------------

    void Animate()
    // few issues with the animator -> cant transition from running to swinging and sometimes swings dont register if anotther animation is playing
    // might need some tweaks to the animator FSM
    {
        bool isrunning = _animator.GetBool("isRunning");
        bool isStrafingLeft = _animator.GetBool("isLeft");
        bool isStrafingRight = _animator.GetBool("isRight");
        bool isWalking = _animator.GetBool("isWalking");
        bool isReversing = _animator.GetBool("isReversing");
        bool isStriking = _animator.GetBool("isStriking");
        bool forwardPressed = Input.GetKey("w") || Input.GetKey("up");
        bool runPressed = Input.GetKey("left shift") || Input.GetKey("right shift");
        bool backPressed = Input.GetKey("s") || Input.GetKey("down");
        bool rightPressed = Input.GetKey("d") || Input.GetKey("right");
        bool leftPressed = Input.GetKey("a") || Input.GetKey("left");
        bool strikePressed = Input.GetKeyDown("e");

        if (UIManager.isPaused) { return; }

        if (IsOwner)
        {
            if (forwardPressed && !isWalking)
            {
                _animator.SetBool("isWalking", true);
            }
            if (!forwardPressed && isWalking)
            {
                _animator.SetBool("isWalking", false);
            }

            if (backPressed && !isReversing)
            {
                _animator.SetBool("isReversing", true);
            }
            if (!backPressed && isReversing)
            {
                _animator.SetBool("isReversing", false);
            }

            if (!isrunning && (forwardPressed && runPressed) && !isStriking)
            {
                _animator.SetBool("isRunning", true);
            }
            if (isrunning && (!runPressed || !forwardPressed))
            {
                _animator.SetBool("isRunning", false);
            }

            if (leftPressed && !isStrafingLeft)
            {
                _animator.SetBool("isLeft", true);
            }
            if (!leftPressed && isStrafingLeft)
            {
                _animator.SetBool("isLeft", false);
            }

            if (rightPressed && !isStrafingRight)
            {
                _animator.SetBool("isRight", true);
            }
            if (!rightPressed && isStrafingRight)
            {
                _animator.SetBool("isRight", false);
            }

            if (strikePressed && !isStriking)
            {
                _animator.SetBool("isStriking", true);
                _animator.SetBool("justStriked", true);
                DisableInput();
            }
            
            if (isStriking)
            {
                if(!strikePressed)
                {
                    _animator.SetBool("justStriked", false);
                }
                if (_animator.GetCurrentAnimatorStateInfo(0).IsName("Strike") && !_animator.IsInTransition(0))
                {
                    // Check if the current animation is the "Strike" animation and not in transition
                    if (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.7f)
                    {
                        // The strike animation has finished playing
                        _animator.SetBool("isStriking", false);
                        EnableInput();
                    }
                }
                else if (_animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                {
                    // If the current state is "Idle", reset isStriking and enable input
                    _animator.SetBool("isStriking", false);
                    EnableInput();
                }
            }
        }
    }


    // State Management -------------------------------------------------------------------------------------------------------------

    // Reset player state to starting state
    public void ResetPlayerState()
    {
        transform.SetPositionAndRotation(_startState.playerPos, _startState.playerRot);

        _currentPlayerState = _startState;
        _playerNetworkData.StorePlayerState(_currentPlayerState, OwnerClientId);
    }

    public void DisableInput()
    {
        _animator.SetBool("isWalking", false);
        _animator.SetBool("isRunning", false);
        _animator.SetBool("isLeft", false);
        _animator.SetBool("isRight", false);
        _animator.SetBool("isReversing", false);
        //_animator.SetBool("isStriking", false);

        canMove = false;
    }

    public void EnableInput()
    {
        canMove = true;
    }
}








