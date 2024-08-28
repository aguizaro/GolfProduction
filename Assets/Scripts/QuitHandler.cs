using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Lobbies;

#if UNITY_EDITOR
using UnityEditor;
#endif


public class QuitHandler : MonoBehaviour
{
    private void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#else
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


 // Playmode Quit Handler ----------------------------------------------------------------------------
 #if UNITY_EDITOR
    private bool playerExitDone = false;
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
        Task asyncTask = Quit();       
        yield return new WaitUntil(() => asyncTask.IsCompleted);

        Debug.Log("Done waiting for playmode exit...");
        playerExitDone = true;
        EditorApplication.isPlaying = false;
    }
#endif

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
        Task asyncTask = Quit();
        yield return new WaitUntil(() => asyncTask.IsCompleted);

        isQuitting = true;
        Debug.Log("Done waiting for application quit...");
        Application.Quit();
    }

    private async Task Quit()
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
