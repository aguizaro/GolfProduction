using Unity.Netcode;
using UnityEngine;

// Network button code provided by https://github.com/Matthew-J-Spencer/Unity-Netcode-Starter/blob/main/Assets/_Game/_Scripts/Misc/NetworkButtons.cs 
public class NetworkButtons : MonoBehaviour
{

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Host")) NetworkManager.Singleton.StartHost();
            if (GUILayout.Button("Server")) NetworkManager.Singleton.StartServer();
            if (GUILayout.Button("Client")) NetworkManager.Singleton.StartClient();
        }
        GUILayout.EndArea();
    }
}