using System.Collections;
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

    public override void OnNetworkSpawn() {
        instance = this;
        Debug.Log("Test");
        
        if (IsServer) {
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
        Debug.Log("Stroke time changed to: " + newTime);
        _currentStrokeTime = newTime;

        if (IsOwner)
        {
            // BLANK
        }

    }

    private void StartStrokeTimer()
    {
        Debug.Log("Starting timer");
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
    private void OnStrokeTimerTimeout() {
        Debug.Log("Done!");
    }
}
