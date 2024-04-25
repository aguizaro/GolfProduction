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

    private PlayerNetworkData _playerNetworkData;
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
    private float swingForce = 50f;
    private bool first = true;

    [SerializeField] private float verticalAngle = 0.50f;

    public Vector3[] holeStartPositions = new Vector3[]
    {
        new Vector3(395.840759f, 71f, 321.73f),
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
        // spawn ball on activation
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


        // Spawn a ball when pressing a certain key (e.g., 'B')
        //if (Input.GetKeyDown(KeyCode.B) && (thisBall == null))
        //{
        //  SpawnBallOnServerRpc();
        //}

        if (Input.GetKeyDown(KeyCode.F) && (thisBall != null))
        {
            ReturnBallToPlayer();
            if (first)
            {
                first = false;
                return;
            }
            _playerController._currentPlayerState.strokes++;
            _playerNetworkData.StorePlayerState(_playerController._currentPlayerState);
            _uiManager.UpdateStrokesUI(_playerController._currentPlayerState.strokes);
        }

    }

    bool IsCloseToBall()
    {
        // Checks if the player is close enough to the ball and looking at it
        if (thisBall == null) return false;

        float distance = Vector3.Distance(playerTransform.position + Vector3.down, thisBall.transform.position);
        Debug.Log("DISTANCE IS: " + distance + " " + startSwingMaxDistance);

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
        if(_ragdollOnOff.IsRagdoll())
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
        Debug.Log(_playerController);
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


        // Increment the number of strokes? idk if this should be located here but prob

        _playerController._currentPlayerState.strokes++;
        _playerNetworkData.StorePlayerState(_playerController._currentPlayerState);
        _uiManager.UpdateStrokesUI(_playerController._currentPlayerState.strokes);

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
        Vector3 spawnPosition = new Vector3(394.55f + Random.Range(-1f, 1f), 70.7f, 0); // 322.09f is intended z
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
            Vector2 randStartPos = holeStartPositions[data.currentHole - 1] + new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
            MoveProjectileToPosition(randStartPos);
            Debug.Log("Hole " + (data.currentHole - 1) + " completed!\nMoving to next position " + thisBall.transform.position);
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

    private void enableRotation()
    {
        if (thisBall != null && thisBallRb != null)
        {
            if (IsOwner) thisBallRb.freezeRotation = false;
        }
    }

    public void SpawnProjectile(ulong ownerId)
    {
        if (!IsOwner) return; //redundnat check since this is a public function

        Vector3 ballSpawnPos = new Vector3(395.5f + Random.Range(-5, 5), 75f, 322.0f + Random.Range(-3, 3));
        Debug.Log("Spawning at: " + ballSpawnPos + "for " + ownerId);
        //RequestBallSpawnServerRpc(OwnerClientId, ballSpawnPos);
    }

    public void MoveProjectileToPosition(Vector3 destination)
    {
        if (thisBall == null) return;

        RemoveForces(); //  prevent ball from rolling
        stopRotation();

        Debug.Log("The given destination position: " + destination);

        //  move ball to point
        thisBall.transform.position = destination;
    }



}
