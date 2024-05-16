using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Threading.Tasks;


// Storing player data over the network ------------------------------------------------------------------------------------------------------------
public struct PlayerData : INetworkSerializable
{
    public ulong playerID;
    public int currentHole;
    public int strokes;
    public ulong enemiesDefeated;
    public int score;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerID);
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


    // Update local variable when network variable updates  ------------------------------------------------------------------------------------------------------------

    public override void OnNetworkSpawn()
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
        _currentPlayerData = newData;

        if (newData.currentHole < 1) return; // no need to check win during pre game

        if (IsOwner) // the following logic will be ran on all clients that are the owner of the player object SIMULTANEOUSLY
        {
            if (prevData.currentHole != newData.currentHole) // if current hole change, check player data for win or moves ball to next hole
            {
                winner = GetComponent<SwingManager>().CheckForWin(newData);
                if (winner >= 0)
                {
                    string winnerName = GetPlayerColor(newData.playerID);
                    Debug.Log("Winner: " + winner + " " + winnerName);
                    DisplayWinnerServerRpc(winnerName);
                }


                //  MAYBE WE SHOULD MOVE THE BALL TO THE NEXT HOLE HERE
            }

            UIManager.instance.UpdateStrokesUI(newData.strokes);
            UIManager.instance.UpdateHoleCountText(newData.currentHole);
        }
    }

    // public functions ------------------------------------------------------------------------------------------------------------

    // only owners should use this to send data to the server
    public void StorePlayerState(PlayerData data) //senderID will be used later
    {
        if (IsOwner)
        {
            PlayerData newData = new PlayerData()
            {
                playerID = data.playerID,
                currentHole = data.currentHole,
                strokes = data.strokes,
                enemiesDefeated = data.enemiesDefeated,
                score = data.score
            };

            // send data to server
            StorePlayerStateServerRpc(newData);
        }
        else
        {
            _currentPlayerData = _networkPlayerData.Value;
        }
    }

    public PlayerData GetPlayerData()
    {
        return _currentPlayerData;
    }


    // server rpcs ------------------------------------------------------------------------------------------------------------

    [ServerRpc]
    private void StorePlayerStateServerRpc(PlayerData data)
    {
        _networkPlayerData.Value = data;
    }

    public string GetPlayerColor(ulong playerID)
    {
        if (OwnerClientId == playerID)
        {
            Color selfColor = GetComponent<PlayerColor>().CurrentColor;
            Debug.Log("Self color: " + selfColor);
            return colorNames[selfColor];
        }
        else
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject player in players)
            {
                if (player.GetComponent<NetworkObject>().OwnerClientId == playerID)
                {
                    Color playerColor = player.GetComponent<PlayerColor>()._netColor.Value;
                    Debug.Log("Player found with ID: " + playerID + " color: " + playerColor);
                    return colorNames[playerColor];
                }
            }
            Debug.LogError("Player not found with ID: " + playerID);
            return "Unknown";
        }
    }

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

}
