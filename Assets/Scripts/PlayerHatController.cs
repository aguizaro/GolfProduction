using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnitySerializedDictionary;

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

        if (IsOwner)
        {
            // Get random texture. We can change how the player's hat initially spawns
            PlayerHatConfig newPlayerConfig = new PlayerHatConfig()
            {
                meshID = 0,
                textureID = GetRandomHatTextureID()
            };
            _currentHatConfig = newPlayerConfig;
            CommitNetworkHatConfigServerRpc(_currentHatConfig);
        }
        else
        {
            // Apply config from network variable
            _currentHatConfig = _networkPlayerHatConfig.Value;
            ApplyCurrentHatConfig();
        }
    }
    public override void OnDestroy()
    {
        _networkPlayerHatConfig.OnValueChanged -= OnHatConfigChanged;
        base.OnDestroy();
    }
    private void OnHatConfigChanged(PlayerHatConfig prevData, PlayerHatConfig newData)
    {
        _currentHatConfig = newData;
        ApplyCurrentHatConfig();
    }

    public void ApplyCurrentHatConfig()
    {
        // TODO: Update player mesh

        // Update player texture
        hatMaterial.mainTexture = textures[_currentHatConfig.textureID];
    }

    // Takes ulong arg which represents the texture ID
    public void SetHatMesh(ulong id)
    {
        if (IsOwner)
        {
            if (meshes.ContainsKey(id))
            {
                PlayerHatConfig newdata = new PlayerHatConfig() 
                {
                    meshID = id,
                    textureID = _currentHatConfig.textureID
                };
                //TODO: Update mesh
            }
        }
        else{
            //_currentHatConfig = 
        }
    }

    public void SetHatTexture(ulong id)
    {
        if (textures.ContainsKey(id))
        {
            //currentTextureId = id;
            //hatMaterial.mainTexture = textures[id];
        }
    }

    [ServerRpc]
    private void CommitNetworkHatConfigServerRpc(PlayerHatConfig config)
    {
        _networkPlayerHatConfig.Value = config;
    }

    public void RandomizeHatTexture()
    {
        // Convert keys to an array
        ulong[] keysArray = new List<ulong>(textures.Keys).ToArray();

        // Get a random key from the array
        ulong randomId = keysArray[UnityEngine.Random.Range(0, keysArray.Length)];

        //currentTextureId = randomId;
        //hatMaterial.mainTexture = textures[currentTextureId];
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

    private PlayerHatConfig GetOwnersHatConfig()
    {
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject playerObject in playerObjects)
        {
            if (playerObject.GetComponent<NetworkObject>().IsOwner && playerObject.GetComponent<NetworkObject>().OwnerClientId == OwnerClientId)
            {
                PlayerHatConfig playerConfig = playerObject.GetComponent<PlayerHatController>()._networkPlayerHatConfig.Value;
                return playerConfig;
            }
        }
        Debug.LogError("Could not find player object with OwnerClientId: " + OwnerClientId);
        return new PlayerHatConfig();
    }
}
