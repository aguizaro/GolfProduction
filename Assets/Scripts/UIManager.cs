using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using Unity.Netcode;


public enum UIState
{
    Title,
    Lobby,
    Game,
}
public enum MenuState
{
    None,
    Pause,
    Settings,
    Control,
}

public class UIManager : MonoBehaviour
{
    // Title Screen UI Elements
    [Header("Title Screen UI Elements")]
    [SerializeField] private GameObject _titleScreenUI;

    [SerializeField] private Button _titleStartButton;
    [SerializeField] private Button _titleSettingsButton;
    [SerializeField] private Button _titleQuitButton;
    [SerializeField] private Camera _mainCamera;

    // Lobby UI Elements
    [Header("Lobby UI Elements")]
    [SerializeField] private GameObject _lobbyUI;
    [SerializeField] private GameObject[] _lobbyEntries; //slots for displaying lobbies
    [SerializeField] private TMP_Text _lobbyJoinCodeText;
    [SerializeField] private TMP_Text _lobbyNameText;
    [SerializeField] private TMP_Text _lobbySignedInText;


    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private Button _createButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _refreshButton;
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
    [SerializeField] private Button _settingsControlButton;
    [SerializeField] private Slider _settingsSensitivitySlider;
    [SerializeField] private TMP_Dropdown _settingsLanguageDropdown;

    // Controls UI Elements

    [Header("Controls UI Elements")]
    [SerializeField] private GameObject _controlsScreenUI;
    [SerializeField] private Button _controlsBackButton;
    [Header("Other")]
    [SerializeField] private TMP_Text _holeCountText;

    [SerializeField] private TMP_Text _directionsTextP;
    [SerializeField] private TMP_Text _directionsTextL;

    // UIManager instance
    public static UIManager instance { get; private set; }

    private float settingsVolume = 0;
    private float settingsSensitivity = 5;
    private bool oneHandMode = false;
    private int language = 0;

    public bool titleScreenMode = true;
    public static bool isPaused { get; set; } = false;
    private bool localeActive = false;
    private Transform _cameraStartTransform;
    private MenuState menuState = MenuState.None;

    private async void Start()
    {
        // Title Button Events
        _titleStartButton.onClick.AddListener(TitleStart);
        _titleSettingsButton.onClick.AddListener(TitleSettings);
        _titleQuitButton.onClick.AddListener(TitleQuit);

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
        _settingsLanguageDropdown.onValueChanged.AddListener(ApplyLanguage);
        _settingsControlButton.onClick.AddListener(GotoControls);

        // Controls Button Events
        _controlsBackButton.onClick.AddListener(DisableControls);
        //Camera Start Position
        _cameraStartTransform = _mainCamera.transform;

        instance = this;

        InitializetLanguageDropdown();
        await LobbyManager.Instance.Authenticate(); //does not block main thread while being atuthenticated

        DisablePause(); DisableSettings(); EnableUI(UIState.Title); // start with title screen
    }


    // Title Screen Methods
    private void TitleStart()
    {
        LobbyManager.Instance.ResetQuit();
        RefreshDisplayList();
        EnableUI(UIState.Lobby);
    }
    private void TitleSettings() => EnableSettings();
    private void TitleQuit() => Application.Quit();

    // Lobby UI Methods
    private async void PlayNow()
    {
        DisableAllLobbyButtons();
        await LobbyManager.Instance.PlayNow();
        EnableAllLobbyButtons();
    }
    private async void CreateLobby()
    {
        DisableAllLobbyButtons();
        await LobbyManager.Instance.Create(_inputField.text, 5);
        EnableAllLobbyButtons();
    }
    private async void JoinLobby()
    {
        DisableAllLobbyButtons();
        await LobbyManager.Instance.Join(joinCode: _inputField.text);
        EnableAllLobbyButtons();
    }


    public void DeactivateUI() { _lobbyUI.SetActive(false); titleScreenMode = false; }
    public void DeactivateHUD()
    {
        // need thes checks here in case TMPro objects are destroyed during applicatino quit
        if (_gamePlayerStrokesText == null || _holeCountText == null || _lobbyJoinCodeText == null || _lobbyNameText == null) { return; }
        _gamePlayerStrokesText.gameObject.SetActive(false);
        _holeCountText.gameObject.SetActive(false);
        _lobbyJoinCodeText.gameObject.SetActive(false);
        _lobbyNameText.gameObject.SetActive(false);
    }
    public void ActivateHUD()
    {
        _gamePlayerStrokesText.gameObject.SetActive(true);
        _holeCountText.gameObject.SetActive(true);
        _lobbyJoinCodeText.gameObject.SetActive(true);
        _lobbyNameText.gameObject.SetActive(true);
    }
    public void DisplayCode(string code) => _lobbyJoinCodeText.text = code;
    public void DisplayLobbyName(string name) => _lobbyNameText.text = name;
    public async void DisplaySignedIn() => _lobbySignedInText.text = await LobbyManager.Instance.GetPlayerName();

    public string GetInputText() { return _inputField.text; }
    public void DisableUIText()
    {
        // need thes checks here in case TMPro objects are destroyed during applicatino quit
        if (_lobbyJoinCodeText == null || _lobbyNameText == null) { return; }
        _lobbyJoinCodeText.text = "";
        _lobbyNameText.text = "";
    }

