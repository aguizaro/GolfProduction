using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerShoot : NetworkBehaviour
{
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float _projectileForce = 20f;
    [SerializeField] private float _cooldown = 0.5f;
    [SerializeField] private float _spawnDist = 1f;
    [SerializeField] private float playerClubRange = 4f;
    [SerializeField] private float verticalAngle = 0.50f;

    private PlayerNetworkData _playerNetworkData;
    private float _lastFired = float.MinValue;
    private RagdollOnOff _ragdollOnOff;
    private GameObject _projectileInstance;
    private Rigidbody _projectileRb;
    private bool isActive = false;
    private bool _projectileMoving = false;

    // On Start -------------------------------------------------------------------------------------------------------------
    public override void OnNetworkSpawn() { _playerNetworkData = GameObject.Find("ServerStateManager").GetComponent<PlayerNetworkData>(); }

    // Activation -------------------------------------------------------------------------------------------------------------
    public void Activate() { isActive = true; _ragdollOnOff = GetComponent<RagdollOnOff>(); }
    public void Deactivate() => isActive = false;

    // Update Loop -------------------------------------------------------------------------------------------------------------
    private void FixedUpdate()
    {
        if (!isActive) return; //prevent updates until player is fully activated
        if (UIManager.isPaused) return; //no shoot on pause
        if (_ragdollOnOff.IsRagdoll()) return; //no shoot when in ragdoll mode
        if (!IsOwner) return; //only owner can shoot

        if (Input.GetMouseButton(0) && _lastFired + _cooldown < Time.time)
        {
            _lastFired = Time.time;
            var dir = transform.forward + new Vector3(0, verticalAngle, 0);

            ExecuteShoot(dir, OwnerClientId);
        }

        // dev cheat key
        if (Input.GetKeyDown(KeyCode.F)) ReturnProjectileToPlayer();

        if (_projectileInstance == null) return;
        if (_projectileMoving && _projectileRb.velocity.magnitude < 0.1f && _projectileRb.angularVelocity.magnitude > 0)
        {
            _projectileMoving = false;
            Debug.Log("Ball stopped moving at " + _projectileInstance.transform.position + " when velocity droppped to " + _projectileRb.velocity.magnitude);
            stopRotation();
        }
    }

    // Spawn and Shooting RPCs -------------------------------------------------------------------------------------------------------------

    [ServerRpc]
    private void RequestBallSpawnServerRpc(ulong ownerId)
    {
        _projectileInstance = Instantiate(_projectilePrefab, transform.position + transform.up / 2 + transform.forward * _spawnDist, Quaternion.identity);
        _projectileInstance.GetComponent<NetworkObject>().SpawnWithOwnership(ownerId);
        _projectileRb = _projectileInstance.GetComponent<Rigidbody>();

        //  prevent ball from rolling
        RemoveForces();
        stopRotation();

        // Inform the client about the spawned projectile
        SpawnedProjectileClientRpc(_projectileInstance.GetComponent<NetworkObject>().NetworkObjectId);
    }

    [ClientRpc]
    private void SpawnedProjectileClientRpc(ulong objectId)
    {
        // Retrieve the projectile on the client side using its NetworkObjectId
        _projectileInstance = NetworkManager.Singleton.SpawnManager.SpawnedObjects[objectId].gameObject;
        _projectileRb = _projectileInstance.GetComponent<Rigidbody>();
    }

    // Shoot the ball or instantiate it if it doesn't exist
    private void ExecuteShoot(Vector3 dir, ulong ownerId)
    {
        if (_projectileInstance == null)
        {
            RequestBallSpawnServerRpc(OwnerClientId);
            return;
        }
        else
        {   // check if ball is close enough to player
            if (_projectileInstance != null && Vector3.Distance(transform.position, _projectileInstance.transform.position) < playerClubRange)
            {
                // allow ball to roll
                RemoveForces();
                enableRotation();

                _projectileInstance.GetComponent<Rigidbody>().AddForce(dir * _projectileForce, ForceMode.Impulse);
                _projectileMoving = true;

                // Display a raycast for debugging
                Debug.DrawRay(_projectileInstance.transform.position, dir * _projectileForce, Color.red, 100f);

                // Increment the number of strokes
                _playerNetworkData.IncrementStrokeCount(ownerId);
            }
        }
        // AudioSource.PlayClipAtPoint(_spawnClip, transform.position);
    }


    // helper functions -------------------------------------------------------------------------------------------------------------
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
}
