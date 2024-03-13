using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class SwingManager : NetworkBehaviour
{
    public Transform playerTransform;
    public Camera swingCamera;
    public Animator playerAnimator;
    public GameObject ballPrefab;

    private bool inSwingMode = false;

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner || !isActiveAndEnabled)
        {
            return;
        }

        if (!inSwingMode && Input.GetKeyDown(KeyCode.Space) && IsCloseToBall())
        {
            StartSwingMode();
            PerformSwingOnServerRpc();
        }
        else if (inSwingMode && Input.GetKeyUp(KeyCode.Space))
        {
            PerformSwingOnServerRpc();
        }

        // Spawn a ball when pressing a certain key (e.g., 'B')
        if (Input.GetKeyDown(KeyCode.B))
        {
            SpawnBallOnServerRpc();
        }
    }

    bool IsCloseToBall()
    {
        // Implement logic to check if the player is close to the ball
        // You might use Vector3.Distance or some collider overlap checks
        return true; // Placeholder return value
    }

    void StartSwingMode()
    {
        inSwingMode = true;
        // Lock player controls
        // Activate swing camera
        swingCamera.enabled = true;
        // Trigger stance animation
        playerAnimator.SetTrigger("Stance");
    }

    void PerformSwing()
    {
        // Calculate swing force and direction
        // Apply the force to the ball
        // Trigger swing animation
        playerAnimator.SetTrigger("Swing");
        // Reset camera
        swingCamera.enabled = false;
        // Unlock player controls
        inSwingMode = false;
    }

    [ServerRpc]
    void PerformSwingOnServerRpc()
    {
        PerformSwing();
        PerformSwingOnClientRpc();
    }

    [ClientRpc]
    void PerformSwingOnClientRpc()
    {
        if (IsOwner)
            PerformSwing();
    }

    [ServerRpc]
    void SpawnBallOnServerRpc()
    {
        Vector3 spawnPosition = playerTransform.position + playerTransform.forward * 1f;        // Ball spawn distance should be tweaked
        GameObject newBall = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);
        newBall.GetComponent<Rigidbody>().velocity = playerTransform.forward * 10f; // Example velocity
        NetworkObject ballNetworkObject = newBall.GetComponent<NetworkObject>();
        if (ballNetworkObject != null)
        {
            ballNetworkObject.Spawn();
        }

        //RemoveForces(); //  prevent ball from rolling
        //stopRotation();
        
        // Inform the client about the spawned projectile
        SpawnBallOnClientRpc(newBall.GetComponent<NetworkObject>().NetworkObjectId);
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

    /*
    private void ReturnProjectileToPlayer()
    {
        if (_projectileInstance == null) return;

        RemoveForces(); //  prevent ball from rolling
        stopRotation();

        //  move ball to player
        _projectileInstance.transform.position = transform.position + transform.up / 2 + transform.forward * _spawnDist;
    }

    private void RemoveForces()
    {
        if (_projectileInstance != null && _projectileRb != null)
        {
            _projectileRb.velocity = Vector3.zero;
            _projectileRb.angularVelocity = Vector3.zero;
        }
    }

    private void stopRotation()
    {
        if (_projectileInstance != null && _projectileRb != null)
        {
            _projectileRb.freezeRotation = true;
        }
    }

    private void enableRotation()
    {
        if (_projectileInstance != null && _projectileRb != null)
        {
            _projectileRb.freezeRotation = false;
        }
    }
    */
    
}
