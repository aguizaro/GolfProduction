using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Unity.VisualScripting;
using UnityEngine.Video;
using UnityEngine.Events;
using FMODUnity;
using FMOD.Studio;
using System.Threading.Tasks;
using Unity.Services.Lobbies.Models;

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
    public Quaternion _playerRotation;
    [SerializeField] private bool _isSprinting = false;

    // Physics
    private Rigidbody _rb;
    //private PlayerShoot _playerShoot;
    private SwingManager _swingManager;

    // State Management
    private PlayerData _currentPlayerState;
    private PlayerNetworkData _playerNetworkData;
    public RagdollOnOff _ragdollOnOff;
    public string playerColor;

    // Animation
    private Animator _animator;
    private GameObject[] _flagPoles;
    private Animator _gateAnimator;

    // Spawning
    private bool _isSpawnedAtPos = false; // used to check if player has been spawned in correct position
    // Cosmetics
    private PlayerHatController _playerHat;

    // Sound
    private EventInstance playerFootsteps;


    // Activation
    public bool IsActive = false;

    [Header("For Input System Only")]
    [SerializeField] private bool _canInput = false; // FALSE BY DEFAULT - THIS IS SET TO TRUE AFTER PLAYER HAS SPAWNED IN PRE LOBBY
    public bool canInput // this is used to enable and disable input for the player (used for pause menu)
    {
        get
        {
            return _canInput;
        }
        set
        {
            _canInput = value;
        }
    }
    [SerializeField] private bool _canMove = true;
    public bool canMove // enable and disable movement for the player (used for ragdoll mode)
    {
        get
        {
            return canInput && _canMove;
        }
        set
        {
            _canMove = value;
        }
    }
    [SerializeField] private bool _canLook = true;
    public bool canLook // enable and disable looking for the player (used for ragdoll mode)
    {
        get
        {
            return canInput && _canLook;
        }
        set
        {
            _canLook = value;
        }
    }
    public Vector2 _moveInput;
    public Vector2 _lookInput;
    public const float _inputThreshold = 0.001f;
    public InputActionAsset _inputActionAsset;
    public InputActionMap gameplayActionMap = new InputActionMap();
    public float _playerYaw = 0f;

    [Header("Hybrid Variables For Both Input Systems")]
    public bool _forwardPressed;
    public bool _backPressed;
    public bool _leftPressed;
    public bool _rightPressed;
    public bool _swingPressed;
    public bool _ballSpawnExitSwingPressed;
    [Header("Action Checkers")]
    public bool isStriking;

    public override void OnNetworkSpawn()
    {
        _rb = gameObject.GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _playerHat = GetComponent<PlayerHatController>();
        _swingManager = GetComponent<SwingManager>();
        _ragdollOnOff = GetComponent<RagdollOnOff>();
        _playerNetworkData = GetComponent<PlayerNetworkData>();
        _flagPoles = GameObject.FindGameObjectsWithTag("HoleFlagPole");

        //_swingManager.Activate(); // activate swing mode
        _ragdollOnOff.Activate(); // activate ragdoll
        //_playerHat.RandomizeHatTexture();
        if (!IsOwner) return;

        #region Input Actions Initialization
        UIManager.instance.onEnablePause.AddListener(DisableInput);
        UIManager.instance.onDisablePause.AddListener(EnableInput);
        _inputActionAsset = _inputActionAsset ?? Resources.Load<InputActionAsset>("InputActionAsset/Actions");
        _inputActionAsset.Enable();
        gameplayActionMap = _inputActionAsset.FindActionMap("Gameplay", throwIfNotFound: true);
        gameplayActionMap.Enable();
        _inputActionAsset.FindActionMap("UI")["Pause"].started += HandlePauseStarted;
        _inputActionAsset.FindActionMap("UI")["ScoreBoard"].started += HandleScoreBoardStarted;
        _inputActionAsset.FindActionMap("UI")["ScoreBoard"].canceled += HandleScoreBoardCanceled;
        _inputActionAsset.FindActionMap("UI").Disable();

        gameplayActionMap["Pause"].started += HandlePauseStarted;
        gameplayActionMap["Sprint"].started += HandleSprintStarted;
        gameplayActionMap["Sprint"].canceled += HandleSprintCanceled;
        gameplayActionMap["Swing"].started += HandleSwingStarted;
        gameplayActionMap["Swing"].canceled += HandleSwingCanceled;
        gameplayActionMap["Ball Spawn/Exit Swing"].started += HandleBallSpawnExitSwingStarted;
        gameplayActionMap["Ball Spawn/Exit Swing"].canceled += HandleBallSpawnExitSwingCanceled;
        gameplayActionMap["ScoreBoard"].started += HandleScoreBoardStarted;
        gameplayActionMap["ScoreBoard"].canceled += HandleScoreBoardCanceled;
        canInput = true;
        canMove = true;
        canLook = true;
        #endregion

        GameObject.Find("Main Camera").GetComponent<StudioListener>().SetAttenuationObject(gameObject);


        // THIS LINE IS THROWING AN ERROR: --------------------------------------------------------------------------------------------------------------------------------
            // - InvalidOperationException: An RPC called on a NetworkObject that is not in the spawned objects list. Please make sure the NetworkObject is spawned before calling RPCs.
            // - This error occurs when a player leaves a game and attempts to host a new game 
        //AudioManager.instance.PlayTimelineSoundForAllClients(FMODEvents.instance.playerFootsteps, gameObject);
        // --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------


        //playerFootsteps = AudioManager.instance.CreateInstance(FMODEvents.instance.playerFootsteps, gameObject);

        // activate player controller - controller will activate the player movement, animations, shooting and ragdoll
        //_playerHat.RandomizeHatTexture();
        //_playerHatMeshId = _playerHat.GetCurrentMeshId();
        //_playerHatTextureId = _playerHat.GetCurrentTextureId();
        //Activate();

        // game manager initialization
        if (IsServer)
        {
            //activate game manager on spawn
            GameObject gameManager = Instantiate(gameManagerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
            gameManager.GetComponent<NetworkObject>().Spawn();
        }
        else
        {
        }
    }

    // Update Loop -------------------------------------------------------------------------------------------------------------
    void Update()
    {

        if (!IsOwner) return;

        //handle player falling through the map
        if (transform.position.y < -10)
        {
            _rb.useGravity = false;
            _rb.velocity = Vector3.zero;

            int playerNum = (int)_playerNetworkData.GetPlayerData().playerNum;

            if (!IsActive) SpawnInPreLobby(playerNum);
            else transform.position = new Vector3(390 + playerNum * 2, 69.5f, 321); //spawn in first hole

            _rb.useGravity = true;
        }

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
                    player.PlayerObject.GetComponent<BasicPlayerController>().ActivateClientRpc();
                }

            }

            // Cycle player's hat config
            if (IsOwner && Input.GetKeyDown(KeyCode.L))
            {
                _playerHat.CycleHatConfig();
            }

            // Cycle player's color config
            if (IsOwner && Input.GetKeyDown(KeyCode.K))
            {
                GetComponent<PlayerColor>().CyclePlayerColor();
            }
        }

        Animate();
        Movement();
    }

    // Activation -------------------------------------------------------------------------------------------------------------

    public void Activate()
    {
        IsActive = true;

        if (IsServer)
        {
            //activate spider
            //GameObject spider = Instantiate(spiderPrefab, new Vector3(-51.4f, 11.4f, 37.97f), Quaternion.identity);
            //spider.GetComponent<NetworkObject>().Spawn();
        }

        if (IsOwner) OpenGate();

        // activate flag poles - in scene placed network objects (server auth) -update this later to be dynamically spawned by server
        foreach (GameObject flagPole in _flagPoles)
        {
            flagPole.GetComponent<HoleFlagPoleManager>().playerNetworkData = _playerNetworkData;
            flagPole.GetComponent<HoleFlagPoleManager>().uiManager = GameObject.Find("Canvas").GetComponent<UIManager>();
            flagPole.GetComponent<HoleFlagPoleManager>().Activate();
        }

        // set initial player state when game starts (after pre-lobby)
        _currentPlayerState = _playerNetworkData.GetPlayerData();
        _currentPlayerState.currentHole = 1;
        UpdatePlayerState(_currentPlayerState);

        // notify players that the lobby is locked
        UIManager.instance.DisplayNotification("Game On! Lobby is now locked");
    }


    public void Deactivate()
    {
        IsActive = false;

        foreach (GameObject flagPole in _flagPoles)
        {
            flagPole.GetComponent<HoleFlagPoleManager>().Deactivate();
        }
        _ragdollOnOff.Deactivate();
        _swingManager.Deactivate();

        if (IsOwner) CloseGate();

        if (!IsOwner) return;
        UIManager.instance.onEnablePause.RemoveListener(DisableInput);
        UIManager.instance.onDisablePause.RemoveListener(EnableInput);
        gameplayActionMap["Pause"].started -= HandlePauseStarted;
        gameplayActionMap["Sprint"].started -= HandleSprintStarted;
        gameplayActionMap["Sprint"].canceled -= HandleSprintCanceled;
        gameplayActionMap["Swing"].started -= HandleSwingStarted;
        gameplayActionMap["Swing"].canceled -= HandleSwingCanceled;
        gameplayActionMap["Ball Spawn/Exit Swing"].started -= HandleBallSpawnExitSwingStarted;
        gameplayActionMap["Ball Spawn/Exit Swing"].canceled -= HandleBallSpawnExitSwingCanceled;
        _inputActionAsset.FindActionMap("UI")["Pause"].started -= HandlePauseStarted;
        _inputActionAsset?.FindActionMap("Gameplay").Disable();
        _inputActionAsset?.FindActionMap("UI").Disable();
    }

    public override void OnDestroy()
    {
        Deactivate();
        base.OnDestroy();
    }


    void OpenGate()
    {
        _gateAnimator = GameObject.FindWithTag("Gates").GetComponent<Animator>();
        _gateAnimator.SetTrigger("OpenGate");

        // play gate open sound
    }

    void CloseGate()
    {
        if (!IsOwner) return;

        _gateAnimator = GameObject.FindWithTag("Gates").GetComponent<Animator>();
        //_gateAnimator.SetTrigger("CloseGate"); // this animation does not exist yet - DELETE THIS COMMENT WHEN ANIMATION IS CREATED

        // play gate close sound
    }


    // Movement -------------------------------------------------------------------------------------------------------------

    private void Movement()
    {
        if (!IsOwner) return;

        if (UIManager.isPaused) { return; }
        else { if (!UIManager.instance.titleScreenMode) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } }

        InputSystemRotation();
        InputSystemMovement();
        //AfterMoveStateUpdate();
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
        isStriking = _animator.GetBool("isStriking");


        if (UIManager.isPaused) { return; }

        if (IsOwner)
        {
            _moveInput = canMove ? gameplayActionMap["Move"].ReadValue<Vector2>().normalized : Vector2.zero;
            _animator.SetFloat("moveX", _moveInput.x);
            _animator.SetFloat("moveY", _moveInput.y);

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
        }
    }


    // Input Management -------------------------------------------------------------------------------------------------------------

    // IN THE FUTURE - lets add a parameter to include rotation (sometimes we wan to disable movement but still allow rotation) - ex: when in ragdoll mode
    public void DisableInput()
    {
        _inputActionAsset?.FindActionMap("Gameplay", false).Disable();
        _inputActionAsset?.FindActionMap("UI", false).Enable();

        _animator.SetBool("isWalking", false);
        _animator.SetBool("isRunning", false);
        _animator.SetBool("isLeft", false);
        _animator.SetBool("isRight", false);
        _animator.SetBool("isReversing", false);
        //_animator.SetBool("isStriking", false);
        canInput = false;
    }

    public void EnableInput()
    {
        _inputActionAsset?.FindActionMap("Gameplay", false).Enable();
        _inputActionAsset?.FindActionMap("UI", false).Disable();

        canInput = true;
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
        _lookInput = canLook ? gameplayActionMap["Look"].ReadValue<Vector2>() : Vector2.zero;
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
        if (!canInput || !canMove) return;
        _isSprinting = _moveInput.y > _inputThreshold;
    }

    public void HandleSprintCanceled(InputAction.CallbackContext ctx)
    {
        if (!canInput || !canMove) return;
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
    public void HandleBallSpawnExitSwingStarted(InputAction.CallbackContext ctx)
    {
        _ballSpawnExitSwingPressed = true;
    }
    public void HandleBallSpawnExitSwingCanceled(InputAction.CallbackContext ctx)
    {
        _ballSpawnExitSwingPressed = false;
    }

    public void HandleScoreBoardStarted(InputAction.CallbackContext ctx)
    {
        UIManager.instance.scoreboardUI.SetActive(true);
        //TODO: Turn on Score Board Panel 
        //Debug.Log("Tab pressed");
    }
    public void HandleScoreBoardCanceled(InputAction.CallbackContext ctx)
    {
        //TODO: Turn off Score Board Panel
        //Debug.Log("Tab released");
    }
    #endregion


    // Network Functions -------------------------------------------------------------------------------------------------------------

    [ClientRpc]
    public void ActivateClientRpc()
    {
        if (!IsOwner) return;

        if (_swingManager == null) _swingManager = GetComponent<SwingManager>();
        if (_swingManager.isInSwingState()) _swingManager.ExitSwingMode();

        _swingManager.RemoveForces(); // in case ball is moving

        DisableInput();

        UIManager.instance.DeactivateDirections(); // deactivate directions UI when game starts (only want this to happen once - so we check if player is owner))
        if (_ragdollOnOff.IsRagdoll()) _ragdollOnOff.ResetRagdoll(); // reset ragdoll if player is in ragdoll mode

        Activate();

        EnableInput();
    }

    // spawn functions -------------------------------------------------------------------------------------------------------------
    public void SpawnInPreLobby(int playerNumber)
    {
        if (!IsOwner) return;

        var spawnPos = new Vector3(-80f + playerNumber * 2, 10f, 64.25f);
        _rb.MovePosition(spawnPos);//space players out by 2 units each
        StartCoroutine(WaitForSpawnPosition(spawnPos));
    }

    // coroutine that waits for player to be spawned in pre-lobby
    private IEnumerator WaitForSpawnPosition(Vector3 spawnPos)
    {
        if (Vector3.Distance(transform.position, spawnPos) > 0.001f){
            yield return new WaitForSeconds(0.1f);
        }

        _isSpawnedAtPos = true;
        GetComponent<StartCameraFollow>().Activate();
        DelayedActivation(); 
    }

    // spawns ball after slight delay then calls LobbyManager StartGame() and enables player input
    private async void DelayedActivation()
    {
        await Task.Delay(1000); // slight delay to allow player to actually spwan in correct position before spawning ball ( 1000 ms is good bc we also have to wait for camera to reach player)
        GetComponent<SwingManager>().Activate();
        LobbyManager.Instance.StartGame();
        GetComponent<BasicPlayerController>().canInput = true;
    }

}