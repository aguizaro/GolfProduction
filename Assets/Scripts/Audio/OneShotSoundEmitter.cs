using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class OneShotSoundEmitter : MonoBehaviour
{
    [InspectorName("Sound")]
    [SerializeField] private EventReference soundEvent;

    // Start is called before the first frame update
    void Start()
    {
        //as
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlaySound(Vector3 worldPos)
    {
        RuntimeManager.PlayOneShot(soundEvent, worldPos);
    }
}
