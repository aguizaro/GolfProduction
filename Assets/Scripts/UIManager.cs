using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public enum UIState
{
    Title,
    Lobby,
    Pause
}

public class UIManager : MonoBehaviour
{
    // Title Screen UI Elements
    [Header("Title Screen UI Elements")]
    [SerializeField] private GameObject _titleScreenUI;

    [SerializeField] private Button _titleStartButton;
    [SerializeField] private Button _titleSettingsButton;

    // Lobby UI Elements
    [Header("Lobby UI Elements")]
    [SerializeField] private GameObject _lobbyUI;
    [SerializeField] private GameObject[] _lobbyEntries;
    [SerializeField] private TMP_Text _lobbyJoinCodeText;
    [SerializeField] private TMP_Text _lobbyNameText;
    [SerializeField] private TMP_Text _lobbySignedInText;


    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private Button _createButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _refreshButton;

    [SerializeField] private LobbyManager _lobbyManager;
    private const int maxDisplayLen= 5; //5 lobby slots at a time

    private void Awake()
    {
        // Title Button Events
        _titleStartButton.onClick.AddListener(TitleStart);

        // Lobby Button Events
        _createButton.onClick.AddListener(CreateLobby);
        _joinButton.onClick.AddListener(JoinLobby);
        _playButton.onClick.AddListener(PlayNow);
        _refreshButton.onClick.AddListener(RefreshDisplayList);

        RefreshDisplayList();
    }

    private void Start() => EnableUI(UIState.Title);

    // Title Screen Methods
    private void TitleStart() => EnableUI(UIState.Lobby);

    // Lobby UI Methods
    private void PlayNow() => _lobbyManager.PlayNow();
    private void CreateLobby() => _lobbyManager.Create(_inputField.text, 6);
    private void JoinLobby() => _lobbyManager.Join(joinCode: _inputField.text);


    public void DeactivateUI() { _lobbyUI.SetActive(false); Debug.Log("Deactivated Lobby UI: " + _lobbyUI.activeSelf); }
    public void DisplayCode(string code) => _lobbyJoinCodeText.text = code;
    public void DisplayLobbyName(string name) => _lobbyNameText.text = name;
    public async void DisplaySignedIn() => _lobbySignedInText.text = await _lobbyManager.GetPlayerName();

    public string GetInputText() { return _inputField.text; }
    public void DisableUIText()
    {
        _lobbyJoinCodeText.text = "";
        _lobbyNameText.text = "";
    }

    public void EnableUI(UIState state)
    {
        _titleScreenUI.SetActive(false);
        _lobbyUI.SetActive(false);

        switch(state)
        {
            case UIState.Title:
                _titleScreenUI.SetActive(true);
                break;
            case UIState.Lobby:
                _lobbyUI.SetActive(true);
                break;
        }
    }

    public async void RefreshDisplayList()
    {
        ClearDisplayList();
        List<LobbyEntry> foundLobbies = await _lobbyManager.FindOpenLobbies();

        Debug.Log($"Found Lobbies: {foundLobbies.Count}");

        // iterate through found lobbies and display each in respective lobbyEntry slot
        int i = 0; 
        foreach(LobbyEntry entry in foundLobbies)
        {
            Debug.Log($"Found {entry.Name} with code: {entry.LobbyType} with {entry.SpotsAvailable} spots left");
            if (i < maxDisplayLen)
            {
                _lobbyEntries[i].SetActive(true);
                _lobbyEntries[i].transform.Find("LobbyName").GetComponent<TMP_Text>().text = entry.Name; // display lobby name
                _lobbyEntries[i].transform.Find("SpotsAvailable").GetComponent<TMP_Text>().text = $"Spots Available: {entry.SpotsAvailable}"; // display lobby availability

                // display each player
                int playerIndex = 0;    
                string delim = "";
                foreach (var p in entry.Players)
                {
                    _lobbyEntries[i].transform.Find("Players").GetComponent<TMP_Text>().text += $"{delim}Player{++playerIndex}: {p.Id}";
                    delim = "\n";
                }

                _lobbyEntries[i].transform.Find("JoinLobbyButton").GetComponent<Button>().onClick.AddListener(() => _lobbyManager.Join(lobbyID: entry.Id)); // join lobby, on button click
            }

            i++;
        }
    }

    public void ClearDisplayList()
    {
        // reset lobby display list
        foreach (GameObject entry in _lobbyEntries) {
            entry.SetActive(false);
        }

        
    }

}