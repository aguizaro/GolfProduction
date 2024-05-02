using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnitySerializedDictionary;

public class PlayerHatController : MonoBehaviour
{
    [SerializeField] private GameObject playerHat;
    private Material hatMaterial;
    private MeshFilter hatMesh;
    private int currentMeshId;
    private int currentTextureId;

    // Editor representation of the texture dictionaries
    [SerializeField] private SerializedDictionary<int, Texture> serializedTextureDictionary;
    [SerializeField] private SerializedDictionary<int, Mesh> serializedMeshDictionary;

    // Lists of meshes and textures
    private Dictionary<int, Mesh> meshes;
    private Dictionary<int, Texture> textures;

    private void Awake()
    {
        hatMesh = playerHat.GetComponent<MeshFilter>();
        hatMaterial = playerHat.GetComponent<Renderer>().material;

        // Deserialize the texture dictionary
        meshes = serializedMeshDictionary.ToDictionary();
        textures = serializedTextureDictionary.ToDictionary();
    }

    public void SetHatMesh(int id)
    {
        if (meshes.ContainsKey(id))
        {
            currentMeshId = id;
            //TODO: Update mesh
        }
    }

    public void SetHatTexture(int id)
    {
        if (textures.ContainsKey(id))
        {
            currentTextureId = id;
            hatMaterial.mainTexture = textures[id];
        }
    }

    public void RandomizeHatTexture()
    {
        // Convert keys to an array
        int[] keysArray = new List<int>(textures.Keys).ToArray();

        // Get a random key from the array
        int randomId = keysArray[UnityEngine.Random.Range(0, keysArray.Length)];

        currentTextureId = randomId;
        hatMaterial.mainTexture = textures[currentTextureId];
    }

    public int GetCurrentMeshId() => currentMeshId;
    public int GetCurrentTextureId() => currentTextureId;
}
