// Framework built using:
// https://www.youtube.com/watch?v=rcBHIOjZDpk&t=1171s&ab_channel=ShapedbyRainStudios

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using FMODUnity;
using FMOD.Studio;

public class AudioManager : NetworkBehaviour
{
    public static AudioManager instance { get; private set; }
    private EventInstance BGM;

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than one Audio Manager in the scene.");
        }
        instance = this;

        // Assign the BGM
        BGM = RuntimeManager.CreateInstance(FMODEvents.instance.music);
        BGM.start();
        BGM.setParameterByNameWithLabel("Music", "Title Screen");
    }

    // ############## Music Management ################################
    public void ChangeMusic(string trackName)
    {
        Debug.Log("Music Change Status: " + BGM.setParameterByNameWithLabel("Music", trackName));
    }

    // ############## Play Oneshot sounds ################################
    public void PlayOneShotForOwner(EventReference soundRef, Vector3 worldPos)
    {
        RuntimeManager.PlayOneShot(soundRef, worldPos);
    }

    public void PlayOneShotForAllClients(EventReference soundRef, Vector3 worldPos, bool isOwner)
    {
        if (!IsOwner) return;
        PlayOneShotForAllClientsServerRpc(FMODEvents.instance.GetEventIDFromEventReference(soundRef), worldPos); // Gets ulong id for event reference
    }

    // ############## Play Timeline sounds ################################
    public void PlayTimelineSoundForOwner(EventReference soundRef, GameObject playerRef=null) 
    {
        // If the provided sound doesn't already exist, create it
        if (!FMODEvents.instance.DoesEventInstanceExist(soundRef))
        {
            FMODEvents.instance.CreateEventInstance(soundRef, playerRef);
        }

        PLAYBACK_STATE playbackState;
        EventInstance eventInstance = FMODEvents.instance.GetEventInstanceFromEventReference(soundRef);

        eventInstance.getPlaybackState(out playbackState);
        if (playbackState.Equals(PLAYBACK_STATE.STOPPED))
        {
            eventInstance.start();
        }
    }

    public void PlayTimelineSoundForAllClients(EventReference soundRef, GameObject playerRef=null) // This requires a NetworkObject to 
    {
        if (!IsOwner) return; 

        if (playerRef) {
            NetworkObject networkObject = playerRef.GetComponent<NetworkObject>();

            if (networkObject)
            {
                //Debug.Log(networkObject.NetworkObjectId);
                PlayTimelineSoundForAllClientsServerRpc(FMODEvents.instance.GetEventIDFromEventReference(soundRef), networkObject.OwnerClientId);
                //PlayTimelineSoundForAllClientsServerRpc(0, 0);
            }
        }
    }

    public void StopTimelineSoundForOwner(EventReference soundRef) 
    {
        // If the provided sound doesn't already exist, don't do anything
        if (!FMODEvents.instance.DoesEventInstanceExist(soundRef))
        {
            EventInstance eventInstance = FMODEvents.instance.GetEventInstanceFromEventReference(soundRef);
            eventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        }
    }

    // ############## Oneshot Rpcs ################################
    [ServerRpc (RequireOwnership=false)]
    void PlayOneShotForAllClientsServerRpc(ulong soundID, Vector3 worldPos)
    {
        PlayOneShotForAllClientsClientRpc(soundID, worldPos);
    }

    [ClientRpc]
    void PlayOneShotForAllClientsClientRpc(ulong soundID, Vector3 worldPos)
    {
        RuntimeManager.PlayOneShot(FMODEvents.instance.GetEventRefenceFromEventID(soundID), worldPos);
    }

    [ServerRpc (RequireOwnership=false)]
    void PlayTimelineSoundForAllClientsServerRpc(ulong soundID, ulong ownerID)
    {
        Debug.Log("Before the client rpc is called: " + GetComponent<NetworkObject>().NetworkObjectId);
        PlayTimelineSoundForAllClientsClientRpc(soundID, ownerID);
    }

    [ClientRpc]
    void PlayTimelineSoundForAllClientsClientRpc(ulong soundID, ulong ownerID)
    {
        Debug.Log($"Owner Client id: {OwnerClientId} Id owner: {IsOwner}");
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            if (player.GetComponent<NetworkObject>().OwnerClientId == ownerID)
            {
                Debug.Log($"Owner Client id: {OwnerClientId} Id owner: {IsOwner} Found {player.GetComponent<NetworkObject>().OwnerClientId}");
                PlayTimelineSoundForOwner(FMODEvents.instance.GetEventRefenceFromEventID(soundID), player);
            }
        }
    }
}
