using System;
using Unity.Netcode;
using UnityEngine;

public class BallColor : NetworkBehaviour
{
    public readonly NetworkVariable<Color> _netColor = new();
    public MeshRenderer _renderer;

    private void Awake()
    {
        _netColor.OnValueChanged += OnValueChanged;
    }

    public override void OnDestroy()
    {
        _netColor.OnValueChanged -= OnValueChanged;
        base.OnDestroy();
    }

    private void OnValueChanged(Color prev, Color next)
    {
        _renderer.material.color = next;
    }

    public void Activate()
    {
        if (IsOwner)
        {
            Debug.Log("ball owned by " + OwnerClientId + " looking for player");
            Color playerColor = GetOwnersColor();
            CommitNetworkColorServerRpc(playerColor);
        }
        else
        {
            _renderer.material.color = _netColor.Value;
            Debug.Log("client: " + NetworkManager.Singleton.LocalClientId + " IsConnectedClient: " + NetworkManager.Singleton.IsConnectedClient + "\nis not owner: " + OwnerClientId + " - setting color to " + _netColor.Value);
        }
    }

    [ServerRpc]
    private void CommitNetworkColorServerRpc(Color color)
    {
        _netColor.Value = color;
        Debug.Log("ServerRpc: Ball color set to " + color);
    }

    private Color GetOwnersColor()
    {
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject playerObject in playerObjects)
        {
            Debug.Log("Player " + playerObject.GetComponent<NetworkObject>().OwnerClientId + " found - isOwner: " + playerObject.GetComponent<NetworkObject>().IsOwner + " - is local player: " + playerObject.GetComponent<NetworkObject>().IsLocalPlayer);
            if (playerObject.GetComponent<NetworkObject>().IsOwner && playerObject.GetComponent<NetworkObject>().OwnerClientId == OwnerClientId)
            {
                Color playerColor = playerObject.GetComponent<PlayerColor>()._netColor.Value;
                Debug.Log("Player " + OwnerClientId + " color is " + playerColor);
                return playerColor;
            }
        }
        Debug.LogError("Could not find player object with OwnerClientId: " + OwnerClientId);
        // orange
        return new Color(1.0f, 0.5f, 0.0f);
    }

}