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





    // Authentication --------------------------------------------------------------------------------------------------------------
    private async Task Authenticate()
    {
        var options = new InitializationOptions();

#if UNITY_EDITOR
        // Remove this if you don't have ParrelSync installed. 
        // It's used to differentiate the clients, otherwise lobby will count them as the same
        options.SetProfile(ClonesManager.IsClone() ? "Clone" : "Primary");
        Debug.Log(ClonesManager.IsClone() ? "user: " + "Clone" : "user: Primary");

#endif

        await UnityServices.InitializeAsync(options);

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            _playerId = AuthenticationService.Instance.PlayerId;

            _playerName = await AuthenticationService.Instance.GetPlayerNameAsync();
            _UIManager.DisplaySignedIn();
            Debug.Log("Signed in as: " + _playerName);
        }


    }

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

            Debug.Log($"lobby response: count: {lobbies.Results.Count}");

            List<LobbyEntry> _foundLobbies = new List<LobbyEntry>();
            foreach (Lobby found in lobbies.Results)
            {
                Debug.Log($"Found:\nName: {found.Name}\n  ID: {found.Data[LobbyTypeKey].Value}\n  Available Slots: {found.AvailableSlots}\n Host ID:{found.HostId}");
                _foundLobbies.Add(new LobbyEntry(found.Name, found.Id, found.Data[LobbyTypeKey].Value, found.AvailableSlots, found.Players));
                foreach (Player p in found.Players)
                {
                    Debug.Log($"Player ID: {p.Id}");
                    if (p.Data != null) {
                        foreach (var data in p.Data)
                        {
                            Debug.Log($"Player data - {data.Key} : {data.Value} ");
                        }
                    }
                }
                foreach (var data in found.Data)
                {
                    Debug.Log($"Lobby data - {data.Key} : {data.Value} ");
                }
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
            ConnectedLobby = await TryQuick() ?? await CreateLobby(defaultName, maxLobbySize);

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
    public async void Join(string joinCode= null, string lobbyID = null)
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
    // join lobby using code and start game
    private async Task JoinGameWithcode(string joinCode)
    {
        try
        {
            Debug.Log("Joining WITH CODE");
            ConnectedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(joinCode);

            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "No Lobby Found using code: " + joinCode);

            // If we found one, grab the relay allocation details

            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(ConnectedLobby.Data[RelayJoinCodeKey].Value);

            // configure unity tranport to use websockets for webGL support
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, _encrptionType));
            NetworkManager.Singleton.GetComponent<UnityTransport>().UseWebSockets = true;

            Debug.Log("Starting Client");
            // Join the game room as a client
            NetworkManager.Singleton.StartClient();

            // Initialize Game
            Debug.Log("starting game");
            StartGame();

        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to join lobby: {e.Message}");
        }
    }

    private async Task JoinGameWithID(string lobbyId)
    {
        try
        {
            Debug.Log("Joining WITH ID");
            ConnectedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);

            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "No Lobby Found using ID: " + lobbyId);

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
            ConnectedLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
            StartCoroutine(PullUpdatesCoroutine(ConnectedLobby.Id, 1));

            string relayJoinCode = ConnectedLobby.Data[RelayJoinCodeKey].Value;
            JoinAllocation joinAllocation = await JoinRelay(relayJoinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, _encrptionType));
            NetworkManager.Singleton.GetComponent<UnityTransport>().UseWebSockets = true;

            NetworkManager.Singleton.StartClient();

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

            Debug.Log("Started Create Lobby");
            // Create a relay allocation and generate a join code to share with the lobby
            Allocation allocation = await AllocateRelay(Math.Min(maxLobbySize, maxPlayers));

            Debug.Log("Created Alloc: " + allocation.AllocationId);

            bool t = NetworkManager.Singleton.enabled;

            Debug.Log(t);

            // configure unity tranport to use websockets for webGL support
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "wss"));

            Debug.Log("Set RelayServer Data");

            NetworkManager.Singleton.GetComponent<UnityTransport>().UseWebSockets = true;

            Debug.Log("Set WSS");

            string relayJoinCode = await GetRelayJoinCode(allocation);
            string lobbyType = "Golf Lobby";

            // Lobby options for a public lobby
            var options = new CreateLobbyOptions
            {   // add join code as a public (anyone can grab this code)
                IsPrivate = false,
                Data = new Dictionary<string, DataObject> {
                    { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                    { LobbyTypeKey, new DataObject(DataObject.VisibilityOptions.Public, lobbyType) }
                }
            };

            string defaultName = "MyLobby " + (DateTime.Now).ToString("MMdd_HHmmss");
            string name = (lobbyName != null && lobbyName.Length > 0) ? lobbyName : defaultName;

            ConnectedLobby = await LobbyService.Instance.CreateLobbyAsync(name, maxPlayers, options);

            Debug.Log("Created public Lobby with lobbyCode " + ConnectedLobby.LobbyCode);

            // Send a heartbeat every 15 seconds to keep the room alive
            StartCoroutine(HeartbeatLobbyCoroutine(ConnectedLobby.Id, 15));

            // Pull updates from the lobby every second
            StartCoroutine(PullUpdatesCoroutine(ConnectedLobby.Id, 1));

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


    private static IEnumerator HeartbeatLobbyCoroutine(string lobbyId, float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            Lobbies.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    private static IEnumerator PullUpdatesCoroutine(string lobbyId, float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            LobbyService.Instance.GetLobbyAsync(lobbyId);
            yield return delay;
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
        if (_currentMapInstance != null) Destroy(_currentMapInstance);
    }

    private void OnDestroy()
    {
        try
        {
            StopAllCoroutines();
            if (ConnectedLobby != null)
            {
                if (ConnectedLobby.HostId == _playerId) Lobbies.Instance.DeleteLobbyAsync(ConnectedLobby.Id);
                else Lobbies.Instance.RemovePlayerAsync(ConnectedLobby.Id, _playerId);
            }
        }
        catch (Exception e)
        {
            Debug.Log($"Error shutting down lobby: {e}");
        }
    }
}