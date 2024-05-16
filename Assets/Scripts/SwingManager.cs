using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class SwingManager : NetworkBehaviour
{
    public Transform playerTransform;
    public Animator playerAnimator;
    public GameObject ballPrefab;
    public StartCameraFollow cameraFollowScript;
    public Canvas meterCanvas;
    public GameObject meterCanvasObject;
    private UIManager _uiManager;
    private RagdollOnOff _ragdollOnOff;

    private Slider powerMeter;
    private PowerMeter powerMeterRef;
    private Coroutine playerMoveCoroutine;

    private float startSwingMaxDistance = 1.6f;   // The distance the player can be from their ball to start swing mode
    [SerializeField]
    private bool inSwingMode = false;
    private bool waitingForSwing = false;
    private bool _isActive;
    private GameObject thisBall;    // Reference to this player's ball
    private Rigidbody thisBallRb;
    private BasicPlayerController _playerController;
    private PlayerNetworkData _playerNetworkData;
    private float swingForce = 50f;
    private GameObject ragdolled_player = null; // this will be null if no ragdolled player is nearby
    private GameObject ragdolledPlayer; // Reference to the ragdolled player

    [SerializeField] private float verticalAngle = 0.50f;

    private Vector3[] holeStartPositions = new Vector3[]
    {
        new Vector3(390f, 69.5f, 321f),
        new Vector3(417.690155f, 79f, 234.9218f),
        new Vector3(451.415436f, 80f, 172.0176f),
        new Vector3(374.986023f, 93.3f, 99.01516f),
        new Vector3(306.8986f, 103.3f, 89.0007248f),
        new Vector3(235.689041f, 97.2f, 114.393f),
        new Vector3(217.792923f, 86.5f, 163.657547f),
        new Vector3(150.851669f, 90f, 163.362488f),
        new Vector3(76.4118042f, 93.15f, 169.826523f)
    };

    private bool thisBallMoving = false;

    public void Activate()
    {
        _isActive = true;
        if (IsOwner)
        {
            SpawnBallOnServerRpc(OwnerClientId);
        }

    }

    public void Deactivate()
    {
        _isActive = false;
    }

    void Start()
    {
        powerMeter = GetComponentInChildren<Slider>();
        powerMeterRef = meterCanvas.GetComponent<PowerMeter>();
        _uiManager = GameObject.Find("Canvas").GetComponent<UIManager>();

        _ragdollOnOff = GetComponent<RagdollOnOff>();

        _playerNetworkData = GetComponent<PlayerNetworkData>();
        _playerController = GetComponent<BasicPlayerController>();

        if (IsOwner) RegisterActions();
    }

    public void RegisterActions()
    {
        _playerController.gameplayActionMap["Swing"].started += HandleSwingStarted;
        _playerController.gameplayActionMap["Swing"].canceled += HandleSwingCanceled;
        _playerController.gameplayActionMap["Ball Spawn/Exit Swing"].started += HandleBallSpawnExitSwingStarted;
        _playerController.gameplayActionMap["Ball Spawn/Exit Swing"].canceled += HandleBallSpawnExitSwingCanceled;
    }

    // Update is called once per frame
    void Update()
    {
        if (!_isActive) return;

        if (!IsOwner || !isActiveAndEnabled)
        {
            return;
        }


        // Check if player is already in swing mode
        if (inSwingMode)
        {
            // Exit swing mode without performing swing
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ExitSwingMode();
                return;
            }

            // Check if the ragdolled player has gotten up
            if (ragdolled_player != null)
            {
                if (ragdolled_player.GetComponent<BasicPlayerController>().enabled)
                {

                    ragdolled_player = null;
                    ExitSwingMode();
                }
                // otherwise if player shoots, perform swing on player
                else if (powerMeterRef.GetShotStatus() == true && waitingForSwing)
                {
                    Debug.Log("PerformSwing() on ownerID " + ragdolled_player.GetComponent<NetworkObject>().OwnerClientId + " from client " + NetworkManager.Singleton.LocalClientId);
                    playerAnimator.SetTrigger("Swing");
                    playerAnimator.ResetTrigger("Stance");

                    PerformSwingOnPlayer();
                }
            }
            else if (powerMeterRef.GetShotStatus() == true && waitingForSwing)
            {
                // Start swing animation, when the club is halfway through the swing it will call PerformSwing() - from the animation event
                playerAnimator.SetTrigger("Swing");
                playerAnimator.ResetTrigger("Stance");
            }

            return; // Don't execute further logic
        }

        // Check for input to enter swing mode - prioritize swing mode on ragdolled players (short circuit evaluation)
        if (!inSwingMode && Input.GetKeyDown(KeyCode.Space))
        {

            if (isCloseToRagdolledPlayer())
            {
                Debug.Log("Update(): Close to ragdolled player - Starting swing mode on player: " + ragdolled_player != null ? ragdolled_player : "null");
                StartSwingMode();
            }
            else if (IsCloseToBall())
            {
                StartSwingMode();
            }
        }

        if (Input.GetKeyDown(KeyCode.F) && (thisBall != null))
        {
            ReturnBallToPlayer();

            if (!_playerController.IsActive) return; // do not count strokes if the player is in pre-game lobby

            PlayerData _currentPlayerData = _playerNetworkData.GetPlayerData();
            _currentPlayerData.strokes++;
            _playerNetworkData.StorePlayerState(_currentPlayerData);
            _uiManager.UpdateStrokesUI(_currentPlayerData.strokes);
        }

    }

    bool IsCloseToBall()
    {
        Debug.Log("Checking is close to ball");
        // Checks if the player is close enough to the ball and looking at it
        if (thisBall == null) return false;

        float distance = Vector3.Distance(playerTransform.position + Vector3.down, thisBall.transform.position);

        if (distance <= startSwingMaxDistance)
        {
            return true;
        }

        return false; // Ball exists but player is not close enough/looking at it
    }

    // finds nearby players and checks if they are ragdolled - returns true if a ragdolled player is found and sets the ragdolled_player_id
    bool isCloseToRagdolledPlayer()
    {
        Debug.Log("Checking is close to ragdolled player");
        //find nearby players
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            // dont find ourselves
            if (player.GetComponent<NetworkObject>().OwnerClientId != OwnerClientId)
            {
                float distance = Vector3.Distance(player.transform.position, transform.position); ;
                if (!player.GetComponent<BasicPlayerController>().enabled)
                {
                    if (distance <= 2f)
                    {
                        setRagdolledPlayerServerRpc((int)player.GetComponent<NetworkObject>().OwnerClientId);
                        ragdolledPlayer = player;
                        Debug.Log("isCloseToRagdolledPlayer(): Ragdolled player found: " + ragdolled_player != null ? ragdolled_player : "null");
                        return true;
                    }
                }

            }
        }
        setRagdolledPlayerServerRpc(-1);
        ragdolledPlayer = null;
        return false;
    }

    public void StartSwingMode()
    {
        if (_ragdollOnOff.IsRagdoll()) return;

        playerAnimator.ResetTrigger("isWalking");
        playerAnimator.ResetTrigger("isRunning");
        playerAnimator.ResetTrigger("isStriking");
        playerAnimator.ResetTrigger("isRight");
        playerAnimator.ResetTrigger("isLeft");
        playerAnimator.ResetTrigger("isReversing");
        playerAnimator.SetTrigger("Stance");
        //Debug.Log("Swing State entered");

        RemoveForces(); //  prevent ball from rolling
        stopRotation();

        // Enable power meter
        // meterCanvas.GetComponent<Canvas>().enabled = true;
        meterCanvasObject.SetActive(true);

        inSwingMode = true;

        // Lock player controls
        _playerController.canInput = false;

        // Set camera to swing state
        cameraFollowScript.SetSwingState(true);

        if (playerMoveCoroutine == null)
        {
            playerMoveCoroutine = StartCoroutine(MovePlayerToStancePos());
        }
    }

    IEnumerator MovePlayerToStancePos()
    {
        Vector3 targetPosition;
        if (ragdolledPlayer != null) // move target pos to ragdolled player if nearby
        {
            targetPosition = ragdolledPlayer.transform.position + (-playerTransform.forward * 0.12f) + playerTransform.right * -.75f;
            Debug.Log("Moving to ragdolled player");
        }
        else // move taget pos to player's ball if no ragdolled player nearby
        {
            targetPosition = thisBall.transform.position + (-playerTransform.forward * 0.12f) + playerTransform.right * -.75f;
            Debug.Log("Moving to ball");

        }

        //targetPosition.y -= 0.12f;    // Instead of moving targ pos down, use a raycast to touch the ground
        // Perform a raycast downwards to find the ground position beneath the target position
        RaycastHit hit;
        if (Physics.Raycast(targetPosition, Vector3.down, out hit, 3))
        {
            targetPosition = hit.point; // Adjust the target position to the ground position
        }
        else
        {
            Debug.LogWarning("Failed to find ground beneath target position!");
        }

        // Define the duration over which to move the player
        float duration = 2.3f; // Adjust as needed

        // Store the initial position of the player
        Vector3 initialPosition = playerTransform.position;

        // Move the player smoothly over time
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            // Calculate the interpolation factor based on elapsed time
            float t = elapsedTime / duration;

            // Interpolate between the initial and target positions
            playerTransform.position = Vector3.Lerp(initialPosition, targetPosition, t);

            // Update the elapsed time
            elapsedTime += Time.deltaTime;

            yield return null;
        }

        // Ensure the player reaches the exact target position
        playerTransform.position = targetPosition;
    }

    void WaitingForSwing()  // Called by the animation event in Stance animation - not called from code
    {
        waitingForSwing = true;
    }
    void PerformSwing()     // Called by the animation event in Swing animation - not called from code
    {
        if (!IsOwner) return;
        if (ragdolled_player != null)
        {
            // still need to reset triggers since performSwingonPlayer does not exit swing mode (swing on player is handled in Update())
            ExitSwingMode();
            return;
        }

        Debug.Log("PerformSwing() on ball");
        PerformSwingOnBall();
    }

    void PerformSwingOnBall()
    {
        if (!IsOwner) return;
        // set to false to exit swing mode after animations finished
        waitingForSwing = false;

        // Add forces
        var dir = transform.forward + new Vector3(0, verticalAngle, 0);
        thisBallRb.AddForce(dir * swingForce * meterCanvas.GetComponent<PowerMeter>().GetPowerValue(), ForceMode.Impulse);
        thisBallMoving = true;

        // Play sound effect for swinging the ball
        AudioManager.instance.PlayOneShot(FMODEvents.instance.playerGolfSwing, _playerController.transform.position);

        // only count strokes if the game is active / not in pre-game lobby
        if (_playerController.IsActive)
        {
            PlayerData _currentPlayerData = _playerNetworkData.GetPlayerData();
            _currentPlayerData.strokes++;
            _playerNetworkData.StorePlayerState(_currentPlayerData);
            _uiManager.UpdateStrokesUI(_currentPlayerData.strokes);
        }

        ExitSwingMode();

    }

    void PerformSwingOnPlayer()
    {
        if (!IsOwner) return;

        // set waitingForSwing to false to exit swing mode after animations finished
        waitingForSwing = false;

        // calc direction
        var dir = transform.forward + new Vector3(0, verticalAngle, 0);
        // add forces
        Vector3 swingForceVector = dir * swingForce * meterCanvas.GetComponent<PowerMeter>().GetPowerValue();

        // Play sound effect for swinging the ball
        AudioManager.instance.PlayOneShot(FMODEvents.instance.playerGolfSwing, _playerController.transform.position);

        Debug.Log("force dir: " + dir);
        Debug.Log("force vector: " + swingForceVector);
        //ask the ragdolled player to add force on themselves
        if (ragdolled_player != null)
        {
            Debug.Log("PerformSwingOnPlayer() on ownerID " + ragdolled_player.GetComponent<NetworkObject>().OwnerClientId + " from client " + NetworkManager.Singleton.LocalClientId);
            AddForceToPlayerServerRpc(swingForceVector, ragdolled_player.GetComponent<NetworkObject>().OwnerClientId);
        }
        //AddForceToPlayerServerRpc(swingForceVector);


        // only count strokes if the game is active / not in pre-game lobby
        if (_playerController.IsActive)
        {
            PlayerData _currentPlayerData = _playerNetworkData.GetPlayerData();
            _currentPlayerData.strokes++;
            _playerNetworkData.StorePlayerState(_currentPlayerData);
            _uiManager.UpdateStrokesUI(_currentPlayerData.strokes);
        }

    }

    // Exit swing state without performing swing
    public void ExitSwingMode()
    {
        inSwingMode = false;

        meterCanvasObject.SetActive(false);

        // Allow ball to roll again
        //enableRotation();

        _playerController.canInput = true;
        cameraFollowScript.SetSwingState(false);
        // Make sure its no longer waiting for swing
        waitingForSwing = false;

        playerAnimator.ResetTrigger("Swing");
        playerAnimator.ResetTrigger("Stance");

        playerAnimator.CrossFade("Idle", 0.1f);
        StopCoroutine(playerMoveCoroutine);
        playerMoveCoroutine = null;
    }


    // Spawn and shooting rpcs

    [ServerRpc]
    void SpawnBallOnServerRpc(ulong ownerId)
    {
        Vector3 spawnPosition = new Vector3(94.2144241f + OwnerClientId * 2, 102.18f, -136.345001f + 1f); // spawn ball in front of player
        thisBall = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);
        thisBallRb = thisBall.GetComponent<Rigidbody>();
        //thisBallRb.velocity = playerTransform.forward * 10f; // Example velocity
        NetworkObject ballNetworkObject = thisBall.GetComponent<NetworkObject>();
        if (ballNetworkObject != null)
        {
            ballNetworkObject.SpawnWithOwnership(ownerId);
            //Debug.Log("Ball spawned for player " + ownerId);

        }

        //RemoveForces(); //  prevent ball from rolling
        //stopRotation();

        // Inform the client about the spawned projectile
        SpawnBallOnClientRpc(thisBall.GetComponent<NetworkObject>().NetworkObjectId);
    }

    [ClientRpc]
    void SpawnBallOnClientRpc(ulong ballId)
    {
        thisBall = NetworkManager.Singleton.SpawnManager.SpawnedObjects[ballId].gameObject;
        thisBallRb = thisBall.GetComponent<Rigidbody>();
    }

    // checks playerdata for final hole, if not, moves ball to next hole startig postiiton
    public void CheckForWin(PlayerData data)
    {
        if (data.currentHole > holeStartPositions.Length)
        {
            Debug.Log("Player " + data.playerID + " has won the game!");
            thisBall.SetActive(false);
        }
        else
        {
            thisBallRb.velocity = Vector3.zero;
            thisBallRb.angularVelocity = Vector3.zero; // maybe get rid of this ? sometimes get a warning
            Vector3 randStartPos;
            // if on first hole, space balls out by 2 units to match player start positions, otherwise spawn them only 1 unit apart
            if (data.currentHole == 1) randStartPos = holeStartPositions[0] + new Vector3(OwnerClientId * 2, 0, -1);
            else randStartPos = holeStartPositions[data.currentHole - 1] + new Vector3(OwnerClientId, 0, 0);

            MoveProjectileToPosition(randStartPos);
            Debug.Log("Ball for player " + data.playerID + " moved to " + randStartPos + " currentHole: " + data.currentHole);
        }
    }

    [ServerRpc]
    private void AddForceToPlayerServerRpc(Vector3 force, ulong playerID)
    {
        AddForceToPlayerClientRpc(force, playerID);
    }

    [ClientRpc]
    private void AddForceToPlayerClientRpc(Vector3 force, ulong playerID)
    {
        //get player object of ragdolled player
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            if (player.GetComponent<NetworkObject>().OwnerClientId == playerID && player.GetComponent<RagdollOnOff>().IsOwner)
            {
                player.GetComponent<RagdollOnOff>().AddForceToSelf(force);
            }
        }
    }

    [ServerRpc]
    private void setRagdolledPlayerServerRpc(int playerID)
    {
        setRagdolledPlayerClientRpc(playerID);
    }

    [ClientRpc]
    private void setRagdolledPlayerClientRpc(int playerID)
    {
        if (playerID == -1)
        {
            ragdolled_player = null;
            return;
        }

        //get player object of ragdolled player
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            if (player.GetComponent<NetworkObject>().OwnerClientId == (ulong)playerID)
            {
                ragdolled_player = player;
                Debug.Log("setRagdolledPlayerClientRpc(): Ragdolled player set to " + ragdolled_player != null ? ragdolled_player.GetComponent<NetworkObject>().OwnerClientId : "null");
            }
        }
    }



    public bool isInSwingState()
    {
        return inSwingMode;
    }

    // helper functions -------------------------------------------------------------------------------------------------------------


    public void ReturnBallToPlayer()
    {
        if (thisBall == null) return;

        RemoveForces(); //  prevent ball from rolling
        stopRotation();

        //  move ball to player
        thisBall.transform.position = transform.position + transform.up / 2 + transform.forward * 1f;
    }

    private void RemoveForces()
    {
        if (thisBall != null && thisBallRb != null)
        {
            if (IsOwner)
            {
                thisBallRb.velocity = Vector3.zero;
                thisBallRb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void stopRotation()
    {
        if (thisBall != null && thisBallRb != null)
        {
            if (IsOwner) thisBallRb.freezeRotation = true;
        }
    }

    //not using rotation to avoid ball moving too much down hills

    private void enableRotation()
    {
        if (thisBall != null && thisBallRb != null)
        {
            if (IsOwner) thisBallRb.freezeRotation = false;
        }
    }

    public void MoveProjectileToPosition(Vector3 destination)
    {
        if (thisBall == null) return;

        RemoveForces(); //  prevent ball from rolling
        stopRotation();

        Debug.Log("ball moved to: " + destination + "for player " + OwnerClientId);

        //  move ball to point
        thisBall.transform.position = destination;
    }

    #region  For New Input System
    public void HandleSwingStarted(InputAction.CallbackContext ctx)
    {
        if (!_isActive || !IsOwner || !isActiveAndEnabled)
            return;
        if (inSwingMode)
        {
            //TODO: do swing start logic here
            powerMeterRef.MouseDown = true;
        }
        else if (isCloseToRagdolledPlayer() || IsCloseToBall())
        {
            //TODO: do enter swing mode logic here
            StartSwingMode();
        }
        else
        {
            //TODO: do strike start logic here
            if (!_playerController.isStriking)
            {
                playerAnimator.SetBool("isStriking", true);
                playerAnimator.SetBool("justStriked", true);
            }
        }
    }
    public void HandleSwingCanceled(InputAction.CallbackContext ctx)
    {
        if (!_isActive || !IsOwner || !isActiveAndEnabled) return;
        if (inSwingMode && powerMeterRef.MouseDown)
        {
            //TODO: do swing logic here
            powerMeterRef.MouseDown = false;
            powerMeterRef.PlayerShot = true;
        }
    }
    public void HandleBallSpawnExitSwingStarted(InputAction.CallbackContext ctx)
    {
        if (!_isActive || !IsOwner || !isActiveAndEnabled)
            return;
        if (inSwingMode)
        {
            ExitSwingMode();
        }
        else if (thisBall != null)
        {
            ReturnBallToPlayer();

            if (!_playerController.IsActive) return; // do not count strokes if the player is in pre-game lobby

            PlayerData _currentPlayerData = _playerNetworkData.GetPlayerData();
            _currentPlayerData.strokes++;
            _playerNetworkData.StorePlayerState(_currentPlayerData);
            _uiManager.UpdateStrokesUI(_currentPlayerData.strokes);
        }
    }
    public void HandleBallSpawnExitSwingCanceled(InputAction.CallbackContext ctx)
    {
        if (!_isActive || !IsOwner || !isActiveAndEnabled) return;
    }
    #endregion

}
