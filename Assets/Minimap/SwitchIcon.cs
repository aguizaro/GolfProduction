using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SwitchIcon : NetworkBehaviour
{
    public Sprite mainPlayerSprite;
    public Sprite otherPlayerSprite;

    void Start()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if(IsOwner)
        {
            renderer.sprite = mainPlayerSprite;
        }
        else
        {
            renderer.sprite = otherPlayerSprite;
        }
    }
}
