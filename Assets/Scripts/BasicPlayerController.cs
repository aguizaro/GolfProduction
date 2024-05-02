using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

// Needs: simple way to deactivate everything on game over / game exit, so players can play again without having to re-launch the game
public class BasicPlayerController : NetworkBehaviour
{
    // Movement
    public float _moveSpeed = 2f;
    public float _sprintMultiplier = 4f;
    public float _rotationSpeed = 100f;
    [SerializeField] private bool _isSprinting = false;

    // Physics
    private Rigidbody _rb;
    private PlayerShoot _playerShoot;

    // State Management
    public PlayerData _currentPlayerState;
    private PlayerNetworkData _playerNetworkData;
    private RagdollOnOff _ragdollOnOff;
    private bool _canMove = true;

    // Animation
    private Animator _animator;
    private GameObject[] _flagPoles;

    // Activation
    [SerializeField] private bool _isActive = false;

#if ENABLE_INPUT_SYSTEM
    [Header("For Input System Only")]
    public Vector2 _moveInput;
    public Vector2 _lookInput;
    public const float _inputThreshold = 0.001f;
    public InputActionAsset _inputActionAsset;
    public InputActionMap _gameplayActionMap = new InputActionMap();
    public float _playerYaw = 0f;
    public InputActionRebindingExtensions.RebindingOperation _rebindingOperation;
    public string targetActionName = "Sprint";
    public string _newInputPath;
    public int _testValue = 0;
#endif
    [Header("Hybrid Variables For Both Input Systems")]
    public bool _forwardPressed;
    public bool _backPressed;
    public bool _leftPressed;
    public bool _rightPressed;
    public bool _strikePressed;

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
        #region Input Actions Initialization
        _inputActionAsset = _inputActionAsset??Resources.Load<InputActionAsset>("InputActionAsset/Actions");
        _inputActionAsset.Enable();
        _gameplayActionMap = _inputActionAsset.FindActionMap("Gameplay",throwIfNotFound: true);
        _gameplayActionMap.Enable();
        _inputActionAsset.FindActionMap("UI").Disable();

        _gameplayActionMap["Pause"].started += HandlePauseStarted;
        _gameplayActionMap["Sprint"].started += HandleSprintStarted;
        _gameplayActionMap["Sprint"].canceled += HandleSprintCanceled;
        _gameplayActionMap["Strike"].started += HandleStrikeStarted;
        _gameplayActionMap["Strike"].canceled += HandleStrikeCanceled;
        #endregion
#endif
        transform.position = new Vector3(Random.Range(390, 400), 69.1f, Random.Range(318, 320));
        // activate player controller - controller will activate the player movement, animations, shooting and ragdoll
        Activate();
    }


    // Update Loop -------------------------------------------------------------------------------------------------------------
    void Update()
    {
        if (!_isActive) return; //prevent updates until player is fully activated

        Animate();
        if (_canMove)
        {
            Movement();
        }
    }

    // Activation -------------------------------------------------------------------------------------------------------------

    public void Activate()
    {
        // Debug.Log("Activating player controller for " + OwnerClientId + " isOwner: " + IsOwner);
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
        _inputActionAsset.FindActionMap("Gameplay").Disable();
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
#if ENABLE_INPUT_SYSTEM
        InputSystemRotation();
        InputSystemMovement();
#else
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        float rotationInput = Input.GetAxis("Mouse X");
        _isSprinting = Input.GetKey(KeyCode.LeftShift) && moveVertical > 0; //sprinting only allowed when moving forward
        PlayerMovement(moveHorizontal, moveVertical, rotationInput);
#endif
        AfterMoveStateUpdate();
    }

    private void PlayerMovement(float moveHorizontal, float moveVertical, float rotationInput)
    {

        float splayerSpeed = _isSprinting ? _moveSpeed * _sprintMultiplier : _moveSpeed;

        Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical);
        movement = movement.normalized * splayerSpeed * Time.deltaTime;

        _rb.MovePosition(transform.position + transform.TransformDirection(movement));

        float rotationAmount = rotationInput * _rotationSpeed * Time.deltaTime;
        Quaternion deltaRotation = Quaternion.Euler(0f, rotationAmount, 0f);
        _rb.MoveRotation(_rb.rotation * deltaRotation);
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
        _forwardPressed = Input.GetKey("w") || Input.GetKey("up");
        _backPressed = Input.GetKey("s") || Input.GetKey("down");
        _rightPressed = Input.GetKey("d") || Input.GetKey("right");
        _leftPressed = Input.GetKey("a") || Input.GetKey("left");
        _strikePressed = Input.GetKeyDown("e");
