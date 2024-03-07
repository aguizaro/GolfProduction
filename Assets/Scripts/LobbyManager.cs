//  References:
//  https://gist.github.com/Matthew-J-Spencer/a5ab1fb5a50465e300ea39d7cde85006
//  https://github.com/adammyhre/Unity-Multiplayer-Kart

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using Unity.Networking.Transport.Relay;
using System.Text.RegularExpressions;



#if UNITY_EDITOR
using ParrelSync;
#endif

public class LobbyEntry
{
    public string Name;
    public string Id;
    public int SpotsAvailable;
    public string LobbyType;
    public List<Player> Players;

    public LobbyEntry(string name, string id, string type, int numSpots, List<Player> players)
    {
        Name = name;
        Id = id;
        LobbyType = type;
        SpotsAvailable = numSpots;
        Players = players;
    }
}


public enum EncryptionType { DTLS, WSS }

public class LobbyManager : MonoBehaviour
{
    //[SerializeField] private GameObject _gameMap;
    [SerializeField] private Transform mainCameraTransform;
    [SerializeField] private UIManager _UIManager;

    [SerializeField] EncryptionType encryption = EncryptionType.WSS;
    [SerializeField] int maxLobbySize = 10;

    private const string RelayJoinCodeKey = "RelayJoinCode";
    private const string LobbyTypeKey = "LobbyType";
    private string _playerId;
    private string _playerName;
    public Lobby ConnectedLobby;
    private GameObject _currentMapInstance;
    private string _encrptionType => (encryption == EncryptionType.DTLS) ? "dtls" : "wss";

    private float lobbyUpdateTimer = 1.8f; //pull updates every x seconds to avoid rate limiting

    private bool isPinging = false; // this bool tracks if the host is currently pinging the lobby


    // Authentication --------------------------------------------------------------------------------------------------------------
    private async Task Authenticate(string playerName = null)
    {
        var options = new InitializationOptions();

#if UNITY_EDITOR
        // Remove this if you don't have ParrelSync installed. 
        // It's used to differentiate the clients, otherwise lobby will count them as the same
        options.SetProfile(ClonesManager.IsClone() ? "Clone" : "Primary");
        Debug.Log(ClonesManager.IsClone() ? "user: " + "Clone" : "user: Primary");

#endif
        // Set the profile name to the player's name if it's valid, otherwise use a random Unity provided name
        string profileName = (string.IsNullOrEmpty(playerName) || !Regex.Match(playerName, "^[a-zA-Z0-9_-]{1,30}$").Success) ? null : playerName;
        if (profileName != null) options.SetProfile(profileName);

        await UnityServices.InitializeAsync(options);

        if (AuthenticationService.Instance.IsSignedIn) return; //already signed in

        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        _playerId = AuthenticationService.Instance.PlayerId;
        _playerName = await AuthenticationService.Instance.GetPlayerNameAsync();

        _UIManager.DisplaySignedIn();
        Debug.Log("Signed in as: " + _playerName);


    }
    // Player Operations --------------------------------------------------------------------------------------------------------------

    // returns name of currently signed in player
    public async Task<string> GetPlayerName()
    {
        try
        {
            await Authenticate();
            return _playerName;
        }
        catch (AuthenticationException e)
        {
            Debug.LogWarning("Error Authenticating when getting player Name" + e.Message);
            return null;
        }
    }

