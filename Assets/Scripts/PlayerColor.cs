using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// code provided by https://github.com/Matthew-J-Spencer/Unity-Netcode-Starter/blob/main/Assets/_Game/_Scripts/Player/PlayerColor.cs

/// <summary>
/// Simple server-authoritative example.
/// Also shows reactive checks instead of per-frame checks
/// If you have questions, pop into discord and have a chat https://discord.gg/tarodev
/// </summary>
public class PlayerColor : NetworkBehaviour
{

    // color names for displaying winner
    private readonly Dictionary<Color, string> colorNames = new Dictionary<Color, string>()
    {
        { Color.black, "Black" },
        { Color.blue, "Blue" },
        { Color.clear, "Clear" },
        { Color.cyan, "Cyan" },
        { Color.gray, "Gray" },
        { Color.green, "Green" },
        { Color.magenta, "Magenta" },
        { Color.red, "Red" },
        { Color.white, "White" },
        { Color.yellow, "Yellow" }
    };

    public readonly NetworkVariable<Color> _netColor = new(readPerm: NetworkVariableReadPermission.Everyone);
    private readonly Color[] _colors = { Color.red, Color.blue, Color.green, Color.yellow, Color.cyan, Color.magenta, Color.gray };
    public MeshRenderer _renderer;
    public Color CurrentColor;

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
        CurrentColor = next;
        GetComponent<BasicPlayerController>().playerColor = colorNames[next];

        PlayerData currentData =  GetComponent<PlayerNetworkData>().GetPlayerData(); // current data will be default on first iteration
        if (IsOwner)
        {
            PlayerData preLobbyState = new()
            {
                playerID = OwnerClientId,
                playerColor = colorNames[next],
                currentHole = 0,
                strokes = 0,
                enemiesDefeated = 0,
                score = 0
            };

            // don't update player number if this is the first iteration
            if (currentData.playerColor == null) preLobbyState.playerNum = (ulong)Array.IndexOf(_colors, next);
            else preLobbyState.playerNum = currentData.playerNum;

            GetComponent<PlayerNetworkData>().StorePlayerState(preLobbyState); //send state to PlayerNetworkData

            Debug.Log($"Player {OwnerClientId} updated to color {colorNames[next]} and player number {preLobbyState.playerNum}");
        }

        //find all objects owned by this player
        GameObject[] BallObjects = GameObject.FindGameObjectsWithTag("Ball");

        foreach (GameObject playerObject in BallObjects)
        {
            playerObject.GetComponent<BallColor>().Activate();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner){ CommitNetworkColorServerRpc();}
        else{
            _renderer.material.color = _netColor.Value;
            GetComponent<BasicPlayerController>().playerColor = colorNames[_netColor.Value];
        }
    }

    public void CyclePlayerColor(){
        if (IsOwner) CyclePlayerColorServerRpc();
        else _renderer.material.color = _netColor.Value;
    }

    [ServerRpc]
    private void CommitNetworkColorServerRpc(){
        int playerNum = GameManager.instance.GetNumberOfPlayers(); //1st player = 0, 2nd player = 1, etc.
        _netColor.Value = _colors[playerNum];
    }

    [ServerRpc]
    private void CyclePlayerColorServerRpc(){
        int currentColorIndex = Array.IndexOf(_colors, _netColor.Value);
        int newColorIndex = (currentColorIndex + 1) % _colors.Length;
        _netColor.Value = _colors[newColorIndex];
        //Debug.Log($"Server updated client {OwnerClientId} to color {colorNames[_colors[newColorIndex]]}");

    }

}