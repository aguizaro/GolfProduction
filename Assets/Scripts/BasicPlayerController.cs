using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

// Needs: simple way to deactivate everything on game over / game exit, so players can play again without having to re-launch the game
public class BasicPlayerController : NetworkBehaviour
{
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
    private Actions _actions;

    // #if ENABLE_INPUT_SYSTEM
    public const float inputThreshold = 0.001f;
    public Vector2 moveInput;
    public Vector2 lookInput;
    public bool forwardPressed;
    public bool backPressed;
    public bool leftPressed;
    public bool rightPressed;
    float targetYaw;
    // #endif
    public override void OnNetworkSpawn()
    {
        _rb = gameObject.GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _playerShoot = GetComponent<PlayerShoot>();
        _ragdollOnOff = GetComponent<RagdollOnOff>();
        _flagPoles = GameObject.FindGameObjectsWithTag("HoleFlagPole");

        _ragdollOnOff.Activate(); // activate ragdoll
        if (!IsOwner) return;
#if ENABLE_INPUT_SYSTEM
        Debug.Log("Now Enabling Input System");
        //using new input system
        _actions = new Actions();
        _actions.Enable();
        _actions.Gameplay.Pause.started += HandlePauseStarted;
        _actions.Gameplay.Sprint.started += HandleSprintStarted;
        _actions.Gameplay.Sprint.canceled += HandleSprintCanceled;
#endif
        transform.position = new Vector3(Random.Range(390, 400), 69.1f, Random.Range(318, 320));
        // activate player controller - controller will activate the player movement, animations, shooting and ragdoll
        Activate();
    }


    // Update Loop -------------------------------------------------------------------------------------------------------------
    void FixedUpdate()
    {
        if (!_isActive) return;
        if (canMove)
        {
            Movement();
        }
    }
    void Update()
    {
        if (!_isActive) return; //prevent updates until player is fully activated

        Animate();
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
        _actions.Disable();
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

#if ENABLE_INPUT_SYSTEM
#else
        // Check for pause input
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!UIManager.isPaused) { UIManager.isPaused = true; UIManager.instance.EnablePause(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            else { UIManager.isPaused = false; UIManager.instance.DisablePause(); Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
#endif
        if (UIManager.isPaused) { return; }
        else { if (!UIManager.instance.titleScreenMode) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } }
#if ENABLE_INPUT_SYSTEM
//new input system
        moveInput = _actions.Gameplay.Move.ReadValue<Vector2>();
        lookInput = _actions.Gameplay.Look.ReadValue<Vector2>();
        InputSystemRotation();
        InputSystemMovement();
        AfterMoveStateUpdate();
#else
        // old input system
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        float rotationInput = Input.GetAxis("Mouse X");
        isSprinting = Input.GetKey(KeyCode.LeftShift) && moveVertical > 0; //sprinting only allowed when moving forward
        PlayerMovement(moveHorizontal, moveVertical, rotationInput);
#endif
    }

    private void PlayerMovement(float moveHorizontal, float moveVertical, float rotationInput)
    {

        float splayerSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical);
        movement = movement.normalized * splayerSpeed * Time.fixedDeltaTime;

        _rb.MovePosition(transform.position + transform.TransformDirection(movement));

        float rotationAmount = rotationInput * rotationSpeed * Time.fixedDeltaTime;
        Quaternion deltaRotation = Quaternion.Euler(0f, rotationAmount, 0f);
        _rb.MoveRotation(_rb.rotation * deltaRotation);

        AfterMoveStateUpdate();
    }

    public void AfterMoveStateUpdate()
    {
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
#if ENABLE_INPUT_SYSTEM
#else
        forwardPressed = Input.GetKey("w") || Input.GetKey("up");
        backPressed = Input.GetKey("s") || Input.GetKey("down");
        rightPressed = Input.GetKey("d") || Input.GetKey("right");
        leftPressed = Input.GetKey("a") || Input.GetKey("left");
#endif
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

            if (!isrunning && (forwardPressed && isSprinting) && !isStriking)
            {
                _animator.SetBool("isRunning", true);
            }
            if (isrunning && (!isSprinting || !forwardPressed))
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
#if ENABLE_INPUT_SYSTEM
        _actions.asset.FindActionMap("Gameplay", false).Disable();
        _actions.asset.FindActionMap("UI", false).Enable();
#endif
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
#if ENABLE_INPUT_SYSTEM
        _actions.asset.FindActionMap("Gameplay", false).Enable();
        _actions.asset.FindActionMap("UI", false).Disable();
#endif
        canMove = true;
    }

    // State Management -------------------------------------------------------------------------------------------------------------
    public void UpdatePlayerState(PlayerData playerState)
    {
        if (!IsOwner) return;
        _playerNetworkData.StorePlayerState(playerState);
    }
    #region  Input Actions
    public void InputSystemRotation()
    {
        if (lookInput.sqrMagnitude > inputThreshold)
        {
            float deltaTimeMultiplier = 0f;
            var devices = InputSystem.devices;
            foreach (var device in devices)
            {
                if (device is Mouse)
                {
                    deltaTimeMultiplier = 0.1f;
                }
                if (device is Gamepad)
                {
                    deltaTimeMultiplier = 0.5f;
                }
            }
            float xInput = lookInput.x;
            targetYaw += lookInput.x * deltaTimeMultiplier;
            targetYaw = ClampAngle(targetYaw, float.MinValue, float.MaxValue);
            transform.rotation = Quaternion.Euler(0, targetYaw, 0);
        }
    }
    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        while (lfAngle < -180f) lfAngle += 360f;
        while (lfAngle > 180f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
    public void InputSystemMovement()
    {
        float splayerSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;
        moveInput = moveInput.normalized;
        if (moveInput.sqrMagnitude > inputThreshold)
        {
            forwardPressed = moveInput.y > 0;
            backPressed = moveInput.y < 0;
            leftPressed = moveInput.x < 0;
            rightPressed = moveInput.x > 0;
        }
        else
        {
            forwardPressed = false;
            backPressed = false;
            leftPressed = false;
            rightPressed = false;
            splayerSpeed = 0;
        }
        _rb.velocity = transform.forward * moveInput.y * splayerSpeed + transform.right * moveInput.x * splayerSpeed + transform.up * _rb.velocity.y;
    }
    public void HandlePauseStarted(InputAction.CallbackContext ctx)
    {
        //Copied from the previous version
        if (!UIManager.isPaused)
        {
            UIManager.isPaused = true;
            UIManager.instance.EnablePause();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            UIManager.isPaused = false;
            UIManager.instance.DisablePause();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void HandleSprintStarted(InputAction.CallbackContext ctx)
    {
        isSprinting = moveInput.y > inputThreshold;
    }

    public void HandleSprintCanceled(InputAction.CallbackContext ctx)
    {
        isSprinting = false;
    }
    #endregion
}








