using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public enum UIState
{
    Title,
    Lobby,
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
    private const int maxDisplayLen = 5; //5 lobby slots at a time

    // Pause UI Elements
    [Header("Pause UI Elements")]
    [SerializeField] private GameObject _pauseScreenUI;

    [SerializeField] private Button _pauseResumeButton;
    [SerializeField] private Button _pauseSettingsButton;
    [SerializeField] private Button _pauseTitleButton;

    // Settings UI Elements
    [Header("Settings UI Elements")]
    [SerializeField] private GameObject _settingsScreenUI;

    [SerializeField] private Button _settingsApplyButton;
    [SerializeField] private Button _settingsBackButton;
    [SerializeField] private Slider _settingsVolumeSlider;
    [SerializeField] private Slider _settingsSensitivitySlider;
    [SerializeField] private Toggle _settingsOneHandToggle;
    [SerializeField] private TMP_Dropdown _settingsLanguageDropdown;

    private float settingsVolume = 0;
    private float settingsSensitivity = 5;
    private bool oneHandMode = false;
    private int language = 0;

    private bool titleScreenMode = true;

    private void Awake()
    {
        // Title Button Events
        _titleStartButton.onClick.AddListener(TitleStart);
        _titleSettingsButton.onClick.AddListener(TitleSettings);

        // Lobby Button Events
        _createButton.onClick.AddListener(CreateLobby);
        _joinButton.onClick.AddListener(JoinLobby);
        _playButton.onClick.AddListener(PlayNow);
        _refreshButton.onClick.AddListener(RefreshDisplayList);

        // Pause Button Events
        _pauseResumeButton.onClick.AddListener(DisablePause);
        //_pauseSettingsButton.onClick.AddListener();
        //_pauseTitleButton.onClick.AddListener();

        // Settings Button Events
        _settingsApplyButton.onClick.AddListener(ApplySettings);
        _settingsBackButton.onClick.AddListener(DisableSettings);

        RefreshDisplayList();
    }

    private void Start() { DisablePause(); DisableSettings(); EnableUI(UIState.Title); }

    // Title Screen Methods
    private void TitleStart() => EnableUI(UIState.Lobby);
    private void TitleSettings() => EnableSettings();

    // Lobby UI Methods
    private void PlayNow() => _lobbyManager.PlayNow();
    private void CreateLobby() => _lobbyManager.Create(_inputField.text, 6);
    private void JoinLobby() => _lobbyManager.Join(joinCode: _inputField.text);


    public void DeactivateUI() { _lobbyUI.SetActive(false); Debug.Log("Deactivated Lobby UI: " + _lobbyUI.activeSelf); titleScreenMode = false; }
    public void DisplayCode(string code) => _lobbyJoinCodeText.text = code;
    public void DisplayLobbyName(string name) => _lobbyNameText.text = name;
    public async void DisplaySignedIn() => _lobbySignedInText.text = await _lobbyManager.GetPlayerName();

    public string GetInputText() { return _inputField.text; }
    public void DisableUIText()
    {
        _lobbyJoinCodeText.text = "";
        _lobbyNameText.text = "";
    }

    //public async void RefreshDisplayList() // I added redundant checks here because sometimes lobby entry is found right before its deleted
    
    // Pause UI Methods
    public void EnablePause() => _pauseScreenUI.SetActive(true);
    public void DisablePause() => _pauseScreenUI.SetActive(false);
    public void EnableSettings() { LoadSettings(); _settingsScreenUI.SetActive(true); }
    public void DisableSettings() { _settingsScreenUI.SetActive(false); if (!titleScreenMode) { EnablePause(); } }

    // Settings UI Methods
    public void SetVolumeSlider(float value) => settingsVolume = value;
    public void SetSensitivitySlider(float value) => settingsSensitivity = value;
    public void SetOneHandModeToggle(bool value) => oneHandMode = value;
    public void SetLanguageDropdown(int value) => language = value;

    public void LoadSettings()
    {
        // Load settings data
        SettingsData sData = DataManager.instance.GetSettingsData();

        settingsVolume = sData.volume;
        settingsSensitivity = sData.cameraSensitivity;
        oneHandMode = sData.oneHandMode;
        language = sData.language;

        _settingsVolumeSlider.value = settingsVolume;
        _settingsSensitivitySlider.value = settingsSensitivity;
        _settingsOneHandToggle.isOn = oneHandMode;
        _settingsLanguageDropdown.value = language;
    }

    public void ApplySettings()
    {
        SettingsData sData = DataManager.instance.GetSettingsData();
        sData.volume = settingsVolume;
        sData.cameraSensitivity = settingsSensitivity;
        sData.oneHandMode = oneHandMode;
        sData.language = language;

        DataManager.instance.SetSettingsData(sData);
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
        try
        {
            ClearDisplayList();
            List<LobbyEntry> foundLobbies = await _lobbyManager.FindOpenLobbies();
            if (foundLobbies.Count == 0)
            {
                Debug.Log("No lobbies found");
                return;
            }

            // iterate through found lobbies and display each in respective lobbyEntry slot
            int i = 0;
            foreach (LobbyEntry entry in foundLobbies)
            {
                Debug.Log($"Found {entry.Name} with code: {entry.LobbyType} with {entry.SpotsAvailable} spots left");
                if (i < maxDisplayLen)
                {
                    if (entry != null)
                    {
                        _lobbyEntries[i].SetActive(true);
                        _lobbyEntries[i].transform.Find("LobbyName").GetComponent<TMP_Text>().text = entry.Name; // display lobby name
                        _lobbyEntries[i].transform.Find("SpotsAvailable").GetComponent<TMP_Text>().text = $"Spots Available: {entry.SpotsAvailable}"; // display lobby availability
                    }

                    if (entry.Players.Count > 0)
                    {
                        // display each player
                        int playerIndex = 0;
                        string delim = "";
                        foreach (var p in entry.Players)
                        {
                            _lobbyEntries[i].transform.Find("Players").GetComponent<TMP_Text>().text += $"{delim}Player{++playerIndex}: {p.Data["PlayerName"].Value}";
                            delim = "\n";
                        }

                        _lobbyEntries[i].transform.Find("JoinLobbyButton").GetComponent<Button>().onClick.AddListener(() => _lobbyManager.Join(lobbyID: entry.Id)); // join lobby, on button click
                    }
                }

                i++;
            }

        }
        catch (Exception e)
        {
            Debug.LogWarning("Error refreshing lobby list: " + e.Message);
        }
    }

    public void ClearDisplayList()
    {
        // reset lobby display list
        foreach (GameObject entry in _lobbyEntries)
        {
            entry.SetActive(false);
        }


    }

}