using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;


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

    // Game UI Elements
    [Header("Game UI Elements")]
    [SerializeField] private TMP_Text _gamePlayerStrokesText;

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
    [SerializeField] private Slider _settingsSensitivitySlider;
    [SerializeField] private TMP_Dropdown _settingsLanguageDropdown;
    [SerializeField] private TMP_Text _holeCountText;

    // UIManager instance
    public static UIManager instance { get; private set; }

    private float settingsVolume = 0;
    private float settingsSensitivity = 5;
    private bool oneHandMode = false;
    private int language = 0;

    public bool titleScreenMode = true;
    public static bool isPaused { get; set; } = false;
    private bool localeActive = false;

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
        _pauseSettingsButton.onClick.AddListener(PauseStartSettings);
        _pauseTitleButton.onClick.AddListener(QuitLobbyReturnToTitle);

        // Settings Button Events
        _settingsApplyButton.onClick.AddListener(ApplySettings);
        _settingsBackButton.onClick.AddListener(DisableSettings);

        instance = this;

        RefreshDisplayList();
    }

    private void Start() { DisablePause(); DisableSettings(); EnableUI(UIState.Title); }

    // Title Screen Methods
    private void TitleStart() => EnableUI(UIState.Lobby);
    private void TitleSettings() => EnableSettings();

    // Lobby UI Methods
    private void PlayNow() => _lobbyManager.PlayNow();
    private void CreateLobby() => _lobbyManager.Create(_inputField.text, 5);
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

    // Pause UI Methods
    public void EnablePause() { isPaused = true; _pauseScreenUI.SetActive(true); }
    public void DisablePause() { isPaused = false; _pauseScreenUI.SetActive(false); _settingsScreenUI.SetActive(false); }
    public void EnableSettings() { LoadSettings(); _settingsScreenUI.SetActive(true); }
    public void DisableSettings() { _settingsScreenUI.SetActive(false); if (!titleScreenMode) { EnablePause(); } }
    public void PauseStartSettings() { _pauseScreenUI.SetActive(false); EnableSettings(); }

    // Quit lobby and return to title screen
    private async void QuitLobbyReturnToTitle()
    {
        await _lobbyManager.OnApplicationQuitCallback();
        ReturnToTitle();
    }

    // returns to rile screen
    public void ReturnToTitle()
    {
        titleScreenMode = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        DisablePause();
        EnableUI(UIState.Title);
    }

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

        _settingsSensitivitySlider.value = settingsSensitivity;
        _settingsLanguageDropdown.value = language;
    }

    public void ApplySettings()
    {
        Debug.Log("Applying settings");
        SettingsData sData = DataManager.instance.GetSettingsData();
        sData.cameraSensitivity = settingsSensitivity;
        sData.language = language;

        DataManager.instance.SetSettingsData(sData);

        Debug.Log("Is Locale active: " + localeActive);

        if (!localeActive) { StartCoroutine(SetLocale(language)); }
    }

    public void EnableUI(UIState state)
    {
        _titleScreenUI.SetActive(false);
        _lobbyUI.SetActive(false);

        switch (state)
        {
            case UIState.Title:
                _titleScreenUI.SetActive(true);
                break;
            case UIState.Lobby:
                _lobbyUI.SetActive(true);
                break;
        }
    }

    IEnumerator SetLocale(int _localeID)
    {
        Debug.Log("Locale entered: " + _localeID);
        localeActive = true;
        yield return LocalizationSettings.InitializationOperation;
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[_localeID];
        localeActive = false;
    }

    public async void RefreshDisplayList() // I added redundant checks here because sometimes lobby entry is found right before its deleted
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
                        _lobbyEntries[i].transform.Find("Players").GetComponent<TMP_Text>().text = ""; // clear previous player list
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

    public void ResetHUD()
    {
        _gamePlayerStrokesText.text = "Strokes: 0";
        _holeCountText.text = "Hole: 1";

    }

    public void UpdateStrokesUI(int strokes)
    {
        _gamePlayerStrokesText.text = "Strokes: " + strokes;
    }

    public void UpdateHoleCountText(int holeCount)
    {
        _holeCountText.text = "Hole: " + holeCount;
    }

}