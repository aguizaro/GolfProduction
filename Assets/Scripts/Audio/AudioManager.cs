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

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than one Audio Manager in the scene.");
        }
        instance = this;
    }

    public void PlayOneShot(EventReference sound, Vector3 worldPos)
    {
        RuntimeManager.PlayOneShot(sound, worldPos);
    }

    public EventInstance CreateInstance(EventReference eventReference, GameObject playerReference)
    {
        EventInstance eventInstance = RuntimeManager.CreateInstance(eventReference);
        RuntimeManager.AttachInstanceToGameObject(eventInstance, playerReference.GetComponent<Transform>(), playerReference.GetComponent<Rigidbody>());
        //eventInstance.set3DAttributes();
        return eventInstance;
    }

    public void PlayOneShotForAllClients(string sound, Vector3 worldPos) 
    {
        if (!IsOwner) return;

        PlayOneShotForAllClientsServerRPC(FMODEvents.instance.GetEventIDFromString(sound), worldPos);
    }

    [ServerRpc]
    private void PlayOneShotForAllClientsServerRPC(ulong soundID, Vector3 worldPos)
    {
        PlayOneShotForAllClientsClientRPC(soundID, worldPos);
    }

    [ClientRpc]
    private void PlayOneShotForAllClientsClientRPC(ulong soundID, Vector3 worldPos)
    {
        RuntimeManager.PlayOneShot(FMODEvents.instance.GetEventRefenceFromEventID(soundID), worldPos);
    }
}
