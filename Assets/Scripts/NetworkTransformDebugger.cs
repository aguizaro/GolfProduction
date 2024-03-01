using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class NetworkTransformDebugger : NetworkBehaviour
{
    private NetworkTransform _networkTransform;

    private void Start()
    {
        _networkTransform = GetComponent<NetworkTransform>();

        if (_networkTransform == null)
        {
            Debug.LogError("NetworkTransform component not found on GameObject.");
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        Debug.Log("Current state of NetworkTransform:");

        // Print position
        Debug.Log("Position: " + _networkTransform.transform.position);

        // Print rotation
        Debug.Log("Rotation: " + _networkTransform.transform.rotation.eulerAngles);

        
    }
}