    // checks if the player has authenticated and creates a player object with the player's name, otherwise authenticates and returns player
    private async Task<Player> CreatePlayer()
    {
        try
        {
            if (_playerId == null) await Authenticate();
            return new Player
            {
                Data = new Dictionary<string, PlayerDataObject> {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, _playerName) }
            }
            };
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to create player: {e.Message}");
            return null;
        }
    }

    private async void UpdatePlayerName(string newName)
    {
        try
        {
            await LobbyService.Instance.UpdatePlayerAsync(ConnectedLobby.Id, AuthenticationService.Instance.PlayerId, new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, newName) }
                }
            });

        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to update player name: {e.Message}");
        }
    }


    // Query Lobbies --------------------------------------------------------------------------------------------------------------

    public async Task<List<LobbyEntry>> FindOpenLobbies()
    {
        try
        {
            await Authenticate();

            QueryLobbiesOptions options = new QueryLobbiesOptions();
            options.Count = 25; //max results to return

            // filter for open lobbies only
            options.Filters = new List<QueryFilter>()
            {
                new QueryFilter(
                    field: QueryFilter.FieldOptions.AvailableSlots,
                    op: QueryFilter.OpOptions.GT,
                    value: "0")
            };

            // order by newest lobbies first
            options.Order = new List<QueryOrder>()
            {
                new QueryOrder(
                    asc: false,
                    field: QueryOrder.FieldOptions.Created)
            };

            QueryResponse lobbies = await Lobbies.Instance.QueryLobbiesAsync(options);

            // iterate through found lobbies and display each in respective lobbyEntry slot
            List<LobbyEntry> _foundLobbies = new List<LobbyEntry>();
            foreach (Lobby found in lobbies.Results)
            {
                Debug.Log($"Found:\nName: {found.Name}\n  ID: {found.Data[LobbyTypeKey].Value}\n  Available Slots: {found.AvailableSlots}\n Host ID:{found.HostId}");
                _foundLobbies.Add(new LobbyEntry(found.Name, found.Id, found.Data[LobbyTypeKey].Value, found.AvailableSlots, found.Players));
            }

            return _foundLobbies;

        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Error finding open lobbies: {e}");
            return null;
        }
    }



    // Play Now --------------------------------------------------------------------------------------------------------------
    public async void PlayNow()
    {
        try
        {
            await Authenticate();

            Debug.Log("Singed in as: " + await AuthenticationService.Instance.GetPlayerNameAsync());

            string defaultName = "QuickLobby " + (DateTime.Now).ToString("MMdd_HHmmss");
            ConnectedLobby = await TryQuick() ?? await CreateLobby(defaultName, maxLobbySize); //redundant assignment of ConnectedLobby - this assignment is only to allow null coalescing operator

            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "Lobby Error: No Lobby connected");

            _UIManager.DeactivateUI();

            Debug.Log("Connected lobby code: " + ConnectedLobby.LobbyCode);

        }
        catch (Exception e)
        {
            Debug.Log(e);
            return;
        }
    }

    // Attempts to quick join a lobby and return the lobby
    // null is returned if no lobbies are available for quick join
    private async Task<Lobby> TryQuick()
    {
        await QuickJoinLobby();
        if (ConnectedLobby == null || !NetworkManager.Singleton.IsClient) return null;

        // Initialize Game
        StartGame();

        return ConnectedLobby;
    }


    // Join --------------------------------------------------------------------------------------------------------------
    public async void Join(string joinCode = null, string lobbyID = null)
    {
        try
        {
            await Authenticate();

            if (joinCode != null)
                await JoinGameWithcode(joinCode);
            else if (lobbyID != null)
                await JoinGameWithID(lobbyID);
            else
                throw new LobbyServiceException(new LobbyExceptionReason(), "Lobby Error: No join code or lobby id specified");

            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "Lobby Error: No Lobby connected");

            _UIManager.DeactivateUI();


            Debug.Log("Connected lobby code: " + ConnectedLobby.LobbyCode);
        }
        catch (Exception e)
        {
            Debug.Log($"Failed to join lobby: {e.Message}");
        }

    }
    // join lobby using code and playerName + start game
    private async Task JoinGameWithcode(string joinCode, string playerName = null)
    {
        try
        {

            JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
            {
                Player = await CreatePlayer()
            };

            ConnectedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(joinCode, options);

            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "No Lobby Found using code: " + joinCode);

            StartCoroutine(PullUpdatesCoroutine(lobbyUpdateTimer));

            // If we found one, grab the relay allocation details

            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(ConnectedLobby.Data[RelayJoinCodeKey].Value);

            // configure unity tranport to use websockets for webGL support
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, _encrptionType));
            NetworkManager.Singleton.GetComponent<UnityTransport>().UseWebSockets = true;

            Debug.Log("Starting Client");
            // Join the game room as a client
            NetworkManager.Singleton.StartClient();
            StartCoroutine(WaitForNetworkConnection());

            // Initialize Game
            Debug.Log("starting game");
            StartGame();

        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to join lobby: {e.Message}");
        }
    }

    // join lobby using ID and playerName + start game
    private async Task JoinGameWithID(string lobbyId, string playerName = null)
    {
        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = await CreatePlayer()
            };

            ConnectedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);

            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "No Lobby Found using ID: " + lobbyId);

            StartCoroutine(PullUpdatesCoroutine(lobbyUpdateTimer));

            // If we found one, grab the relay allocation details

            Debug.Log("Connected Lobby: " + ConnectedLobby.Name);

            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(ConnectedLobby.Data[RelayJoinCodeKey].Value);

            Debug.Log("grabbed allocation from lobby: " + allocation.AllocationId);

            // configure unity tranport to use websockets for webGL support
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, _encrptionType));
            NetworkManager.Singleton.GetComponent<UnityTransport>().UseWebSockets = true;

            Debug.Log("Starting Client");
            // Join the game room as a client
            NetworkManager.Singleton.StartClient();
            StartCoroutine(WaitForNetworkConnection());

            // Initialize Game
            StartGame();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to join lobby: {e.Message}");
        }
    }



    // attempts quick joins a lobby and starts client
    // _connectedLobby is set to lobby, or null if it fails
    private async Task QuickJoinLobby()
    {
        try
        {
            QuickJoinLobbyOptions options = new QuickJoinLobbyOptions
            {
                Player = await CreatePlayer()
            };

            ConnectedLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "No Lobby Found");

            StartCoroutine(PullUpdatesCoroutine(lobbyUpdateTimer));

            string relayJoinCode = ConnectedLobby.Data[RelayJoinCodeKey].Value;
            JoinAllocation joinAllocation = await JoinRelay(relayJoinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, _encrptionType));
            NetworkManager.Singleton.GetComponent<UnityTransport>().UseWebSockets = true;

            NetworkManager.Singleton.StartClient();
            StartCoroutine(WaitForNetworkConnection());

        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to quick join lobby: {e.Message}");
            ConnectedLobby = null;
        }
    }


    // Create --------------------------------------------------------------------------------------------------------------
    public async void Create(string lobbyName, int lobbySize)
    {
        try
        {
            await Authenticate();

            await CreateLobby(lobbyName, lobbySize);

            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "Lobby Error: No Lobby connected");

            _UIManager.DeactivateUI();

            Debug.Log("Created lobby code: " + ConnectedLobby.LobbyCode);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return;
        }
    }


    //Creates a public lobby and sets it to instanse variable ConnectedLobby
    async Task<Lobby> CreateLobby(string lobbyName, int maxPlayers)
    {
        try
        {

            // Create a relay allocation and generate a join code to share with the lobby
            Allocation allocation = await AllocateRelay(Math.Min(maxLobbySize, maxPlayers));

            // configure unity tranport to use websockets for webGL support
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "wss"));
            NetworkManager.Singleton.GetComponent<UnityTransport>().UseWebSockets = true;

            string relayJoinCode = await GetRelayJoinCode(allocation);
            string lobbyType = "Golf Lobby";

            // Lobby options for a public lobby
            var options = new CreateLobbyOptions
            {   // add join code as a public (anyone can grab this code)
                IsPrivate = false,
                Data = new Dictionary<string, DataObject> {
                    { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                    { LobbyTypeKey, new DataObject(DataObject.VisibilityOptions.Public, lobbyType) },
                },
                Player = await CreatePlayer()
            };

            string defaultName = "MyLobby " + (DateTime.Now).ToString("MMdd_HHmmss");
            string name = (lobbyName != null && lobbyName.Length > 0) ? lobbyName : defaultName;

            ConnectedLobby = await LobbyService.Instance.CreateLobbyAsync(name, maxPlayers, options);

            Debug.Log("Created public Lobby with lobbyCode " + ConnectedLobby.LobbyCode);
            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "Lobby Error: No Lobby connected");

            // Send a heartbeat every 10 seconds to keep the room alive
            StartCoroutine(HeartbeatLobbyCoroutine(ConnectedLobby.Id, 10));
            StartCoroutine(PullUpdatesCoroutine(lobbyUpdateTimer));

            // Pull updates every x seconds to avoid rate limiting


            // Start the room. I'm doing this immediately, but maybe you want to wait for the lobby to fill up
            NetworkManager.Singleton.StartHost();

            // Initialize Game
            StartGame();

            return ConnectedLobby;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogFormat($"Failed creating a lobby: {e.Message}");
            return null;
        }
    }


    // Allocation --------------------------------------------------------------------------------------------------------------

    async Task<Allocation> AllocateRelay(int maxPlayers)
    {
        try
        {
            return await RelayService.Instance.CreateAllocationAsync(maxPlayers);
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Error allocating relay: {e.Message}");
            return null;
        }
    }


    // Relay --------------------------------------------------------------------------------------------------------------

    async Task<string> GetRelayJoinCode(Allocation allocation)
    {
        try
        {
            return await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Error getting relay join code: {e.Message}");
            return null;
        }
    }

    async Task<JoinAllocation> JoinRelay(string joinCode)
    {
        try
        {
            return await RelayService.Instance.JoinAllocationAsync(joinCode);
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Error joining relay: {e.Message}");
            return default;
        }
    }

    // Coroutines --------------------------------------------------------------------------------------------------------------

    private IEnumerator WaitForNetworkConnection()
    {
        Debug.Log("Wait for Network connection");

        while (!NetworkManager.Singleton.IsConnectedClient)
        {
            yield return new WaitForEndOfFrame();
        }
        // do something here to indicate that the client is connected and start making calls to the server -------

        Debug.Log("Connected to Network");
    }

    private static IEnumerator HeartbeatLobbyCoroutine(string lobbyId, float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            Lobbies.Instance.SendHeartbeatPingAsync(lobbyId);
            Debug.Log("Heartbeat Ping Sent by " + NetworkManager.Singleton.LocalClientId);
            yield return delay;
        }
    }

    // not a coroutine, but a method that calls itself every waitTimeSeconds in the update loop
    private IEnumerator PullUpdatesCoroutine(float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            GetLobbyUpdates();
            yield return delay;
        }
    }

    private async void GetLobbyUpdates()
    {
        try
        {
            var lobby = await Lobbies.Instance.GetLobbyAsync(ConnectedLobby.Id);
            if (lobby != null)
            {
                ConnectedLobby = lobby;
                Debug.Log($"Updated Lobby: {lobby.Name}");
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to update lobby: {e.Message}");
        }
    }


    // Gameplay --------------------------------------------------------------------------------------------------------------

    private void StartGame()
    {
        Debug.Log("STARTING GAME");

        _UIManager.DeactivateUI();
        _UIManager.DisplaySignedIn();
        _UIManager.DisplayCode(ConnectedLobby.LobbyCode);
        _UIManager.DisplayLobbyName(ConnectedLobby.Name);

        //_currentMapInstance = Instantiate(_gameMap);
    }

    private void EndGame()
    {
        _UIManager.DisableUIText();
    }


    // Leave Lobby --------------------------------------------------------------------------------------------------------------

    // removes current player (self) from lobby
    public void LeaveLobby()
    {
        try
        {
            if (ConnectedLobby == null) return;
            Lobbies.Instance.RemovePlayerAsync(ConnectedLobby.Id, _playerId);
            ConnectedLobby = null;
            Debug.Log("Left Lobby");

        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to leave lobby: {e.Message}");
        }
    }

    // method for host to kick players
    private void ServerKickPlayer(string playerId)
    {
        if (ConnectedLobby.HostId == _playerId)
        {
            Lobbies.Instance.RemovePlayerAsync(ConnectedLobby.Id, playerId);
            ConnectedLobby = null;
        }
    }


    // Application Quit --------------------------------------------------------------------------------------------------------------

    //  try to migrate host otherwise delete lobby if you are the host, otherwise leave the lobby on application quit
    public void OnApplicationQuitCallback()
    {

        if (ConnectedLobby != null)
        {
            if (ConnectedLobby.HostId == _playerId)
            {
                Debug.LogWarning("Host has left the lobby, deleting lobby");
                DeleteLobby();
            }
            else
            {
                Debug.LogWarning("Client Leaving Lobby");
                LeaveLobby();
            }
        }
    }

    // unity handles host migration automatically, just need to leave the lobby right ??????
    /*private void OnApplicationQuit()
    {
        //onApplicationQuitCallback();
        LeaveLobby();
        Debug.Log("Application Quit");
    }*/

    private void OnDestroy()
    {
        Debug.LogWarning("OnDestroy");
        OnApplicationQuitCallback();

    }


    // method for host to delete the current lobby
    private async void DeleteLobby()
    {
        try
        {
            if (ConnectedLobby == null) return;

            StopAllCoroutines();

            if (ConnectedLobby.HostId == _playerId) await Lobbies.Instance.DeleteLobbyAsync(ConnectedLobby.Id);
            ConnectedLobby = null;

        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to delete lobby: {e.Message}");
        }
    }

    // Update Loop --------------------------------------------------------------------------------------------------------------


    public bool me = false;
    private void Update()
    {
        if (ConnectedLobby != null && ConnectedLobby.HostId == _playerId)
        {
            if (!me)
            {
                me = true;
                Debug.LogWarning("I am the new host: " + NetworkManager.Singleton.LocalClientId);
            }

        }
        else
        {

        }
    }
}