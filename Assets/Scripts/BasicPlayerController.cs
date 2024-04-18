using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;


// Needs: simple way to deactivate everything on game over / game exit, so players can play again without having to re-launch the game
public class BasicPlayerController : NetworkBehaviour
{
    // prefabs
    public GameObject spiderPrefab;

    // Movement
    public float moveSpeed = 2f;
    public float sprintMultiplier = 4f;
    public float rotationSpeed = 100f;
    private bool isSprinting = false;

    // Physics
    private Rigidbody _rb;
    private PlayerShoot _playerShoot;

    // State Management
    public PlayerData _currentPlayerState;
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

        // activate player controller - controller will activate the player movement, animations, shooting and ragdoll
        Activate();
    }


    // Update Loop -------------------------------------------------------------------------------------------------------------
    void Update()
    {
        if (!_isActive) return; //prevent updates until player is fully activated

        Animate();
        if (canMove)
        {
            Movement();
        }
    }


    // Activation -------------------------------------------------------------------------------------------------------------

    public void Activate()
    {
        Debug.Log("Activating player controller for " + OwnerClientId + " isOwner: " + IsOwner);
        _playerNetworkData = GetComponent<PlayerNetworkData>();

        if (!IsOwner) return;

        // activate player movement, animations, shooting and ragdoll
        _isActive = true;
        _playerShoot.Activate();


        //activate spider
        if (IsServer)
        {
            GameObject spider = Instantiate(spiderPrefab, new Vector3(391, 72.1f, 289), Quaternion.identity);
            spider.GetComponent<NetworkObject>().Spawn();
        }




        // activate flag poles
        foreach (GameObject flagPole in _flagPoles)
        {
            flagPole.GetComponent<HoleFlagPoleManager>().playerNetworkData = _playerNetworkData;
            flagPole.GetComponent<HoleFlagPoleManager>().playerController = this;
            flagPole.GetComponent<HoleFlagPoleManager>().uiManager = GameObject.Find("Canvas").GetComponent<UIManager>();
            flagPole.GetComponent<HoleFlagPoleManager>().Activate();
        }

        // set initial player state
        _currentPlayerState = new PlayerData
        {
            playerID = OwnerClientId,
            playerPos = transform.position,
            playerRot = transform.rotation,
            currentHole = 1,
            strokes = 0,
            enemiesDefeated = 0,
            score = 0

        };
        UpdatePlayerState(_currentPlayerState);
    }


    public void Deactivate()
    {
        _isActive = false;
        foreach (GameObject flagPole in _flagPoles)
        {
            flagPole.GetComponent<HoleFlagPoleManager>().Deactivate();
        }
        _ragdollOnOff.Deactivate();
        _playerShoot.Deactivate();

    }

    public override void OnDestroy()
    {
        Deactivate();
        base.OnDestroy();

    }


    // Movement -------------------------------------------------------------------------------------------------------------

    private void Movement()
    {
        if (!IsOwner)
        {
            if (!_isActive) return; //prevent updates until state manager is fully activated

            // update local player state with network data ?
        }

        // Check for pause input
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!UIManager.isPaused) { UIManager.isPaused = true; UIManager.instance.EnablePause(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            else { UIManager.isPaused = false; UIManager.instance.DisablePause(); Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        if (UIManager.isPaused) { return; }
        else { if (!UIManager.instance.titleScreenMode) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } }

        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        float rotationInput = Input.GetAxis("Mouse X");
        isSprinting = Input.GetKey(KeyCode.LeftShift) && moveVertical > 0; //sprinting only allowed when moving forward

        PlayerMovement(moveHorizontal, moveVertical, rotationInput);

    }

    private void PlayerMovement(float moveHorizontal, float moveVertical, float rotationInput)
    {

        float splayerSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical);
        movement = movement.normalized * splayerSpeed * Time.deltaTime;

        _rb.MovePosition(transform.position + transform.TransformDirection(movement));

        float rotationAmount = rotationInput * rotationSpeed * Time.deltaTime;
        Quaternion deltaRotation = Quaternion.Euler(0f, rotationAmount, 0f);
        _rb.MoveRotation(_rb.rotation * deltaRotation);

        if (!_isActive) return; //prevent updates to state manager until player is fully activated

        // Update player state if position or rotation has changed
        if (transform.position != _currentPlayerState.playerPos || transform.rotation != _currentPlayerState.playerRot)
        {
            _currentPlayerState = new PlayerData
            {
                playerID = OwnerClientId,
                playerPos = transform.position,
                playerRot = transform.rotation,
                currentHole = _currentPlayerState.currentHole,
                strokes = _currentPlayerState.strokes,
                enemiesDefeated = _currentPlayerState.enemiesDefeated,
                score = _currentPlayerState.score
            };

            //Debug.Log("BasicPlayerController: sending to PlayerNetworkData.cs\nOwner: " + OwnerClientId + "\nstrokes: " + _currentPlayerState.strokes + "\nhole: " + _currentPlayerState.currentHole + "\npos: " + _currentPlayerState.playerPos);
            UpdatePlayerState(_currentPlayerState);
        }
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
                if (!strikePressed)
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


    // Input Management -------------------------------------------------------------------------------------------------------------

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

    // State Management -------------------------------------------------------------------------------------------------------------
    public void UpdatePlayerState(PlayerData playerState)
    {
        if (!IsOwner) return;
        _playerNetworkData.StorePlayerState(playerState);
    }
}








