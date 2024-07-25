using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Lobbies;

#if UNITY_EDITOR
using UnityEditor;
#endif


public class QuitHandler : MonoBehaviour
{
    private bool playerExitDone = false;


 // Playmode Quit Handler ----------------------------------------------------------------------------
    private void OnEnable()
    {
#if UNITY_EDITOR
        Debug.Log("Using Editor");
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#else
        Debug.Log("NOT Using Editor");
        Application.wantsToQuit += OnWantsToQuit;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#else
        Application.wantsToQuit -= OnWantsToQuit;
#endif
    }


    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (!playerExitDone){
                Debug.Log("Editor waiting to exit playmode...");
                EditorApplication.isPlaying = true; // Re-enter play mode
                StartCoroutine(HandlePlayModeQuit());
            }
        }
    }

    private IEnumerator HandlePlayModeQuit()
    {
        // Simulate async operation
        Task asyncTask = LobbyManager.Instance.PlayerExit();

        Debug.Log("Still waiting");
        
        // Wait until async operation is complete
        yield return new WaitUntil(() => asyncTask.IsCompleted);
        Debug.Log("Done waiting");
        playerExitDone = true;
        EditorApplication.isPlaying = false;
    }

    // Application Quit Handler ----------------------------------------------------------------------------
    private bool isQuitting = false;

    private bool OnWantsToQuit()
    {

        if(!isQuitting){
            Debug.Log("Application trying to quit...");
            isQuitting = true;
            StartCoroutine(HandleApplicationQuit());
            return false; // Prevent the application from quitting immediately
        }
        return true;
    }

    private IEnumerator HandleApplicationQuit()
    {
        // Run the async operation
        Task asyncTask = LobbyManager.Instance.PlayerExit();

        // Wait until the async operation is completed
        yield return new WaitUntil(() => asyncTask.IsCompleted);

        // Ensure the application quits after async operation
        isQuitting = true;
        Application.Quit();
    }

    private async void OnApplicationQuit()
    {
        // failsafe if application quits before PlayerExit() can finish
        // dont need to bother to quit gracefully if the applicaiton is shutting down, 
        // just make sure to delete lobby for host and leave lobby for non-hosts
        if (LobbyManager.Instance.ConnectedLobby != null)
        {
            if (LobbyManager.Instance.ConnectedLobby.HostId == LobbyManager.Instance._playerId) await LobbyService.Instance.DeleteLobbyAsync(LobbyManager.Instance.ConnectedLobby.Id);
            else await Lobbies.Instance.RemovePlayerAsync(LobbyManager.Instance.ConnectedLobby.Id, LobbyManager.Instance._playerId);
        }
    }

}
