using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerShoot : NetworkBehaviour
{
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float _projectileSpeed = 5;
    [SerializeField] private float _cooldown = 0.5f;
    [SerializeField] private float _spawnDist = 1f;
    [SerializeField] private float playerClubRange = 4f;

    private float _lastFired = float.MinValue;
    private GameObject projectileInstance;

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetMouseButton(0) && _lastFired + _cooldown < Time.time)
        {
            _lastFired = Time.time;
            var dir = transform.forward + transform.up / 2;
            ExecuteShoot(dir, OwnerClientId);
        }
    }

    [ServerRpc]
    private void RequestBallSpawnServerRpc(Vector3 dir, ulong ownerId)
    {
        projectileInstance = Instantiate(_projectilePrefab, transform.position + transform.forward * _spawnDist, Quaternion.identity);
        projectileInstance.GetComponent<NetworkObject>().SpawnWithOwnership(ownerId);

        // Inform the client about the spawned projectile
        SpawnedProjectileClientRpc(projectileInstance.GetComponent<NetworkObject>().NetworkObjectId);
    }

    [ClientRpc]
    private void SpawnedProjectileClientRpc(ulong objectId)
    {
        // Retrieve the projectile on the client side using its NetworkObjectId
        projectileInstance = NetworkManager.Singleton.SpawnManager.SpawnedObjects[objectId].gameObject;
    }

    // Shoot the ball or instantiate it if it doesn't exist
    private void ExecuteShoot(Vector3 dir, ulong ownerId)
    {
        if (projectileInstance == null)
        {
            RequestBallSpawnServerRpc(dir, OwnerClientId);
            return;
        }
        else
        {
            if (projectileInstance != null && Vector3.Distance(transform.position, projectileInstance.transform.position) < playerClubRange)
            {
                projectileInstance.GetComponent<Rigidbody>().AddRelativeForce(dir * _projectileSpeed, ForceMode.Impulse);
            }
        }
        // AudioSource.PlayClipAtPoint(_spawnClip, transform.position);
    }
}
