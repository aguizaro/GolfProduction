
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

    [SerializeField] EncryptionType encryption = EncryptionType.DTLS;
    private const int maxPeerConnections = 5; // Server + 5 peers = 6 total players

    private const string RelayJoinCodeKey = "RelayJoinCode";
    private const string LobbyTypeKey = "LobbyType";

    private string _playerName;
    private string _playerId;
    private ulong _localClientId;

    private const string playerNameKey = "PlayerName";
    private const string playerIdKey = "PlayerId";
    private const string localClientIdKey = "LocalClientId";

    public Lobby ConnectedLobby;
    private string _encrptionType => (encryption == EncryptionType.DTLS) ? "dtls" : "wss";

    private ILobbyEvents ConnectedLobbyyEvents;
    private bool gameIsActive = false;

    private bool subscribedToNetworkManagerEvents = false;

    private bool isQuitting = false;
    private bool quitDone = false;

    // Authentication --------------------------------------------------------------------------------------------------------------
    public async Task Authenticate(string playerName = null)
    {
        if (UnityServices.State == ServicesInitializationState.Initializing || UnityServices.State == ServicesInitializationState.Initialized) return;

        var options = new InitializationOptions();

#if UNITY_EDITOR
        // Remove this if you don't have ParrelSync installed. 
        // It's used to differentiate the clients, otherwise lobby will count them as the same
        options.SetProfile(ClonesManager.IsClone() ? "Clone" : "Primary");
        //Debug.Log(ClonesManager.IsClone() ? "user: " + "Clone" : "user: Primary");

#endif
        // Set the profile name to the player's name if it's valid, otherwise use a random Unity provided name
        string profileName = (string.IsNullOrEmpty(playerName) || !Regex.Match(playerName, "^[a-zA-Z0-9_-]{1,30}$").Success) ? null : playerName;
        if (profileName != null) options.SetProfile(profileName);

        await UnityServices.InitializeAsync(options);

        if (AuthenticationService.Instance.IsSignedIn) return; //already signed in

        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        _playerId = AuthenticationService.Instance.PlayerId;
        _playerName = await AuthenticationService.Instance.GetPlayerNameAsync();
        _localClientId = NetworkManager.Singleton.LocalClientId;

        UIManager.instance.DisplaySignedIn();
        Debug.Log("Signed in as: " + _playerName);

        //ConnectionNotificationManager.Singleton.OnClientConnectionNotification += HandleClientConnectionNotification;

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
                { playerNameKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, _playerName) },
                { playerIdKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Private, _playerId) },
                { localClientIdKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Private, _localClientId.ToString()) }
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
                    { playerNameKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, newName) }
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
                //Debug.Log($"Found:\nName: {found.Name}\n  ID: {found.Data[LobbyTypeKey].Value}\n  Available Slots: {found.AvailableSlots}\n Host ID:{found.HostId}");
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
    public async Task PlayNow(string lobbyName = null, int maxPeers = maxPeerConnections)
    {
        try
        {
            await Authenticate();

            string defaultName = "QuickLobby " + DateTime.Now.ToString("mmss");
            lobbyName = (lobbyName != null && lobbyName.Length > 0) ? lobbyName : defaultName;

            ConnectedLobby = await TryQuick() ?? await Create(lobbyName, maxPeers); //redundant assignment of ConnectedLobby - this assignment is only to allow null coalescing operator

            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "Lobby Error: No Lobby connected");

            UIManager.instance.DeactivateUI();

        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return;
        }
    }

    // Attempts to quick join a lobby and return the lobby
    // null is returned if no lobbies are available for quick join
    private async Task<Lobby> TryQuick()
    {
        await QuickJoinLobby();
        if (ConnectedLobby == null || !NetworkManager.Singleton.IsClient) return null;

        return ConnectedLobby;
    }


    // Join --------------------------------------------------------------------------------------------------------------
    public async Task<bool> Join(string joinCode = null, string lobbyID = null)
    {
        try
        {
            await Authenticate();

            bool joinedSuccessful = false;

            if (joinCode != null)
                joinedSuccessful = await JoinGameWithcode(joinCode);
            else if (lobbyID != null)
                joinedSuccessful = await JoinGameWithID(lobbyID);
            else
                throw new LobbyServiceException(new LobbyExceptionReason(), "Lobby Error: No join code or lobby id specified");

            if (!joinedSuccessful) throw new LobbyServiceException(new LobbyExceptionReason(), "Join Lobby unsuccessful");

            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "Lobby Error: No Lobby connected");

            UIManager.instance.DeactivateUI();

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to join lobby: {e.Message}");
            return false;
        }

    }
    // join lobby using code and playerName + start game
    private async Task<bool> JoinGameWithcode(string joinCode, string playerName = null)
    {
        try
        {

            JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
            {
                Player = await CreatePlayer()
            };

            ConnectedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(joinCode, options);
            await SubscribeToLobbyEvents();
            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "No Lobby Found using code: " + joinCode);

            bool clientConnected = await StartClient();
            if (!clientConnected) throw new LobbyServiceException(new LobbyExceptionReason(), "Failed to start client");

            return true;

        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to join lobby: {e.Message}");
            return false;
        }
    }

    // join lobby using ID and playerName + start game
    private async Task<bool> JoinGameWithID(string lobbyId, string playerName = null)
    {
        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = await CreatePlayer()
            };

            ConnectedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
            await SubscribeToLobbyEvents();
            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "No Lobby Found using ID: " + lobbyId);

            bool clientConnected = await StartClient();
            if (!clientConnected) throw new LobbyServiceException(new LobbyExceptionReason(), "Failed to start client");

            return true;

        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to join lobby: {e.Message}");
            return false;
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
            await SubscribeToLobbyEvents();
            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "No Lobby Found");

            bool clientConnected = await StartClient();
            if (!clientConnected) throw new LobbyServiceException(new LobbyExceptionReason(), "Failed to start client");

        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to quick join lobby: {e.Message}");
        }
    }


    // Create --------------------------------------------------------------------------------------------------------------

    // creates a lobby with a given name and maxPeers (total players = maxPeers + 1 host)
    public async Task<Lobby> Create(string lobbyName, int maxPeers = maxPeerConnections)
    {
        try
        {
            await Authenticate();

            await CreateLobby(lobbyName, Math.Clamp(maxPeers, 1, maxPeerConnections)); //clamp lobby size to 2-6 players total (1 host + 1-5 peers)

            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "Lobby Error: No Lobby connected");

            bool hostConnected = await StartHost();
            if (!hostConnected) throw new Exception("Failed to start host");

            UIManager.instance.DeactivateUI();

            return ConnectedLobby;


        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return null;
        }
    }


    //Creates a public lobby and sets it to instanse variable ConnectedLobby
    async Task<Lobby> CreateLobby(string lobbyName = null, int maxPeers = maxPeerConnections)
    {
        try
        {

            // Create a relay allocation and generate a join code to share with the lobby
            Allocation allocation = await AllocateRelay(maxPeers);

            // configure unity tranport to use websockets for webGL support
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, _encrptionType));
            NetworkManager.Singleton.GetComponent<UnityTransport>().UseWebSockets = encryption == EncryptionType.WSS;

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

            string defaultName = "MyLobby " + DateTime.Now.ToString("mmss");
            string name = (lobbyName != null && lobbyName.Length > 0) ? lobbyName : defaultName;

            ConnectedLobby = await LobbyService.Instance.CreateLobbyAsync(name, maxPeers + 1, options); //lobbySize = maxPeers + 1 host
            await SubscribeToLobbyEvents();
            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "Lobby Error: No Lobby connected");

            // pings to keep the room alive
            StartCoroutine(HeartbeatLobbyCoroutine(ConnectedLobby.Id, 10));

            return ConnectedLobby;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed creating a lobby: {e.Message}");
            await PlayerExit();
            return null;
        }
    }

    // Lobby Events --------------------------------------------------------------------------------------------------------------

    // Subscribe to lobby events
    private async Task SubscribeToLobbyEvents()
    {
        var callbacks = new LobbyEventCallbacks();
        callbacks.LobbyChanged += OnLobbyChanged;
        callbacks.PlayerJoined += OnPlayerJoined;
        callbacks.PlayerLeft += OnPlayerLeft;
        callbacks.KickedFromLobby += OnKickedFromLobby;
        callbacks.LobbyEventConnectionStateChanged += OnLobbyEventConnectionStateChanged;
        try
        {
            if (ConnectedLobbyyEvents != null) return;
            ConnectedLobbyyEvents = await Lobbies.Instance.SubscribeToLobbyEventsAsync(ConnectedLobby.Id, callbacks);
        }
        catch (LobbyServiceException ex)
        {
            switch (ex.Reason)
            {
                case LobbyExceptionReason.AlreadySubscribedToLobby: Debug.LogWarning($"Already subscribed to lobby[{ConnectedLobby.Id}]. We did not need to try and subscribe again. Exception Message: {ex.Message}"); break;
                case LobbyExceptionReason.SubscriptionToLobbyLostWhileBusy: Debug.LogError($"Subscription to lobby events was lost while it was busy trying to subscribe. Exception Message: {ex.Message}"); throw;
                case LobbyExceptionReason.LobbyEventServiceConnectionError: Debug.LogError($"Failed to connect to lobby events. Exception Message: {ex.Message}"); throw;
                default: throw;
            }
        }
    }

    private async Task UnsubscribeFromLobbyEvents()
    {
        if (ConnectedLobbyyEvents != null)
        {
            StopAllCoroutines();
            ConnectedLobbyyEvents.Callbacks.LobbyChanged -= OnLobbyChanged;
            ConnectedLobbyyEvents.Callbacks.PlayerJoined -= OnPlayerJoined;
            ConnectedLobbyyEvents.Callbacks.PlayerLeft -= OnPlayerLeft;
            ConnectedLobbyyEvents.Callbacks.KickedFromLobby -= OnKickedFromLobby;
            ConnectedLobbyyEvents.Callbacks.LobbyEventConnectionStateChanged -= OnLobbyEventConnectionStateChanged;
            await ConnectedLobbyyEvents.UnsubscribeAsync();

            ConnectedLobbyyEvents = null;
        }
    }

    private async void OnLobbyChanged(ILobbyChanges changes)
    {
        if (changes.LobbyDeleted)
        {
            Debug.LogWarning("lobbyChanged: Lobby Deleted");
            ConnectedLobby = null;
            await PlayerExit();
            return;
        }

        if (changes.IsLocked.Changed)
        {
            // Do something specific due to this change
            Debug.LogWarning("lobbyChanged: Lobby Locked: " + changes.IsLocked.Value);
        }

        if (changes.AvailableSlots.Changed)
        {
            if (changes.AvailableSlots.Value == 0)
            {
                Debug.LogWarning("lobbyChanged:: Lobby Full");
                // Do something specific due to this change
            }
            if (changes.AvailableSlots.Added)
            {
                //Debug.LogWarning("lobbyChanged: Player Joined Lobby");
                // Do something specific due to this change
            }
        }

        changes.ApplyToLobby(ConnectedLobby);

        if (changes.Name.Changed)
        {
            // Do something specific due to this change
        }
        // Refresh the UI in some way
    }

    private void OnPlayerJoined(List<LobbyPlayerJoined> players)
    {
        foreach (var playerEntry in players)
        {
            Debug.LogWarning($"player: {playerEntry.Player.Data[playerNameKey].Value} joined lobby");
        }
        // Refresh the UI in some way
    }

    private void OnPlayerLeft(List<int> playerNumbers)
    {
        foreach (var playerNumber in playerNumbers)
        {
            Debug.LogWarning($"player: {playerNumber} left lobby");
        }
        // Refresh the UI in some way
    }

    private async void OnKickedFromLobby()
    {
        Debug.LogWarning("Kicked from lobby");
        await PlayerExit();
        return;
        // Refresh the UI in some way
    }

    private void OnLobbyEventConnectionStateChanged(LobbyEventConnectionState state)
    {
        switch (state)
        {
            case LobbyEventConnectionState.Unsubscribed:
                //Debug.LogWarning("Lobby event connection state is unsubscribed.");
                break;
            case LobbyEventConnectionState.Subscribing:
                //Debug.Log("Lobby event connection state is subscribing.");
                break;
            case LobbyEventConnectionState.Subscribed:
                //Debug.Log("Lobby event connection state is subscribed.");
                break;
            case LobbyEventConnectionState.Unsynced:
                Debug.LogWarning("Lobby event connection state is unsynced. This should not happen. Will attempt to resync.");
                break;
            case LobbyEventConnectionState.Error:
                Debug.LogError("Lobby event connection state is in error. This should not happen. Conection will not be reattempted.");
                break;
        }
    }


    // Allocation --------------------------------------------------------------------------------------------------------------

    async Task<Allocation> AllocateRelay(int maxPeers)
    {
        try
        {
            return await RelayService.Instance.CreateAllocationAsync(maxPeers); // lobbySize = maxPeers + 1 host
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

    private async Task WaitForNetworkConnection()
    {
        float tick = 0;
        while (!NetworkManager.Singleton.IsConnectedClient)
        {

            if (ConnectedLobby == null) throw new Exception("WaitForNetConnection: Lobby is null");

            tick += Time.deltaTime;
            if (tick > 20) throw new Exception("WaitForNetConnection: Reached max timeout of 20 seconds");

            await Task.Yield();
        }
        StartGame();
    }

    private static IEnumerator HeartbeatLobbyCoroutine(string lobbyId, float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            Lobbies.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }


    // Gameplay --------------------------------------------------------------------------------------------------------------

    private void StartGame()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        gameIsActive = true;
        isQuitting = false;

        UIManager.instance.DeactivateUI();
        UIManager.instance.ActivateHUD();
        UIManager.instance.DisplaySignedIn();
        UIManager.instance.DisplayCode(ConnectedLobby.LobbyCode);
        UIManager.instance.DisplayLobbyName(ConnectedLobby.Name);
        UIManager.instance.ResetHUD();


        // find player object and spawn in prelobby
        GameObject[] playersfound = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in playersfound)
        {
            if (player.GetComponent<NetworkObject>().OwnerClientId == NetworkManager.Singleton.LocalClientId)
            {

                player.GetComponent<BasicPlayerController>().SpawnInPreLobby();
            }
        }
    }

    private void EndGame()
    {

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        gameIsActive = false;
        //ConnectionNotificationManager.Singleton.OnClientConnectionNotification -= HandleClientConnectionNotification;

        UIManager.instance.DisableUIText();
        UIManager.instance.DeactivateHUD();
        UIManager.instance.ReturnToTitle();
    }


    // Leave Lobby --------------------------------------------------------------------------------------------------------------

    // removes current player (self) from lobby
    public async Task LeaveLobby()
    {
        try
        {
            if (ConnectedLobby == null) return;
            StopAllCoroutines();

            await Lobbies.Instance.RemovePlayerAsync(ConnectedLobby.Id, _playerId);
            ConnectedLobby = null;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to leave lobby: {e.Message}");
        }
    }

    // method for host to kick players -- not tested
    private async Task ServerKickPlayer(ulong ownerclientid)
    {
        await PlayerExit();
    }

    // method for host to lock the lobby
    public async Task LockLobby()
    {
        try
        {
            if (ConnectedLobby == null) return;

            ConnectedLobby = await LobbyService.Instance.UpdateLobbyAsync(ConnectedLobby.Id, new UpdateLobbyOptions
            {
                IsLocked = true,
                IsPrivate = true
            });

            Debug.Log("Locked Lobby");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to lock lobby: {e.Message}");
        }
    }


    // Application Quit --------------------------------------------------------------------------------------------------------------

    private async void OnApplicationQuit()
    {
        // failsafe if application quits before PlayerExit() can finish
        // dont need to bother to quit gracefully if the applicaiton is shutting down, 
        // just make sure to delete lobby for host and leave lobby for non-hosts
        if (ConnectedLobby != null)
        {
            if (ConnectedLobby.HostId == _playerId) await LobbyService.Instance.DeleteLobbyAsync(ConnectedLobby.Id);
            else await Lobbies.Instance.RemovePlayerAsync(ConnectedLobby.Id, _playerId);
        }
    }

    public async Task PlayerExit()
    {
        try
        {
            await TryQuitLobby();
            EndGame();
            isQuitting = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Error Exiting: " + e.Message);
            isQuitting = false;
        }
    }


    public async Task TryQuitLobby()
    {
        if (quitDone) return;

        if (isQuitting)
        {
            await WaitForQuit();
            return;
        }

        isQuitting = true;

        //Remove player data from GameManager
        GameObject[] playersfound = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in playersfound)
        {
            if (player.GetComponent<NetworkObject>().OwnerClientId == NetworkManager.Singleton.LocalClientId)
            {
                player.GetComponent<PlayerNetworkData>().RemovePlayerDataFromGameManager();
                break;
            }
        }

        if (ConnectedLobbyyEvents != null) await UnsubscribeFromLobbyEvents();
        if (subscribedToNetworkManagerEvents) UnsubscribeFromNetworkManagerEvents();

        if (ConnectedLobby != null)
        {

            if (ConnectedLobby.HostId == _playerId) await DeleteLobby();
            else await LeaveLobby();
        }
        if (NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.Shutdown();
        }
        quitDone = true;
    }

    // method for host to delete the current lobby
    private async Task DeleteLobby()
    {
        try
        {
            if (ConnectedLobby == null) return;

            if (ConnectedLobby.HostId == _playerId) await LobbyService.Instance.DeleteLobbyAsync(ConnectedLobby.Id);
            ConnectedLobby = null;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to delete lobby: {e.Message}");
        }
    }

    private async Task WaitForQuit()
    {
        float tick = 0;
        while (!quitDone)
        {
            tick += Time.deltaTime;
            if (tick > 7) throw new Exception("WaitForQuit: Reached max timeout of 7 seconds");

            await Task.Yield();
        }
    }

    public void ResetQuit()
    {
        quitDone = false;
    }


    // Client/Server Methods --------------------------------------------------------------------------------------------------------------
    private async Task<bool> StartHost()
    {
        try
        {
            SubscribeToNetworkManagerEvents();

            if (ConnectedLobby == null) throw new Exception("No lobby connected");
            if (NetworkManager.Singleton.IsClient) throw new Exception($"NetManager clientID:{NetworkManager.Singleton.LocalClientId} is already a client");

            NetworkManager.Singleton.StartHost();
            await WaitForNetworkConnection();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Failed to start host: " + e.Message);
            await PlayerExit();
            return false;
        }
    }

    private async Task<bool> StartClient()
    {
        try
        {
            SubscribeToNetworkManagerEvents();

            if (ConnectedLobby == null) throw new Exception("No lobby connected");
            if (NetworkManager.Singleton.IsClient) throw new Exception($"NetManager clientID: {NetworkManager.Singleton.LocalClientId} is already a client");

            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(ConnectedLobby.Data[RelayJoinCodeKey].Value);
            // configure unity tranport to use websockets for webGL support
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, _encrptionType));
            NetworkManager.Singleton.GetComponent<UnityTransport>().UseWebSockets = encryption == EncryptionType.WSS;

            NetworkManager.Singleton.StartClient();
            await WaitForNetworkConnection();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Failed to start client: " + e.Message);
            await PlayerExit();
            return false;
        }
    }

    // Connection Notifications - not used --------------------------------------------------------------------------------------------------------------
    private void HandleClientConnectionNotification(ulong clientId, ConnectionNotificationManager.ConnectionStatus status)
    {
        if (status == ConnectionNotificationManager.ConnectionStatus.Connected)
        {
            Debug.Log($"Client {clientId} connected!");
            // Perform actions when a client connects, e.g., update UI, spawn player, etc.
        }
        else if (status == ConnectionNotificationManager.ConnectionStatus.Disconnected)
        {
            Debug.Log($"Client {clientId} disconnected!");
            // Perform actions when a client disconnects, e.g., remove player, update UI, etc.
        }
    }

    // NetworkManager Events --------------------------------------------------------------------------------------------------------------

    private void SubscribeToNetworkManagerEvents()
    {
        NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
        //NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnClientStarted += OnClientStarted;
        //NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        NetworkManager.Singleton.OnClientStopped += OnClientStopped;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

        subscribedToNetworkManagerEvents = true;
    }

    private void UnsubscribeFromNetworkManagerEvents()
    {
        NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
        //NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientStarted -= OnClientStarted;
        //NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
        NetworkManager.Singleton.OnClientStopped -= OnClientStopped;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;

        subscribedToNetworkManagerEvents = false;

    }

    private async void OnTransportFailure()
    {
        Debug.LogWarning("NetManagerEvent: Transport Failure: ");
        await PlayerExit();
    }

    // private void OnServerStarted()
    // {
    //     Debug.Log("NetManagerEvent: Server Started");
    // }

    private void OnClientStarted()
    {
        //Debug.Log("NetManagerEvent: Client Started: " + NetworkManager.Singleton.LocalClientId);
    }

    // private async void OnServerStopped(bool wasHost)
    // {
    //     Debug.LogWarning("NetManagerEvent: Server Stopped - wasHost: " + wasHost);
    //     await PlayerExit();
    // }

    private async void OnClientStopped(bool wasHost)
    {
        Debug.LogWarning("NetManagerEvent: Client Stopped - wasHost: " + wasHost);
        await PlayerExit();
    }

    // this callback is only ran on the server and on the local client that disconnects.
    private void OnClientConnected(ulong clientId)
    {
        // if (clientId == NetworkManager.Singleton.LocalClientId) Debug.Log("NetManagerEvent: Local Client Connected");
        // else Debug.Log("NetManagerEvent: Remote Client Connected: " + clientId);
    }

    // this callback is only ran on the server and on the local client that disconnects.
    private async void OnClientDisconnect(ulong clientId)
    {
        Debug.LogWarning("NetManagerEvent: Client Disconnected: " + clientId);
        // only disconnect if the client is the local client (since this function runs simultaneously on all clients)
        if (clientId == NetworkManager.Singleton.LocalClientId) await PlayerExit();
    }


    // Singleton pattern --------------------------------------------------------------------------------------------------------------

    public static LobbyManager Instance { get; private set; }
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("There is already an instance of the LobbyManager in the scene. Deleting this one.");
            Destroy(this);
        }
        else
        {
            Instance = this;
        }

        if (NetworkManager.Singleton == null) throw new Exception($"There is no {nameof(NetworkManager)} for the {nameof(LobbyManager)} to do stuff with! Please add a {nameof(NetworkManager)} to the scene.");
    }
}