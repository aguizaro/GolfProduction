using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Threading.Tasks;
using NUnit.Framework;
using System;

public class SwingManager : NetworkBehaviour
{
    public Transform playerTransform;
    public Animator playerAnimator;
    public enum FrameFrozenTarget
    {
        TimeScale,
        Animator,
    }
    public FrameFrozenTarget frameFrozenTarget = FrameFrozenTarget.Animator;
    [UnityEngine.Range(0f, 5f)] public float frameFrozenDuration = 0.25f;
    [UnityEngine.Range(0f, 1f)] public float frameFrozenRate = 0.05f;
    public Vector3 cameraShakeOffset = new Vector3(0f, 0.1f, 0f);
    [UnityEngine.Range(0f, 1f)] public float cameraShakeDuration = 0.33f;
    public float cameraShakeRemainingTime;
    public GameObject ballPrefab;
    public GameObject VFXPrefab;
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
        new Vector3(-58.73f, 10.28f, 72.09f),
        new Vector3(47.87f, 10.28f, 27.41f),
        new Vector3(-100.62f, 10.28f, -69.2f)
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
        if (IsOwner) UnregisterActions();
    }

    public override void OnNetworkSpawn()
    {
        powerMeter = GetComponentInChildren<Slider>();
        powerMeterRef = meterCanvas.GetComponent<PowerMeter>();
        _uiManager = GameObject.Find("Canvas").GetComponent<UIManager>();

        _ragdollOnOff = GetComponent<RagdollOnOff>();

        _playerNetworkData = GetComponent<PlayerNetworkData>();
        _playerController = GetComponent<BasicPlayerController>();
        if (!IsOwner) return;
        RegisterActions();
    }

    public void RegisterActions()
    {
        _playerController.gameplayActionMap["Swing"].started += HandleSwingStarted;
        _playerController.gameplayActionMap["Swing"].canceled += HandleSwingCanceled;
        _playerController.gameplayActionMap["Ball Spawn/Exit Swing"].started += HandleBallSpawnExitSwingStarted;
        _playerController.gameplayActionMap["Ball Spawn/Exit Swing"].canceled += HandleBallSpawnExitSwingCanceled;
    }
    public void UnregisterActions()
    {
        _playerController.gameplayActionMap["Swing"].started -= HandleSwingStarted;
        _playerController.gameplayActionMap["Swing"].canceled -= HandleSwingCanceled;
        _playerController.gameplayActionMap["Ball Spawn/Exit Swing"].started -= HandleBallSpawnExitSwingStarted;
        _playerController.gameplayActionMap["Ball Spawn/Exit Swing"].canceled -= HandleBallSpawnExitSwingCanceled;
    }
    // Update is called once per frame
    void Update()
    {
        if (!_isActive || !IsOwner || !isActiveAndEnabled)
            return;

        // Check if player is already in swing mode
        if (inSwingMode)
        {
            //check if input is enabled - input is enabled when player unpauses game - this prevents player from moving while in swing mode
            _playerController.canMove = false;
            _playerController.canLook = false;

            // Check if the ragdolled player has gotten up
            if (ragdolled_player != null)
            {
                if (!ragdolled_player.GetComponent<RagdollOnOff>().IsRagdoll())
                {

                    setRagdolledPlayerServerRpc(-1);
                    ExitSwingMode();
                }
                // otherwise if player shoots, perform swing on player
                else if (powerMeterRef.GetShotStatus() == true && waitingForSwing)
                {
                    playerAnimator.SetTrigger("Swing");
                    playerAnimator.ResetTrigger("Stance");

                    Debug.Log("performing swing animation on ragdolled player");

                    //PerformSwingOnPlayer();
                }
            }
            else if (powerMeterRef.GetShotStatus() == true && waitingForSwing)
            {
                // Start swing animation, when the club is halfway through the swing it will call PerformSwing() - from the animation event
                playerAnimator.SetTrigger("Swing");
                playerAnimator.ResetTrigger("Stance");
            }

            return;
        }

    }

    bool IsCloseToBall()
    {
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
        //find nearby players
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            // dont find ourselves
            if (player.GetComponent<NetworkObject>().OwnerClientId != OwnerClientId)
            {
                float distance = Vector3.Distance(player.transform.position, transform.position);
                if (player.GetComponent<RagdollOnOff>().IsRagdoll() && !player.GetComponent<RagdollOnOff>().alreadyLaunched && !player.GetComponent<RagdollOnOff>().beingLaunched)
                {
                    if (distance <= 2f)
                    {
                        setRagdolledPlayerServerRpc((int)player.GetComponent<NetworkObject>().OwnerClientId);
                        ragdolledPlayer = player;
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

        RemoveForces(); //  prevent ball from rolling
        stopRotation();

        // Enable power meter
        // meterCanvas.GetComponent<Canvas>().enabled = true;
        meterCanvasObject.SetActive(true);

        inSwingMode = true;

        // Lock player controls
        _playerController.canMove = false;
        _playerController.canLook = false;

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
        }
        else // move taget pos to player's ball if no ragdolled player nearby
        {
            targetPosition = thisBall.transform.position + (-playerTransform.forward * 0.12f) + playerTransform.right * -.75f;
        }

        // Perform a raycast downwards to find the ground position beneath the target position
        RaycastHit hit;
        if (Physics.Raycast(targetPosition, Vector3.down, out hit, 3))
        {
            targetPosition = hit.point; // Adjust the target position to the ground position
        }
        else
        {
            //Debug.LogWarning("Failed to find ground beneath target position!");
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

        Debug.Log($"PerformSwing() called from player: {OwnerClientId}, is ragdolled player null? {ragdolled_player == null}");
        // if (ragdolled_player != null)
        // {
        //     // still need to reset triggers since performSwingonPlayer does not exit swing mode (swing on player is handled in Update())
        //     ExitSwingMode();
        //     return;
        // }
        if (ragdolled_player == null && thisBall != null)
        {
            Debug.Log("Perform Swing on Ball");
            PerformSwingOnBall();
        }
        else
        {
            Debug.Log("Perform Swing on Player");
            PerformSwingOnPlayer();
        }
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
        PoolManager.Release(VFXPrefab, thisBall.transform.position, Quaternion.LookRotation(dir.normalized * -1f));
        // SetThatBallServerRpc(OwnerClientId);
        VFXServerRpc(thisBall.transform.position, Quaternion.LookRotation(dir.normalized * -1f));
        StartCoroutine(FrameFrozen(frameFrozenDuration));
        //TODO:add camera shake
        StartCoroutine(ShakeCamera());

        // Play sound effect for swinging the ball
        AudioManager.instance.PlayOneShotForAllClients(FMODEvents.instance.playerGolfSwing, _playerController.transform.position, IsOwner);

        // only count strokes if the game is active / not in pre-game lobby
        if (_playerController.IsActive)
        {
            PlayerData _currentPlayerData = _playerNetworkData.GetPlayerData();
            _currentPlayerData.strokes++;
            _playerNetworkData.StorePlayerState(_currentPlayerData);
        }

        ExitSwingMode();
    }

    IEnumerator FrameFrozen(float duration)
    {
        float ffTimeer = duration;
        while (ffTimeer > 0)
        {
            ffTimeer = Mathf.Clamp(ffTimeer - Time.fixedDeltaTime, 0, duration);
            if (frameFrozenTarget == FrameFrozenTarget.TimeScale)
            {
                Time.timeScale = Mathf.Lerp(frameFrozenRate, 1f, 1f - ffTimeer / duration);
            }
            else
            {
                playerAnimator.speed = Mathf.Lerp(frameFrozenRate, 1f, 1f - ffTimeer / duration);
            }
            yield return null;
        }
    }

    IEnumerator ShakeCamera()
    {
        // if(Camera.main != null)
        //     Debug.Log("Camera.main is founded.");
        cameraShakeRemainingTime = cameraShakeDuration;
        bool sign = true;
        while (cameraShakeRemainingTime > 0)
        {
            Camera.main.transform.position += sign ? cameraShakeOffset : -cameraShakeOffset;
            sign = !sign;
            cameraShakeRemainingTime -= Time.deltaTime;
            yield return null;
        }
        Camera.main.transform.position += sign ? -cameraShakeOffset : Vector3.zero;
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
        AudioManager.instance.PlayOneShotForAllClients(FMODEvents.instance.playerGolfSwing, _playerController.transform.position, IsOwner);

        Debug.Log("force dir: " + dir);
        Debug.Log("force vector: " + swingForceVector);
        //ask the ragdolled player to add force on themselves
        if (ragdolled_player != null)
        {
            AddForceToPlayerServerRpc(swingForceVector, ragdolled_player.GetComponent<NetworkObject>().OwnerClientId);
        }
        //AddForceToPlayerServerRpc(swingForceVector);


        // only count strokes if the game is active / not in pre-game lobby
        if (_playerController.IsActive)
        {
            PlayerData _currentPlayerData = _playerNetworkData.GetPlayerData();
            _currentPlayerData.strokes++;
            _playerNetworkData.StorePlayerState(_currentPlayerData);
        }

        ExitSwingMode();
    }

    // Exit swing state without performing swing
    public void ExitSwingMode()
    {
        inSwingMode = false;

        meterCanvasObject.SetActive(false);

        // Allow ball to roll again
        //enableRotation();

        _playerController.canInput = true;
        _playerController.canMove = true;
        _playerController.canLook = true;
        cameraFollowScript.SetSwingState(false);
        // Make sure its no longer waiting for swing
        waitingForSwing = false;

        playerAnimator.ResetTrigger("Swing");
        playerAnimator.ResetTrigger("Stance");

        playerAnimator.CrossFade("Idle", 0.1f);
        if (playerMoveCoroutine != null)
        {
            StopCoroutine(playerMoveCoroutine);
            playerMoveCoroutine = null;
        }
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

    [ServerRpc]
    void VFXServerRpc(Vector3 pos, Quaternion quaternion)
    {
        VFXClientRpc(pos, quaternion);
    }
    [ClientRpc]
    void VFXClientRpc(Vector3 pos, Quaternion quaternion)
    {
        PoolManager.Release(VFXPrefab, pos, quaternion);
    }

    [ServerRpc]
    void SetThatBallServerRpc(ulong ownerId)
    {
        SetThatBallClientRpc(ownerId);
    }

    [ClientRpc]
    void SetThatBallClientRpc(ulong ownerId)
    {
        GameObject[] balls = GameObject.FindGameObjectsWithTag("Ball");
        foreach (GameObject ball in balls)
        {
            if (ball.GetComponent<NetworkObject>().OwnerClientId == ownerId)
            {
                thisBall = ball;
                // thisBallRb = thisBall.GetComponent<Rigidbody>();
                break;
            }
        }
    }

    // checks playerdata for final hole, if not, moves ball to next hole startig postiiton
    // returns -1 if no win, returns playerID if win
    public int CheckForWin(PlayerData data)
    {
        if (data.currentHole > holeStartPositions.Length)
        {
            thisBallRb.gameObject.SetActive(false);
            return (int)data.playerID;
        }

        thisBallRb.velocity = Vector3.zero;
        thisBallRb.angularVelocity = Vector3.zero; // maybe get rid of this ? sometimes get a warning
        Vector3 randStartPos;
        // if on first hole, space balls out by 2 units to match player start positions, otherwise spawn them only 1 unit apart
        if (data.currentHole == 1) randStartPos = holeStartPositions[0] + new Vector3(OwnerClientId * 2, 0, -1);
        else randStartPos = holeStartPositions[data.currentHole - 1] + new Vector3(OwnerClientId, 0, 0);

        MoveProjectileToPosition(randStartPos);
        return -1;

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
            if (ragdolled_player != null)
            {
                ragdolled_player.GetComponent<RagdollOnOff>().beingLaunched = false;
                ragdolled_player = null;
            }
            return;
        }

        //get player object of ragdolled player
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            if (player.GetComponent<NetworkObject>().OwnerClientId == (ulong)playerID)
            {
                player.GetComponent<RagdollOnOff>().beingLaunched = true;
                ragdolled_player = player;
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

    public void RemoveForces()
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
        //  move ball to point
        thisBall.transform.position = destination;
    }

    #region  For New Input System
    public void HandleSwingStarted(InputAction.CallbackContext ctx)
    {
        if (!_isActive || !IsOwner || !isActiveAndEnabled || _ragdollOnOff.IsRagdoll() || !_playerController.canInput)
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

                // Play sound effect for swinging the ball
                AudioManager.instance.PlayOneShotForAllClients(FMODEvents.instance.playerGolfStrike, _playerController.transform.position, IsOwner);
            }
        }
    }
    public void HandleSwingCanceled(InputAction.CallbackContext ctx)
    {
        if (!_isActive || !IsOwner || !isActiveAndEnabled || _ragdollOnOff.IsRagdoll() || !_playerController.canInput) return;
        if (inSwingMode && powerMeterRef.MouseDown)
        {
            //TODO: do swing logic here
            powerMeterRef.MouseDown = false;
            powerMeterRef.PlayerShot = true;
        }
    }
    public void HandleBallSpawnExitSwingStarted(InputAction.CallbackContext ctx)
    {
        if (!_isActive || !IsOwner || !isActiveAndEnabled || !_playerController.canInput)
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
        }
    }
    public void HandleBallSpawnExitSwingCanceled(InputAction.CallbackContext ctx)
    {
        if (!_isActive || !IsOwner || !isActiveAndEnabled || !_playerController.canInput) return;
    }
    #endregion

}
