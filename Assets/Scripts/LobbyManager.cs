
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
using Unity.VisualScripting;



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
    public string _playerId;
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

        ConnectionNotificationManager.Singleton.OnClientConnectionNotification += HandleClientConnectionNotification;

    }
    // Player Operations --------------------------------------------------------------------------------------------------------------

    // returns name of currently signed in player
    public string GetLocalPlayerName()
    {
        return _playerName;
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
                { playerIdKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, _playerId) },
                { localClientIdKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, NetworkManager.Singleton.LocalClientId.ToString()) }
            }
            };
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to create player: {e.Message}");
            return null;
        }
    }

    // Update lobby player data with current local clientID

    public async Task<bool> UpdatePlayerName(string newName)
    {
        try
        {
            // check if player name is valid
            if (string.IsNullOrEmpty(newName) || !Regex.Match(newName, "^[a-zA-Z0-9_-]{1,30}$").Success) throw new LobbyServiceException(new LobbyExceptionReason(), "Invalid player name provided");
            
            // update player name in authentication service
            _playerName = newName;
            await AuthenticationService.Instance.UpdatePlayerNameAsync(newName);

            // update player name in lobby service
            if (ConnectedLobby == null) return true;
            await LobbyService.Instance.UpdatePlayerAsync(ConnectedLobby.Id, AuthenticationService.Instance.PlayerId, new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { playerNameKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, newName) },
                    { localClientIdKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, NetworkManager.Singleton.LocalClientId.ToString()) }
                }
            });

            // update player name in nameTagRotator
            Transform NameTagCanvas = NetworkManager.Singleton.LocalClient.PlayerObject.transform.Find("NameTagCanvas");
            if (NameTagCanvas != null) NameTagCanvas.Find("NameTag").GetComponent<NameTagRotator>().UpdateNameTag(newName);

            // update player name in local player network data
            PlayerData updatedData = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerNetworkData>().GetPlayerData();
            updatedData.playerName = newName;
            NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerNetworkData>().StorePlayerState(updatedData);
            
            return true;
        }
        catch (Exception e)
        {
            Debug.Log($"{e.Message}. Please try again.");
            return false;
        }
    }

    public async Task<bool> UpdateClientID(){
        try
        {
            if (ConnectedLobby == null) return false;
            await LobbyService.Instance.UpdatePlayerAsync(ConnectedLobby.Id, AuthenticationService.Instance.PlayerId, new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { localClientIdKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, NetworkManager.Singleton.LocalClientId.ToString()) }
                }
            });

            return true;
        }
        catch (Exception e)
        {
            Debug.Log($"{e.Message}. Please try again.");
            return false;
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

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to join lobby: {e.Message}");
            UIManager.instance.EnableAllLobbyButtons();
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

            ConnectedLobby = await LobbyService.Instance.QuickJoinLobbyAsync(options);
            await SubscribeToLobbyEvents();
            if (ConnectedLobby == null) throw new LobbyServiceException(new LobbyExceptionReason(), "No Lobby Found");

            bool clientConnected = await StartClient();
            if (!clientConnected) throw new LobbyServiceException(new LobbyExceptionReason(), "Failed to start client");

        }
        catch (LobbyServiceException e)
        {
            Debug.Log($"Failed to quick join lobby: {e.Message}");
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

        }

        if (changes.AvailableSlots.Changed)
        {
            if (changes.AvailableSlots.Value == 0)
            {
                UIManager.instance.DisplayNotification("Lobby is now full");
            }
            if (changes.AvailableSlots.Added)
            {
                //Debug.LogWarning("lobbyChanged: Player Joined Lobby");
                // Do something specific due to this change
            }
        }

        if (changes.Name.Changed)
        {
            // Do something specific due to this change
            Debug.Log("lobbyChanged: Name Changed to " + changes.Name.Value);
        }
       

        if (changes.PlayerData.Changed){
            // FIGURE OUT HOW TO UPDATE PLAYER DATA HERE - CURRENTLY BEING DONE IN HandleClientConnectionNotification On Client Connection (if client is this player) 
        }

        try{
            changes.ApplyToLobby(ConnectedLobby);
        }
        catch (Exception e){
            Debug.LogWarning($"Failed to apply changes to lobby: {e.Message}");
        }

    }

    private void OnPlayerJoined(List<LobbyPlayerJoined> players)
    {
        foreach (var playerEntry in players)
        {
            UIManager.instance.DisplayNotification($"{playerEntry.Player.Data[playerNameKey].Value} is joining lobby");
        }

        if (NetworkManager.Singleton.IsServer){

        }
    }   

    private void OnPlayerLeft(List<int> playerNumbers)
    {
        foreach (var playerNumber in playerNumbers)
        {
            // Display a notification that a player has left
        }
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
        //StartGame(); // - this call was moved to BasicPlayerController.cs when the player is spawned in the correct position - this is to prevent the user from seeing the camera flying through world before spawn
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

    public void StartGame()
    {
        try{
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            gameIsActive = true;
            isQuitting = false;

            UIManager.instance.DeactivateUI();
            UIManager.instance.EnableAllLobbyButtons();
            UIManager.instance.ActivateHUD();
            UIManager.instance.DisplaySignedIn();
            UIManager.instance.DisplayCode(ConnectedLobby.LobbyCode);
            UIManager.instance.DisplayLobbyName(ConnectedLobby.Name);
            UIManager.instance.ResetHUD();
        }
        catch (Exception e){
            Debug.LogWarning($"Failed to start game: {e.Message}");
        }
    }

    private void EndGame()
    {

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        gameIsActive = false;
        ConnectionNotificationManager.Singleton.OnClientConnectionNotification -= HandleClientConnectionNotification;

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

        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"Failed to lock lobby: {e.Message}");
        }
    }


    // Application Quit --------------------------------------------------------------------------------------------------------------

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

        if (ConnectedLobbyyEvents != null) await UnsubscribeFromLobbyEvents();
        if (subscribedToNetworkManagerEvents) UnsubscribeFromNetworkManagerEvents();

        if (ConnectedLobby != null)
        {
            if (ConnectedLobby.HostId == _playerId) await DeleteLobby();
            else await LeaveLobby();
        }
        
        if (NetworkManager.Singleton.IsClient) NetworkManager.Singleton.Shutdown();
        quitDone = true;
    }

    // method for host to delete the current lobby
    private async Task DeleteLobby()
    {
        try
        {
            if (ConnectedLobby == null) return;

            // delete game manager data
            GameManager.instance.ClearPlayersData();

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

    // Connection Notifications --------------------------------------------------------------------------------------------------------------
    private async void HandleClientConnectionNotification(ulong clientId, ConnectionNotificationManager.ConnectionStatus status)
    {
        Debug.Log($"con NOTIF: Client {clientId} is {status}");

        if (status == ConnectionNotificationManager.ConnectionStatus.Connected)
        {
            // if we are the new connected client
            if(clientId == NetworkManager.Singleton.LocalClientId){
                if (ConnectedLobby == null) return;
                // update LocalClientID in lobby player data and set my nameTag to my name
                //Debug.Log("ConnectNOTIF: I just connected with name: " + _playerName + " and ID: " + _playerId);
                await UpdateClientID();

                Transform NameTagCanvas = NetworkManager.Singleton.LocalClient.PlayerObject.transform.Find("NameTagCanvas");
                if (NameTagCanvas != null) NameTagCanvas.Find("NameTag").GetComponent<NameTagRotator>().UpdateNameTag(_playerName);
                
                //set other player's nameTags to their names
                foreach (var player in GameObject.FindGameObjectsWithTag("Player"))
                {
                    if (player.GetComponent<NetworkObject>().OwnerClientId != clientId)
                    {
                        Transform nametagcanvas = player.transform.Find("NameTagCanvas");
                        if (nametagcanvas != null) nametagcanvas.Find("NameTag").GetComponent<NameTagRotator>().UpdateNameTag(player.GetComponent<PlayerNetworkData>().GetPlayerData().playerName);
                    }
                }
                
                return;
            }

            // Refresh hat config to sync with new player
            NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerHatController>().RefreshHatConfig();

        }
        else if (status == ConnectionNotificationManager.ConnectionStatus.Disconnected)
        {
            // REMEMBER: This function is unreliable when host changes, so RemovePlayerData has been moved to OnClientDisconnect NetManager event
        }
    }

    // NetworkManager Events --------------------------------------------------------------------------------------------------------------

    private void SubscribeToNetworkManagerEvents()
    {
        NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
        NetworkManager.Singleton.OnClientStopped += OnClientStopped;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        //NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        //NetworkManager.Singleton.OnClientStarted += OnClientStarted;
        //NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        //NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        subscribedToNetworkManagerEvents = true;
    }

    private void UnsubscribeFromNetworkManagerEvents()
    {
        NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
        NetworkManager.Singleton.OnClientStopped -= OnClientStopped;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        //NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        //NetworkManager.Singleton.OnClientStarted -= OnClientStarted;
        //NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
        //NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        subscribedToNetworkManagerEvents = false;

    }

/*
    private void OnServerStarted()
    {
        Debug.Log("NetManagerEvent: Server Started");
    }

    private void OnClientStarted()
    {
        //Debug.Log("NetManagerEvent: Client Started: " + NetworkManager.Singleton.LocalClientId);
    }

    private async void OnServerStopped(bool wasHost)
    {
        Debug.LogWarning("NetManagerEvent: Server Stopped - wasHost: " + wasHost);
        await PlayerExit();
    }

    this callback is only ran on the server and on the local client that disconnects.
    private void OnClientConnected(ulong clientId)
    {
        // if (clientId == NetworkManager.Singleton.LocalClientId) Debug.Log("NetManagerEvent: Local Client Connected");
        // else Debug.Log("NetManagerEvent: Remote Client Connected: " + clientId);
    }
*/

    // THESE 3 EVENT HANDLERS ARE CALLED LAST (AFTER HandleClientConnectionNotification) AND SERVE 
    // AS A FAILSAFE (QUIT GRACEFULLY) IN CASE A CLIENT FAILS AND ^ HANDLER MISSES IT.
    
    // MOST LIKELY - PLAYEREXIT() HAS ALREADY BEEN CALLED BY THE TIME THESE FUNCTIONS RUN, SO
    // PLAYEREXIT() WILL RETURN EARLY IN THAT CASE

    private async void OnTransportFailure()
    {
        Debug.LogWarning("NetManagerEvent: Transport Failure: ");
        await PlayerExit();
    }

    private async void OnClientStopped(bool wasHost)
    {
        //Debug.LogWarning("NetManagerEvent: Client Stopped - wasHost: " + wasHost);
        await PlayerExit();
    }

    // this callback is only ran on the server and on the local client that disconnects.
    private async void OnClientDisconnect(ulong clientId)
    {
        //Debug.LogWarning("NetManagerEvent: Client Disconnected: " + clientId);
        // only disconnect if the client is the local client (since this function runs simultaneously on all clients)
        if (clientId == NetworkManager.Singleton.LocalClientId) await PlayerExit();
        else if (NetworkManager.Singleton.IsServer){
            Debug.Log("NetManagerEvent: Player Disconnected: " + clientId + " - server removing player data");
            GameManager.instance.RemovePlayerData(clientId); // server removes player data for a client when they disconnect - this needs to be called here because HandleClientConnectionNotification is not reliable when host keeps changing
        }
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