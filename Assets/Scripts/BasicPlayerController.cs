using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Unity.VisualScripting;
using UnityEngine.Video;

// Needs: simple way to deactivate everything on game over / game exit, so players can play again without having to re-launch the game
public class BasicPlayerController : NetworkBehaviour
{
    // prefabs
    public GameObject gameManagerPrefab;
    public GameObject spiderPrefab;

    // Movement
    public float _moveSpeed = 2f;
    private float _sprintMultiplier = 2.5f;
    public float _rotationSpeed = 100f;
    [SerializeField] private bool _isSprinting = false;

    // Physics
    private Rigidbody _rb;
    //private PlayerShoot _playerShoot;
    private SwingManager _swingManager;

    // State Management
    private PlayerData _currentPlayerState;
    private PlayerNetworkData _playerNetworkData;
    public RagdollOnOff _ragdollOnOff;
    private bool _canMove = true;

    // Animation
    private Animator _animator;
    private GameObject[] _flagPoles;

    // Spawning
    private bool _isSpawnedAtPos = false; // used to check if player has been spawned in correct position

    // Activation
    public bool IsActive = false;

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
    public bool _swingPressed;
    public bool _ballSpawnPressed;

    public override void OnNetworkSpawn()
    {
        _rb = gameObject.GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _swingManager = GetComponent<SwingManager>();
        _ragdollOnOff = GetComponent<RagdollOnOff>();
        _flagPoles = GameObject.FindGameObjectsWithTag("HoleFlagPole");

        _swingManager.Activate(); // activate swing mode
        _ragdollOnOff.Activate(); // activate ragdoll

        if (!IsOwner) return;

#if ENABLE_INPUT_SYSTEM
        #region Input Actions Initialization
        _inputActionAsset = _inputActionAsset ?? Resources.Load<InputActionAsset>("InputActionAsset/Actions");
        _inputActionAsset.Enable();
        _gameplayActionMap = _inputActionAsset.FindActionMap("Gameplay", throwIfNotFound: true);
        _gameplayActionMap.Enable();
        _inputActionAsset.FindActionMap("UI").Disable();

        _gameplayActionMap["Pause"].started += HandlePauseStarted;
        _gameplayActionMap["Sprint"].started += HandleSprintStarted;
        _gameplayActionMap["Sprint"].canceled += HandleSprintCanceled;
        _gameplayActionMap["Swing"].started += HandleSwingStarted;
        _gameplayActionMap["Swing"].canceled += HandleSwingCanceled;
        _gameplayActionMap["Ball Spawn/Exit Swing"].started += HandleBallSpawnStarted;
        _gameplayActionMap["Ball Spawn/Exit Swing"].canceled += HandleBallSpawnCanceled;
        #endregion
#endif
        // activate player controller - controller will activate the player movement, animations, shooting and ragdoll
        //Activate();
    }

    // Update Loop -------------------------------------------------------------------------------------------------------------
    void Update()
    {

        if (!IsOwner) return;

        //prevent updates until player is fully activated
        if (!IsActive)
        {
            // activate game for all players if host presses space in pre-game lobby
            if (IsServer && Input.GetKeyDown(KeyCode.P))
            {
                //close lobby for new players
                _ = LobbyManager.Instance.LockLobby(); // no await here - does not block main thread but thats okay, as long as lobby starts lock process

                // activate all players
                foreach (NetworkClient player in NetworkManager.Singleton.ConnectedClientsList)
                {
                    Debug.Log("Activating player " + player.PlayerObject.GetComponent<BasicPlayerController>().OwnerClientId + "from server: " + IsServer);
                    player.PlayerObject.GetComponent<BasicPlayerController>().ActivateClientRpc();
                }

            }
        }

        Animate();
        if (_canMove)
        {
            Movement();
        }
    }

    // Activation -------------------------------------------------------------------------------------------------------------

