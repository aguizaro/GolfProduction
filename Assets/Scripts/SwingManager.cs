using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class SwingManager : NetworkBehaviour
{
    public Transform playerTransform;
    public Animator playerAnimator;
    public GameObject ballPrefab;
    public StartCameraFollow cameraFollowScript;
    public BasicPlayerController playerControllerScript;

    [SerializeField]
    private float startSwingMaxDistance = 2f;   // The distance the player can be from their ball to start swing mode
    private bool inSwingMode = false;
    private bool waitingForSwing = false;
    private GameObject thisBall;    // Reference to this player's ball
    private Rigidbody thisBallRb;
    [SerializeField] private float swingForce = 20f;
    [SerializeField] private float verticalAngle = 0.50f;

    private bool thisBallMoving = false;


    // Update is called once per frame
    void Update()
    {
        if (!IsOwner || !isActiveAndEnabled)
        {
            return;
        }

        // Spawn a ball when pressing a certain key (e.g., 'B')
        if (Input.GetKeyDown(KeyCode.B) && (thisBall == null))
        {
            Debug.Log("Spawn ball");
            SpawnBallOnServerRpc();
        }

        // dev cheat key
        if (Input.GetKeyDown(KeyCode.F)) ReturnBallToPlayer();
        
        // Check if player is already in swing mode and waiting to swing
        if (inSwingMode && waitingForSwing)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) // Exit swing mode without performing swing
            {
                ExitSwingMode();
            }
            else if (Input.GetKeyDown(KeyCode.Space)) // Perform swing
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
        else if (inSwingMode && Input.GetKeyDown(KeyCode.Escape))
        {
            ExitSwingMode();
        }
    }


    bool IsCloseToBall()
    {
        // Checks if the player is close enough to the ball and looking at it
        if (thisBall == null) return false;

        float distance = Vector3.Distance(playerTransform.position, thisBall.transform.position);

        if (distance <= startSwingMaxDistance)
        {
            return true;
            // Check for line of sight: will be good for active ragdoll
            /*
            RaycastHit hit;
            // Send raycast from the camera's position and direction
            bool hasLineOfSight = Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, startSwingMaxDistance);

            if (hasLineOfSight && hit.collider.gameObject == thisBall)
            {
                return true;    // Player is close to ball and looking at it
            }
            */
        }

        return false; // Ball exists but player is not close enough/looking at it
    }

    void StartSwingMode()
    {
        Debug.Log("Swing State entered");
        inSwingMode = true;
        waitingForSwing = true;
        // Lock player controls
        playerControllerScript.DisableInput();
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
        thisBallRb.AddForce(dir * swingForce, ForceMode.Impulse);
        thisBallMoving = true;

/*
        // Increment the number of strokes? idk if this should be located here but prob

        playerControllerScript._currentPlayerState.strokes++;
        _playerNetworkData.StorePlayerState(playerControllerScript._currentPlayerState, ownerId);

        _uiManager.UpdateStrokesUI(playerControllerScript._currentPlayerState.strokes);
*/

        // Set camera to default state
        cameraFollowScript.SetSwingState(false);

        // Re enable player controls
        playerControllerScript.EnableInput();

        inSwingMode = false;
    }

    // Exit swing state without performing swing
    void ExitSwingMode()
    {
        inSwingMode = false;

        playerControllerScript.EnableInput();
        cameraFollowScript.SetSwingState(false);
        // Make sure its no longer waiting for swing
        waitingForSwing = false;
    }


    [ServerRpc]
    void SpawnBallOnServerRpc()
    {
        Vector3 spawnPosition = playerTransform.position + playerTransform.forward * 1f + Vector3.up * 0.5f;
        thisBall = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);
        thisBallRb = thisBall.GetComponent<Rigidbody>();
        //thisBallRb.velocity = playerTransform.forward * 10f; // Example velocity
        NetworkObject ballNetworkObject = thisBall.GetComponent<NetworkObject>();
        if (ballNetworkObject != null)
        {
            ballNetworkObject.Spawn();
        }

        //RemoveForces(); //  prevent ball from rolling
        //stopRotation();
        
        // Inform the client about the spawned projectile
        SpawnBallOnClientRpc(thisBall.GetComponent<NetworkObject>().NetworkObjectId);
    }

    [ClientRpc]
    void SpawnBallOnClientRpc(ulong ballId)
    {
        if (IsOwner)
            return;

        NetworkObject ballNetworkObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[ballId];
        if (ballNetworkObject != null)
        {
            GameObject ballGameObject = ballNetworkObject.gameObject;
            if (ballGameObject != null)
            {
                ballGameObject.GetComponent<NetworkObject>().Spawn();
            }
        }
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
