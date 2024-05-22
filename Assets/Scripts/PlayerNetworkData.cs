using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Threading.Tasks;
using Unity.VisualScripting;


// Storing player data over the network ------------------------------------------------------------------------------------------------------------
public struct PlayerData : INetworkSerializable
{
    public ulong playerID;
    public string playerColor;
    public int currentHole;
    public int strokes;
    public ulong enemiesDefeated;
    public int score;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerID);
        // Handle null string for playerColor
        if (serializer.IsWriter)
        {
            string nonNullPlayerColor = playerColor ?? string.Empty;
            serializer.SerializeValue(ref nonNullPlayerColor);
        }
        else
        {
            serializer.SerializeValue(ref playerColor);
            playerColor ??= string.Empty;
        }

        serializer.SerializeValue(ref currentHole);
        serializer.SerializeValue(ref strokes);
        serializer.SerializeValue(ref enemiesDefeated);
        serializer.SerializeValue(ref score);
    }
}

// ------------------------------------------------------------------------------------------------------------

public class PlayerNetworkData : NetworkBehaviour
{
    private PlayerData _currentPlayerData;
    private int winner = -1;

    private NetworkVariable<PlayerData> _networkPlayerData = new NetworkVariable<PlayerData>(new PlayerData(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

    private void Awake()
    {
        _networkPlayerData.OnValueChanged += OnPlayerDataChanged;
    }
    public override void OnDestroy()
    {
        _networkPlayerData.OnValueChanged -= OnPlayerDataChanged;
        base.OnDestroy();
    }

    private void OnPlayerDataChanged(PlayerData prevData, PlayerData newData)
    {
        //Debug.Log("Player data changed - isOwner:  " + IsOwner + " - playerColor: " + newData.playerColor + " - playerID: " + newData.playerID + " - currentHole: " + newData.currentHole + " - strokes: " + newData.strokes + " - enemiesDefeated: " + newData.enemiesDefeated + " - score: " + newData.score);
        _currentPlayerData = newData;

        if (IsOwner)
        {
            if (IsServer) GameManager.instance.UpdatePlayerData(newData);
            else UpdateGameManagerServerRpc(newData);

            if (newData.currentHole < 1) return; // no need to check win during pre game           

            if (prevData.currentHole != newData.currentHole) // if current hole change, check player data for win or moves ball to next hole
            {
                winner = GetComponent<SwingManager>().CheckForWin(newData);
                if (winner >= 0)
                {
                    string winnerName = GetComponent<BasicPlayerController>().playerColor;
                    DisplayWinnerServerRpc(winnerName);
                }

                //  MAYBE WE SHOULD MOVE THE BALL TO THE NEXT HOLE HERE
            }

            UIManager.instance.UpdateStrokesUI(newData.strokes);
            UIManager.instance.UpdateHoleCountText(newData.currentHole);
        }
    }

    // player data / state ------------------------------------------------------------------------------------------------------------

    public void StorePlayerState(PlayerData data)
    {
        if (IsOwner) StorePlayerStateServerRpc(data);
        else _currentPlayerData = _networkPlayerData.Value;
    }

    public PlayerData GetPlayerData()
    {
        return _currentPlayerData;
    }

    public void RemovePlayerDataFromGameManager()
    {
        if (!IsOwner) return;
        RemovePlayerDataFromGameManagerServerRpc(OwnerClientId);
    }

    [ServerRpc]
    private void RemovePlayerDataFromGameManagerServerRpc(ulong playerID)
    {
        GameManager.instance.RemovePlayerData(playerID);
    }


    [ServerRpc]
    private void StorePlayerStateServerRpc(PlayerData data)
    {
        _networkPlayerData.Value = data;
    }

    // Display Winner / Exit ------------------------------------------------------------------------------------------------------------

    [ServerRpc]
    private void DisplayWinnerServerRpc(string winnerName)
    {
        DisplaywinnerClientRpc(winnerName);
    }

    [ClientRpc]
    private void DisplaywinnerClientRpc(string winnerName)
    {
        DisplayWinnerAndExit(winnerName);
    }

    private async void DisplayWinnerAndExit(string winnerName)
    {

        UIManager.instance.ActivateWinner($"Winner: {winnerName} !");
        await Task.Delay(8000); // wait 8 seconds before exiting lobby
        UIManager.instance.DeactivateWinner();
        await LobbyManager.Instance.PlayerExit();
    }

    // Update GameManager ------------------------------------------------------------------------------------------------------------

    [ServerRpc]
    private void UpdateGameManagerServerRpc(PlayerData data)
    {
        GameManager.instance.UpdatePlayerData(data);
    }


}
