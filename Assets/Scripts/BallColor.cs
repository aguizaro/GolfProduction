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
            Color playerColor = GetOwnersColor();
            CommitNetworkColorServerRpc(playerColor);
        }
        else
        {
            _renderer.material.color = _netColor.Value;
        }
    }

    [ServerRpc]
    private void CommitNetworkColorServerRpc(Color color)
    {
        _netColor.Value = color;
    }

    private Color GetOwnersColor()
    {
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject playerObject in playerObjects)
        {
            if (playerObject.GetComponent<NetworkObject>().IsOwner && playerObject.GetComponent<NetworkObject>().OwnerClientId == OwnerClientId)
            {
                Color playerColor = playerObject.GetComponent<PlayerColor>()._netColor.Value;
                return playerColor;
            }
        }
        Debug.LogError("Could not find player object with OwnerClientId: " + OwnerClientId);
        // orange
        return new Color(1.0f, 0.5f, 0.0f);
    }

}