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
    public ulong playerID; // used for network (OwnerClientID)
    public ulong playerNum; // used for gameplay (player 0, player 1, player 2)
    public string playerName;
    public int currentHole;
    public int strokes;
    public ulong enemiesDefeated;
    public int score;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerID);
        serializer.SerializeValue(ref playerNum);
        // Handle null string for playerName
        if (serializer.IsWriter)
        {
            string nonNullplayerName = playerName ?? string.Empty;
            serializer.SerializeValue(ref nonNullplayerName);
        }
        else
        {
            serializer.SerializeValue(ref playerName);
            playerName ??= string.Empty;
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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsOwner){
            _currentPlayerData = _networkPlayerData.Value;
            Debug.Log($"playerDataSpawn: I am not the owner client {OwnerClientId} - CurrentPlayerData: name: " + _currentPlayerData.playerName + " - playerID: " + _currentPlayerData.playerID + " - playerNum: " + _currentPlayerData.playerNum);
            Debug.Log("playerDataSpawn: Non owner setting name tag to " + _currentPlayerData.playerName);
            // set this players nameTag to playerName
            transform.Find("NameTagCanvas").Find("NameTag").GetComponent<NameTagRotator>().UpdateNameTag(_currentPlayerData.playerName);
        }
    }

    public override void OnDestroy()
    {
        _networkPlayerData.OnValueChanged -= OnPlayerDataChanged;
        RemoveClientDataFromGameManager(OwnerClientId);
        base.OnDestroy();
    }

    private void OnPlayerDataChanged(PlayerData prevData, PlayerData newData)
    {
        Debug.Log($"Player data changed for {newData.playerName} - Hole: {newData.currentHole} - Strokes: {newData.strokes} - PlayerID: {newData.playerID} - PlayerNum: {newData.playerNum}");
        _currentPlayerData = newData;

        string playerColor = GetComponent<BasicPlayerController>().playerColor;
        if (prevData.currentHole != newData.currentHole && prevData.currentHole >= 1) UIManager.instance.DisplayNotification($"{newData.playerName} made hole {prevData.currentHole} in {newData.strokes} strokes", playerColor);
        if (prevData.playerName != newData.playerName) transform.Find("NameTagCanvas").Find("NameTag").GetComponent<NameTagRotator>().UpdateNameTag(newData.playerName);

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
                    string winnerName = newData.playerName;
                    DisplayWinnerServerRpc(winnerName);
                }

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

    public void RefreshPlayerData()
    {
        Debug.Log("Refreshing Player Data");
        if (IsOwner) StorePlayerStateServerRpc(_currentPlayerData);
    }

    public void RemoveClientDataFromGameManager(ulong clientID)
    {
        if (!IsOwner) return;
        RemovePlayerDataFromGameManagerServerRpc(clientID);
    }


    [ServerRpc]
    private void RemovePlayerDataFromGameManagerServerRpc(ulong clientID)
    {
        GameManager.instance.RemovePlayerData(clientID);
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
