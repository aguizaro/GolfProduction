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
    private int _index;
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

        if (IsOwner)
        {
            PlayerData preLobbyState = new PlayerData
            {
                playerID = OwnerClientId,
                playerColor = colorNames[next],
                currentHole = 0,
                strokes = 0,
                enemiesDefeated = 0,
                score = 0
            };

            GetComponent<PlayerNetworkData>().StorePlayerState(preLobbyState);
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
        // Take note, RPCs are queued up to run.
        // If we tried to immediately set our color locally after calling this RPC it wouldn't have propagated
        if (IsOwner)
        {
            _index = (int)OwnerClientId;
            Color nextColor = GetNextColor();
            CommitNetworkColorServerRpc(nextColor);
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

    private Color GetNextColor()
    {
        return _colors[_index++ % _colors.Length];
    }

}