#endif


        if (UIManager.isPaused) { return; }

        if (IsOwner)
        {
#if ENABLE_INPUT_SYSTEM
            _moveInput = _gameplayActionMap["Move"].ReadValue<Vector2>().normalized;
            _animator.SetFloat("moveX", _moveInput.x);
            _animator.SetFloat("moveY", _moveInput.y);
#else
            _animator.SetFloat("moveX", 0f);
            _animator.SetFloat("moveY", 0f);
#endif
            if (_forwardPressed && !isWalking)
            {
                _animator.SetBool("isWalking", true);
            }
            if (!_forwardPressed && isWalking)
            {
                _animator.SetBool("isWalking", false);
            }

            if (_backPressed && !isReversing)
            {
                _animator.SetBool("isReversing", true);
            }
            if (!_backPressed && isReversing)
            {
                _animator.SetBool("isReversing", false);
            }

            if (!isrunning && (_forwardPressed && _isSprinting) && !isStriking)
            {
                _animator.SetBool("isRunning", true);
            }
            if (isrunning && (!_isSprinting || !_forwardPressed))
            {
                _animator.SetBool("isRunning", false);
            }

            if (_leftPressed && !isStrafingLeft)
            {
                _animator.SetBool("isLeft", true);
            }
            if (!_leftPressed && isStrafingLeft)
            {
                _animator.SetBool("isLeft", false);
            }

            if (_rightPressed && !isStrafingRight)
            {
                _animator.SetBool("isRight", true);
            }
            if (!_rightPressed && isStrafingRight)
            {
                _animator.SetBool("isRight", false);
            }

            if (_strikePressed && !isStriking)
            {
                _animator.SetBool("isStriking", true);
                _animator.SetBool("justStriked", true);
                DisableInput();
            }

            if (isStriking)
            {
                if (!_strikePressed)
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
        _inputActionAsset.FindActionMap("Gameplay", false).Disable();
        _inputActionAsset.FindActionMap("UI", false).Enable();
#endif
        _animator.SetBool("isWalking", false);
        _animator.SetBool("isRunning", false);
        _animator.SetBool("isLeft", false);
        _animator.SetBool("isRight", false);
        _animator.SetBool("isReversing", false);
        //_animator.SetBool("isStriking", false);
        _canMove = false;
    }

    public void EnableInput()
    {
#if ENABLE_INPUT_SYSTEM
        _inputActionAsset.FindActionMap("Gameplay", false).Enable();
        _inputActionAsset.FindActionMap("UI", false).Disable();
#endif
        _canMove = true;
    }

    // State Management -------------------------------------------------------------------------------------------------------------
    public void UpdatePlayerState(PlayerData playerState)
    {
        if (!IsOwner) return;
        _playerNetworkData.StorePlayerState(playerState);
    }
    #region  Input Actions Functions
    public void InputSystemRotation()
    {
        _lookInput = _gameplayActionMap["Look"].ReadValue<Vector2>();
        if (_lookInput.sqrMagnitude > _inputThreshold)
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
            _playerYaw += _lookInput.x * deltaTimeMultiplier;
            _playerYaw = ClampAngle(_playerYaw, float.MinValue, float.MaxValue);
            _rb.MoveRotation(Quaternion.Euler(0f, _playerYaw, 0f));
        }
    }
    public static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        while (lfAngle < -180f) lfAngle += 360f;
        while (lfAngle > 180f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
    public void InputSystemMovement()
    {
        float splayerSpeed = _isSprinting ? _moveSpeed * _sprintMultiplier : _moveSpeed;
        if (_moveInput.sqrMagnitude > _inputThreshold)
        {
            _forwardPressed = _moveInput.y > 0.1f;
            _backPressed = _moveInput.y < -0.2f;
            _leftPressed = _moveInput.x < -0.25f;
            _rightPressed = _moveInput.x > 0.25f;
        }
        else
        {
            _forwardPressed = false;
            _backPressed = false;
            _leftPressed = false;
            _rightPressed = false;
            splayerSpeed = 0;
        }
        Vector3 movement = new Vector3(_moveInput.x, 0f, _moveInput.y);
        movement = movement.normalized * splayerSpeed * Time.deltaTime;
        _rb.MovePosition(transform.position + transform.TransformDirection(movement));
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
        _isSprinting = _moveInput.y > _inputThreshold;
    }

    public void HandleSprintCanceled(InputAction.CallbackContext ctx)
    {
        _isSprinting = false;
    }
    public void HandleStrikeStarted(InputAction.CallbackContext ctx)
    {
        _strikePressed = true;
    }
    public void HandleStrikeCanceled(InputAction.CallbackContext ctx)
    {
        _strikePressed = false;
    }
    #endregion
    #region Actions Rebinding
    [ContextMenu("Rebind Actions")]
    public void TestRebinding()
    {
        RebindActions(targetActionName);
        _testValue++;
    }
    public void CheckTest(InputAction.CallbackContext ctx)
    {
        Debug.Log($"{ctx.action.name}: {_strikePressed}");
    }
    public void RebindActions(string name)
    {
        _gameplayActionMap.Disable();
        _inputActionAsset.FindActionMap("UI", false).Enable();
        InputAction action = _gameplayActionMap[name];
        Debug.Log($"Rebinding Start for {name},binding count {action.bindings.Count}");
        for (int i = 0; i < action.bindings.Count; i++)
        {
            Debug.Log($"{action.bindings[i].effectivePath}");
        }

        _newInputPath = InputControlPath.ToHumanReadableString(action.bindings[0].effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice);

        action.Disable();

        _rebindingOperation = action.PerformInteractiveRebinding()
            .WithControlsExcluding("<Gamepad or Keyboard>")
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation =>
            {
                int bindingIndex = operation.action.GetBindingIndexForControl(operation.action.controls[0]);
                var newBinding = operation.action.bindings[bindingIndex];
                _newInputPath = InputControlPath.ToHumanReadableString(newBinding.effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice);
                action.ApplyBindingOverride(bindingIndex, newBinding.effectivePath);
                operation.action.started += CheckTest;
                _inputActionAsset.FindActionMap("Gameplay", false).Enable();
                _inputActionAsset.FindActionMap("UI", false).Disable();
                _rebindingOperation.Dispose();
                Debug.Log($"Rebinding Completed for {name},binding count {action.bindings.Count}");
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    Debug.Log($"{action.bindings[i].effectivePath}");
                }
            })
            .Start();
    }
    #endregion 
}








