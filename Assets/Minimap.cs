using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class Minimap : NetworkBehaviour
{
    public Camera miniMapCamera;
    public RawImage miniMapImage;
    // Start is called before the first frame update
    void Start()
    {
        RenderTexture renderTexture = new RenderTexture(256, 256, 16);
        miniMapCamera.targetTexture = renderTexture;

        if(IsOwner)
        {
            RawImage miniMapImage = GameObject.Find("Minimap Image").GetComponent<RawImage>();
            miniMapImage.texture = renderTexture;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