    // Pause UI Methods
    public void EnablePause() { isPaused = true; _pauseScreenUI.SetActive(true); }
    public void DisablePause() { isPaused = false; _pauseScreenUI.SetActive(false); _settingsScreenUI.SetActive(false); }
    public void EnableSettings() { EnableMenu(MenuState.Settings); }
    public void DisableSettings() { _settingsScreenUI.SetActive(false); if (!titleScreenMode) { EnablePause(); } }
    public void PauseStartSettings() { _pauseScreenUI.SetActive(false); EnableSettings(); }
    public void EnableControls()
    {
        EnableMenu(MenuState.Control);
    }
    public void DisableControls()
    {
        Debug.Log("Disable Control");
        EnableMenu(MenuState.Settings);
    }

    // Quit lobby and return to title screen
    private async void QuitLobbyReturnToTitle()
    {
        await LobbyManager.Instance.TryQuitLobby();
        ReturnToTitle();
    }

    // returns to title screen
    public void ReturnToTitle()
    {
        DeactivateHUD();
        _mainCamera.transform.position = _cameraStartTransform.position;
        _mainCamera.transform.rotation = _cameraStartTransform.rotation;
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

    private void InitializetLanguageDropdown()
    {
        var currentLocale = LocalizationSettings.SelectedLocale;
        var locales = LocalizationSettings.AvailableLocales.Locales;
        for (int i = 0; i < locales.Count; i++)
        {
            if (locales[i] == currentLocale)
            {
                _settingsLanguageDropdown.value = i;
            }
        }
    }

    // temp ui to activate directions text
    public void ActivateDirections(bool isHost)
    {
        if (isHost) _directionsTextP.gameObject.SetActive(true);
        _directionsTextL.gameObject.SetActive(true);
    }

    public void DeactivateDirections()
    {
        _directionsTextP.gameObject.SetActive(false);
        _directionsTextL.gameObject.SetActive(false);
    }

    public void LoadSettings()
    {
        // Load settings data
        SettingsData sData = DataManager.instance.GetSettingsData();

        settingsVolume = sData.volume;
        settingsSensitivity = sData.cameraSensitivity;
        oneHandMode = sData.oneHandMode;
        language = sData.language;

        _settingsSensitivitySlider.value = settingsSensitivity;
        //_settingsLanguageDropdown.value = language;
    }

    public void ApplySettings()
    {
        Debug.Log("Applying settings");
        SettingsData sData = DataManager.instance.GetSettingsData();
        // apply all settings
        sData.cameraSensitivity = settingsSensitivity;
        // language is applied on when it is changed, so do not need to apply it now

        DataManager.instance.SetSettingsData(sData);

        DisableSettings();
    }

    public void ApplyLanguage(int lang)
    {
        SettingsData sData = DataManager.instance.GetSettingsData();
        sData.language = lang;
        DataManager.instance.SetSettingsData(sData);

        if (!localeActive) { StartCoroutine(SetLocale(language)); }
    }

    public void GotoControls()
    {
        EnableControls();
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

    public void EnableMenu(MenuState state)
    {
        _settingsScreenUI.SetActive(false);
        _controlsScreenUI.SetActive(false);
        menuState = state;

        switch (state)
        {
            case MenuState.Settings:
                LoadSettings();
                _settingsScreenUI.SetActive(true);
                break;
            case MenuState.Control:
                _controlsScreenUI.SetActive(true);
                break;
            default:
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
            List<LobbyEntry> foundLobbies = await LobbyManager.Instance.FindOpenLobbies();
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

                        _lobbyEntries[i].transform.Find("JoinLobbyButton").GetComponent<Button>().onClick.AddListener(() => HandleJoinLobbyButton(entry)); // join lobby, on button click
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


    ulong timesPressed = 0;
    bool isJoining = false;

    private async void HandleJoinLobbyButton(LobbyEntry entry)
    {
        Debug.Log($"pressed join button - count : {++timesPressed} - lobbyID: {entry.Id} lobbyName: {entry.Name}");

        if (isJoining) return;
        isJoining = true;
        DisableAllLobbyButtons();


        Debug.Log($"calling lobbymanager join() with id: {entry.Id} and name: {entry.Name}");
        bool success = await LobbyManager.Instance.Join(lobbyID: entry.Id);

        if (!success) Debug.LogWarning("Failed to join lobby");
        isJoining = false;
        EnableAllLobbyButtons();

    }

    public void ClearDisplayList()
    {
        // reset lobby display list
        foreach (GameObject entry in _lobbyEntries)
        {
            //remove all listeners
            entry.transform.Find("JoinLobbyButton").GetComponent<Button>().onClick.RemoveAllListeners();
            //disable entry
            entry.SetActive(false);
        }
    }

    private void DisableAllLobbyButtons()
    {
        _createButton.enabled = false;
        _joinButton.enabled = false;
        _playButton.enabled = false;
        _refreshButton.enabled = false;

        foreach (GameObject entry in _lobbyEntries)
        {
            entry.transform.Find("JoinLobbyButton").GetComponent<Button>().enabled = false;
        }
    }

    private void EnableAllLobbyButtons()
    {
        _createButton.enabled = true;
        _joinButton.enabled = true;
        _playButton.enabled = true;
        _refreshButton.enabled = true;

        foreach (GameObject entry in _lobbyEntries)
        {
            entry.transform.Find("JoinLobbyButton").GetComponent<Button>().enabled = true;
        }
    }

    public void ResetHUD()
    {
        _gamePlayerStrokesText.text = "0";
        _holeCountText.text = "0";

    }

    public void UpdateStrokesUI(int strokes)
    {
        _gamePlayerStrokesText.text = strokes.ToString();
    }

    public void UpdateHoleCountText(int holeCount)
    {
        _holeCountText.text = holeCount.ToString();
    }

}