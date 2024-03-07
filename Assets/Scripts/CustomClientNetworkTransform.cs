using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class CustomClientNetworkTransform : NetworkTransform
{
    /// <summary>
    /// Used to determine who can write to this transform. Owner client only.
    /// This imposes state to the server. This is putting trust on your clients. Make sure no security-sensitive features use this transform.
    /// </summary>
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }


    protected override void OnAuthorityPushTransformState(ref NetworkTransformState networkTransformState)
    {
        // Log the position and rotation every time it's updated
        //Debug.Log("client: " + OwnerClientId + " OnAuthorityPushTransformState\nPosition: " + networkTransformState.GetPosition() + " Rotation: " + networkTransformState.GetRotation().eulerAngles);
    }

    protected override void OnNetworkTransformStateUpdated(ref NetworkTransformState oldState, ref NetworkTransformState newState)
    {
        //Debug.Log("client: " + OwnerClientId + " OnNetworkTransformStateUpdated\nPosition: " + newState.GetPosition() + " Rotation: " + newState.GetRotation().eulerAngles);
    }


}