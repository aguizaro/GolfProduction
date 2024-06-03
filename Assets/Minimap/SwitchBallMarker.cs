using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class SwitchBallMarker : NetworkBehaviour
{
    public Sprite mainPlayerBallMarker;
    private Quaternion initialRotation;
    // Start is called before the first frame update
    void Start()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if(IsOwner)
        {
            initialRotation = transform.rotation;
            renderer.sprite = mainPlayerBallMarker;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void LateUpdate()
    {
        if(IsOwner)
        {
            transform.rotation = initialRotation;
        }
    }
}
