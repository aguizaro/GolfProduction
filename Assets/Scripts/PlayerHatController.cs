using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnitySerializedDictionary;
using UnityEditor;

public struct PlayerHatConfig : INetworkSerializable
{
    public ulong meshID;
    public ulong textureID;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref meshID);
        serializer.SerializeValue(ref textureID);
    }
}

public class PlayerHatController : NetworkBehaviour
{
    public NetworkVariable<PlayerHatConfig> _networkPlayerHatConfig = new NetworkVariable<PlayerHatConfig>(new PlayerHatConfig(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private PlayerHatConfig _currentHatConfig;
    [SerializeField] private GameObject playerHat;
    private Material hatMaterial;
    private MeshFilter hatMesh;

    // Editor representation of the mesh/texture dictionaries
    [SerializeField] private SerializedDictionary<ulong, Texture> serializedTextureDictionary;
    [SerializeField] private SerializedDictionary<ulong, Mesh> serializedMeshDictionary;

    // Dictionaries of meshes and textures
    private Dictionary<ulong, Mesh> meshes;
    private Dictionary<ulong, Texture> textures;

    private void Awake()
    {
        _networkPlayerHatConfig.OnValueChanged += OnHatConfigChanged;
    }

    public override void OnNetworkSpawn()
    {
        hatMesh = playerHat.GetComponent<MeshFilter>();
        hatMaterial = playerHat.GetComponent<Renderer>().material;

        // Deserialize the texture dictionary
        meshes = serializedMeshDictionary.ToDictionary();
        textures = serializedTextureDictionary.ToDictionary();
    }
    public override void OnDestroy()
    {
        _networkPlayerHatConfig.OnValueChanged -= OnHatConfigChanged;
        base.OnDestroy();
    }
    private void OnHatConfigChanged(PlayerHatConfig prevData, PlayerHatConfig newData)
    {
        Debug.Log($"Owner: {OwnerClientId} hat config changed: {prevData.meshID}, {prevData.textureID} -> {newData.meshID}, {newData.textureID}");
        _currentHatConfig = newData;
        ApplyCurrentHatConfig();
    }

    public void ApplyCurrentHatConfig()
    {
        // TODO: Update player mesh
        hatMesh.mesh = meshes[_currentHatConfig.meshID];

        // Update player texture
        hatMaterial.mainTexture = textures[_currentHatConfig.textureID];

        Debug.Log($"Owner: {OwnerClientId} applied hat config: {_currentHatConfig.meshID}, {_currentHatConfig.textureID}");
    }

    // Takes ulong arg which represents the texture ID
    public void SetHatMesh(ulong id)
    {
        if (meshes.ContainsKey(id))
        {
            SetHatConfig(id, _currentHatConfig.textureID);
        }
    }

    public void SetHatTexture(ulong id)
    {
        if (textures.ContainsKey(id))
        {
            SetHatConfig(_currentHatConfig.meshID, id);
        }
    }

    public void SetHatConfig(ulong meshID, ulong textureID)
    {
        if (IsOwner)
        {
            // Get random texture. We can change how the player's hat initially spawns
            PlayerHatConfig newPlayerConfig = new PlayerHatConfig()
            {
                meshID = meshID,
                textureID = textureID
            };
            _currentHatConfig = newPlayerConfig;
            Debug.Log($"Owner: {OwnerClientId} setting hat config: {meshID}, {textureID}");
            CommitNetworkHatConfigServerRpc(_currentHatConfig);
        }
        else
        {
            // Apply config from network variable
            _currentHatConfig = _networkPlayerHatConfig.Value;
            Debug.Log($"non-owner: {OwnerClientId} reading hat config: {meshID}, {textureID}");
            ApplyCurrentHatConfig();
        }
    }

    [ServerRpc]
    private void CommitNetworkHatConfigServerRpc(PlayerHatConfig config)
    {
        _networkPlayerHatConfig.Value = new PlayerHatConfig()
        {
            meshID = config.meshID,
            textureID = config.textureID
        };
        Debug.Log("server set hat config: " + config.meshID + ", " + config.textureID + "for OwnerClientId " + OwnerClientId);
    }

    public void CycleHatConfig()
    {
        // Mesh id:1 (default mesh), Mesh id:0 (no mesh), 11 textures (index 0-10)
        ulong[] textureKeys = new List<ulong>(textures.Keys).ToArray();
        int textureIndex = Array.IndexOf(textureKeys, _currentHatConfig.textureID);

        // cycle back to no hat if at the end of the texture list
        if (textureIndex == textureKeys.Length - 1)
        {
            RemoveHatConfig();
            return;
        }

        textureIndex = (textureIndex + 1) % textureKeys.Length;
        SetHatConfig(1, textureKeys[textureIndex]);
    }

    // [ServerRpc]
    // public void GetEveryonesHatConfigServerRpc()
    // {
    //     GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
    //     foreach (GameObject playerObject in playerObjects)
    //     {
    //         PlayerHatConfig playerConfig = playerObject.GetComponent<PlayerHatController>()._networkPlayerHatConfig.Value;
    //         Debug.Log("SERVERRPC: Player " + playerObject.GetComponent<NetworkObject>().OwnerClientId + " has hat config: " + playerConfig.meshID + ", " + playerConfig.textureID);
    //         //UpdateConfigClientRpc(playerConfig, playerObject.GetComponent<NetworkObject>().OwnerClientId);
    //     }
    // }

    // re-sends current hat config over network
    public void UpdateHatConfig()
    {
        StartCoroutine(DelayedHatUpdate());
    }

    IEnumerator DelayedHatUpdate()
    {
        yield return new WaitForSecondsRealtime(5f);
        SetHatConfig(_currentHatConfig.meshID, _currentHatConfig.textureID);
    }



    public void RemoveHatConfig()
    {
        SetHatConfig(0, 0);
    }

    public ulong GetRandomHatMeshID()
    {
        // Convert keys to an array
        ulong[] keysArray = new List<ulong>(meshes.Keys).ToArray();

        // Get a random key from the array
        return keysArray[UnityEngine.Random.Range(0, keysArray.Length)];
    }

    public ulong GetRandomHatTextureID()
    {
        // Convert keys to an array
        ulong[] keysArray = new List<ulong>(textures.Keys).ToArray();

        // Get a random key from the array
        return keysArray[UnityEngine.Random.Range(0, keysArray.Length)];
    }

    public ulong GetCurrentMeshId() => _currentHatConfig.meshID;
    public ulong GetCurrentTextureId() => _currentHatConfig.textureID;
    public PlayerHatConfig GetCurrentHatConfig() => _currentHatConfig;

    //private PlayerHatConfig GetOwnersHatConfig()
    //{
    //    GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
    //
    //    foreach (GameObject playerObject in playerObjects)
    //    {
    //        if (playerObject.GetComponent<NetworkObject>().IsOwner && playerObject.GetComponent<NetworkObject>().OwnerClientId == OwnerClientId)
    //        {
    //            PlayerHatConfig playerConfig = playerObject.GetComponent<PlayerHatController>()._networkPlayerHatConfig.Value;
    //            return playerConfig;
    //        }
    //    }
    //    Debug.LogError("Could not find player object with OwnerClientId: " + OwnerClientId);
    //    return new PlayerHatConfig();
    //}
}
