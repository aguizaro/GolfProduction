using System.Collections.Generic;
using UnityEngine;
using UnityTimer;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    public static GameManager instance { get; private set; }

    private Timer _strokeTimer;

    // Game Manager Logic
    private bool gameStarted = false; // Set to true once player is ready to begin playing the game
    private float strokeTimerDuration = 10.0f;
    private float _currentStrokeTime;
    private int _nextStrokeTimerCheckpoint;

    // Network variables
    private NetworkVariable<float> _networkStrokeTime = new NetworkVariable<float>(10.0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Hole data
    int currentHole = 1;

    public override void OnNetworkSpawn()
    {
        instance = this;

        if (IsServer)
        {
            SetStrokeTimerCheckpoint();
            StartStrokeTimer();
        }

        _networkStrokeTime.OnValueChanged += OnStrokeTimeChanged;
    }

    public override void OnDestroy()
    {
        _networkStrokeTime.OnValueChanged -= OnStrokeTimeChanged;
    }

    private void OnStrokeTimeChanged(float prevTime, float newTime)
    {
        _currentStrokeTime = newTime;

        if (IsOwner)
        {
            // BLANK
        }
    }

    private void StartStrokeTimer()
    {
        _strokeTimer = Timer.Register(
            duration: strokeTimerDuration,
            onComplete: () => OnStrokeTimerTimeout(),
            onUpdate: (variable) => TimerUpdateCheck()
        );
    }

    private void SetStrokeTimerCheckpoint()
    {
        int roundedFloat = Mathf.FloorToInt(strokeTimerDuration);

        if (roundedFloat == strokeTimerDuration) { _nextStrokeTimerCheckpoint = roundedFloat - 1; } // If the stroke timer is a whole nunber
        else { _nextStrokeTimerCheckpoint = roundedFloat; } // If the stroke timer is a float
    }

    private void TimerUpdateCheck()
    {
        if (!IsServer) return;

        // Only change the value of the stroke time for clients after every second instaed of every frame
        if (_strokeTimer.GetTimeRemaining() <= _nextStrokeTimerCheckpoint)
        {
            _networkStrokeTime.Value = _nextStrokeTimerCheckpoint;

            if (_nextStrokeTimerCheckpoint > 0) { _nextStrokeTimerCheckpoint -= 1; } // Decrement checkpoint if it's currently nonzero
        }
    }

    // Logic Setters
    public void SetGameStarted() { gameStarted = true; StartStrokeTimer(); }

    // Logic Getters
    public bool GetGameStarted() => gameStarted;
    public float GetStrokeTimerTimeLeft() => _strokeTimer.GetTimeRemaining();

    // Timer signals
    private void OnStrokeTimerTimeout()
    {
        //Debug.Log("Done!");
    }


    // Player Data Management ------------------------------------------------------------------------------------------------------------

    private Dictionary<ulong, PlayerData> playersData = new Dictionary<ulong, PlayerData>();

    public void UpdatePlayerData(PlayerData data)
    {
        UpdateData(data);
    }

    public void RemovePlayerData(ulong playerID)
    {
        RemoveData(playerID); // remove data on all clients (including host)
    }

    private void UpdateData(PlayerData playerData)
    {
        //check for existing player data
        if (playersData.ContainsKey(playerData.playerID))
        {
            playersData[playerData.playerID] = playerData;
            Debug.Log(" GameManager: Player data updated for player: " + playerData.playerID + " - " + playerData.playerColor + " - " + playerData.currentHole + " - " + playerData.strokes + " - " + playerData.score + " - " + playerData.enemiesDefeated);
        }
        else
        {
            playersData.Add(playerData.playerID, playerData);
            Debug.Log(" GameManager: Player data created for player: " + playerData.playerID + " - " + playerData.playerColor + " - " + playerData.currentHole + " - " + playerData.strokes + " - " + playerData.score + " - " + playerData.enemiesDefeated);
        }

        Debug.Log(" GameManager: Players data count: " + playersData.Count);

        UpdateScoreboard();
    }

    private void RemoveData(ulong playerID)
    {
        if (playersData.ContainsKey(playerID))
        {
            playersData.Remove(playerID);
            Debug.Log(" GameManager: Player data removed for player: " + playerID + " - count: " + playersData.Count);
        }

        UpdateScoreboard();
    }

    private void UpdateScoreboard()
    {
        foreach (var player in NetworkManager.Singleton.ConnectedClientsList)
        {
            Debug.Log(" GameManager: Updating scoreboard for player: " + player.ClientId);
            Debug.Log(player.PlayerObject);
            Debug.Log(player.PlayerObject.gameObject);
            Debug.Log(player.PlayerObject.gameObject.GetComponent<PlayerScoreboard>());
            player.PlayerObject.gameObject.GetComponent<PlayerScoreboard>().AddScoreboardData(playersData);
        }
    }


}