    public void Activate()
    {
        _playerNetworkData = GetComponent<PlayerNetworkData>();
        _ragdollOnOff._playerNetworkData = _playerNetworkData;

        Debug.Log("inside Activate() owned by player " + OwnerClientId + " isOwner: " + IsOwner + "Is local player: " + IsLocalPlayer + "Is server: " + IsServer + "Is client: " + IsClient);

        // spawn players at firt hole
        transform.position = new Vector3(390 + OwnerClientId * 2, 69.5f, 321); //space players out by 2 units each
        transform.rotation = Quaternion.Euler(0, -179f, 0); //face flag pole
        Debug.Log("Player " + OwnerClientId + " spawned at " + transform.position);

        IsActive = true;

        if (IsServer)
        {
            //activate game manager
            GameObject gameManager = Instantiate(gameManagerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
            gameManager.GetComponent<NetworkObject>().Spawn();

            //activate spider
            GameObject spider = Instantiate(spiderPrefab, new Vector3(391, 72.1f, 289), Quaternion.identity);
            spider.GetComponent<NetworkObject>().Spawn();
        }

        // activate flag poles - in scene placed network objects (server auth) -update this later to be dynamically spawned by server
        foreach (GameObject flagPole in _flagPoles)
        {
            flagPole.GetComponent<HoleFlagPoleManager>().playerNetworkData = _playerNetworkData;
            //flagPole.GetComponent<HoleFlagPoleManager>().playerController = this;
            flagPole.GetComponent<HoleFlagPoleManager>().uiManager = GameObject.Find("Canvas").GetComponent<UIManager>();
            flagPole.GetComponent<HoleFlagPoleManager>().Activate();
        }

        // set initial player state
        _currentPlayerState = new PlayerData
        {
            playerID = OwnerClientId,
            currentHole = 1,
            strokes = 0,
            enemiesDefeated = 0,
            score = 0
        };
        UpdatePlayerState(_currentPlayerState);
    }


    public void Deactivate()
    {
        IsActive = false;

        foreach (GameObject flagPole in _flagPoles)
        {
            flagPole.GetComponent<HoleFlagPoleManager>().Deactivate();
        }
        _ragdollOnOff.Deactivate();
        _inputActionAsset?.FindActionMap("Gameplay").Disable();
    }

    public override void OnDestroy()
    {
        Deactivate();
        base.OnDestroy();
    }


    // Movement -------------------------------------------------------------------------------------------------------------

    private void Movement()
    {
        if (!IsOwner) return;

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
        //AfterMoveStateUpdate();
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
        _swingPressed = Input.GetKeyDown("e");
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

            if (_swingPressed && !isStriking)
            {
                _animator.SetBool("isStriking", true);
                _animator.SetBool("justStriked", true);
                DisableInput();
            }

            if (isStriking)
            {
                if (!_swingPressed)
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
        _inputActionAsset?.FindActionMap("Gameplay", false).Disable();
        _inputActionAsset?.FindActionMap("UI", false).Enable();
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
        _inputActionAsset?.FindActionMap("Gameplay", false).Enable();
        _inputActionAsset?.FindActionMap("UI", false).Disable();
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
            SettingsData sData = DataManager.instance.GetSettingsData();
            _rb.MoveRotation(Quaternion.Euler(0f, _playerYaw * sData.cameraSensitivity, 0f));
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

            Vector3 movement = new Vector3(_moveInput.x, 0f, _moveInput.y);
            movement = movement.normalized * splayerSpeed * Time.deltaTime;
            _rb.MovePosition(transform.position + transform.TransformDirection(movement));
            Debug.Log("Player " + OwnerClientId + " pos: " + transform.position);
        }
        else
        {
            _forwardPressed = false;
            _backPressed = false;
            _leftPressed = false;
            _rightPressed = false;
            //splayerSpeed = 0;
        }

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
    public void HandleSwingStarted(InputAction.CallbackContext ctx)
    {
        _swingPressed = true;
    }
    public void HandleSwingCanceled(InputAction.CallbackContext ctx)
    {
        _swingPressed = false;
    }
    public void HandleBallSpawnStarted(InputAction.CallbackContext ctx)
    {
        _ballSpawnPressed = true;
    }
    public void HandleBallSpawnCanceled(InputAction.CallbackContext ctx)
    {
        _ballSpawnPressed = false;
    }
    #endregion


    // Network Functions -------------------------------------------------------------------------------------------------------------

    [ClientRpc]
    public void ActivateClientRpc()
    {
        if (!IsOwner) return;
        Activate();
    }

    // spawn functions -------------------------------------------------------------------------------------------------------------
    public void SpawnInPreLobby()
    {
        if (!IsOwner) return;

        _rb.MovePosition(new Vector3(94.2f + OwnerClientId * 2, 100.5f, -136.3f));//space players out by 2 units each
        Debug.Log("Player " + OwnerClientId + " spawned at " + transform.position);
    }
}








