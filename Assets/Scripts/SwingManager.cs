using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

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
    private bool first = true;

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
    }
    // Update is called once per frame
    void Update()
    {
        if (!_isActive) return;

        if (!IsOwner || !isActiveAndEnabled)
        {
            return;
        }


        // Check if player is already in swing mode and waiting to swing
        if (inSwingMode && waitingForSwing)
        {
            if (Input.GetKeyDown(KeyCode.Space)) // Exit swing mode without performing swing
            {
                ExitSwingMode();
            }
            else if (powerMeterRef.GetShotStatus() == true) // Perform swing
            {
                PerformSwing();
            }
            return; // Don't execute further logic if waiting for swing
        }

        // Check for input to enter swing mode
        if (!inSwingMode && Input.GetKeyDown(KeyCode.Space) && IsCloseToBall())
        {
            Debug.Log("Called StartSwingMode()");
            StartSwingMode();
        }
        // Check for input to exit swing mode
        //else if (inSwingMode && Input.GetKeyDown(KeyCode.Space))
        //{
        //  ExitSwingMode();
        //}

        if (Input.GetKeyDown(KeyCode.F) && (thisBall != null))
        {
            ReturnBallToPlayer();
            if (first)
            {
                first = false;
                return;
            }

            if (!_playerController.IsActive) return; // do not count strokes if the player is in pre-game lobby

            PlayerData _currentPlayerData = _playerNetworkData.GetPlayerData();
            _currentPlayerData.strokes++;
            _playerNetworkData.StorePlayerState(_currentPlayerData);
            _uiManager.UpdateStrokesUI(_currentPlayerData.strokes);
        }

    }

    bool IsCloseToBall()
    {
        // Checks if the player is close enough to the ball and looking at it
        if (thisBall == null) return false;

        float distance = Vector3.Distance(playerTransform.position + Vector3.down, thisBall.transform.position);
        //Debug.Log("DISTANCE IS: " + distance + " " + startSwingMaxDistance);

        if (distance <= startSwingMaxDistance)
        {
            /*
            Ray ray = new Ray(transform.position, transform.forward);

            RaycastHit hit;
            if(Physics.Raycast(ray, out hit, 2f))
            {
                if (hit.collider.gameObject == thisBall)
                {
                    return true;
                }
            }
            */
            return true;
        }
        //Debug.DrawRay(transform.position, transform.forward * 3, Color.red, 0.5f);

        return false; // Ball exists but player is not close enough/looking at it
    }

    void StartSwingMode()
    {
        if (_ragdollOnOff.IsRagdoll())
        {
            return;
        }
        Debug.Log("Swing State entered");

        RemoveForces(); //  prevent ball from rolling
        stopRotation();

        // Enable power meter
        // meterCanvas.GetComponent<Canvas>().enabled = true;
        meterCanvasObject.SetActive(true);

        inSwingMode = true;
        waitingForSwing = true;

        // Lock player controls
        _playerController.DisableInput();

        // Set camera to swing state
        cameraFollowScript.SetSwingState(true);
        // Trigger stance animation
        playerAnimator.SetTrigger("Stance");
    }

    // perform swing and exit swing state, will need rpcs
    void PerformSwing()
    {
        // Calculate swing force and direction
        // Apply the force to the ball
        // Trigger swing animation
        playerAnimator.SetTrigger("Swing");
        // set waitingForSwing to false to exit swing mode after animations finished
        waitingForSwing = false;

        // Add forces
        var dir = transform.forward + new Vector3(0, verticalAngle, 0);
        thisBallRb.AddForce(dir * swingForce * meterCanvas.GetComponent<PowerMeter>().GetPowerValue(), ForceMode.Impulse);
        thisBallMoving = true;

        // only count strokes if the game is active / not in pre-game lobby
        if (_playerController.IsActive)
        {
            PlayerData _currentPlayerData = _playerNetworkData.GetPlayerData();
            _currentPlayerData.strokes++;
            _playerNetworkData.StorePlayerState(_currentPlayerData);
            _uiManager.UpdateStrokesUI(_currentPlayerData.strokes);
        }

        // Set camera to default state
        cameraFollowScript.SetSwingState(false);

        // Unlock player controls
        inSwingMode = false;

        ExitSwingMode();
    }

    // Exit swing state without performing swing, will need rpcs
    public void ExitSwingMode()
    {
        inSwingMode = false;

        meterCanvasObject.SetActive(false);

        // Allow ball to roll again
        //enableRotation();

        _playerController.EnableInput();
        cameraFollowScript.SetSwingState(false);
        // Make sure its no longer waiting for swing
        waitingForSwing = false;
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
            Debug.Log("Ball spawned for player " + ownerId);

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

    public bool isInSwingState()
    {
        return inSwingMode;
    }

    // helper functions -------------------------------------------------------------------------------------------------------------


    private void ReturnBallToPlayer()
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



}
