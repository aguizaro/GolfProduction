using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class UIManager : MonoBehaviour
{

    [SerializeField] private GameObject _UItoDeactivate;
    [SerializeField] private GameObject[] _lobbyEntries;
    [SerializeField] private TMP_Text _joinCodeText;
    [SerializeField] private TMP_Text _lobbyNameText;
    [SerializeField] private TMP_Text _signedInText;


    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private Button _createButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _refreshButton;

    [SerializeField] private LobbyManager _lobbyManager;
    private const int maxDisplayLen= 5; //5 lobby slots at a time

    private void Awake()
    {
        _createButton.onClick.AddListener(CreateLobby);
        _joinButton.onClick.AddListener(JoinLobby);
        _playButton.onClick.AddListener(PlayNow);
        _refreshButton.onClick.AddListener(RefreshDisplayList);

        RefreshDisplayList();
    }

    private void PlayNow() => _lobbyManager.PlayNow();
    private void CreateLobby() => _lobbyManager.Create(_inputField.text, 6);
    private void JoinLobby() => _lobbyManager.Join(joinCode: _inputField.text);


    public void DeactivateUI() => _UItoDeactivate.SetActive(false);
    public void DisplayCode(string code) => _joinCodeText.text = code;
    public void DisplayLobbyName(string name) => _lobbyNameText.text = name;
    public async void DisplaySignedIn() => _signedInText.text = await _lobbyManager.GetPlayerName();

    public string GetInputText() { return _inputField.text; }
    public void DisableUIText()
    {
        _joinCodeText.text = "";
        _lobbyNameText.text = "";
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