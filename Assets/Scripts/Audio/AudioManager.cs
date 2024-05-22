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

    public void PlayOneShotForOwner(EventReference soundRef, Vector3 worldPos)
    {
        RuntimeManager.PlayOneShot(soundRef, worldPos);
    }

    public void PlayOneShotForAllClients(EventReference soundRef, Vector3 worldPos, bool isOwner)
    {
        if (!isOwner) return;
        PlayOneShotForAllClientsServerRpc(FMODEvents.instance.GetEventIDFromEventReference(soundRef), worldPos); // Gets ulong id for event reference
    }

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
}
