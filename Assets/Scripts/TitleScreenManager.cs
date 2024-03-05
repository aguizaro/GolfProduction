using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleScreenManager : MonoBehaviour
{
    string serverMenuPath = "UIScene";

    // Main Title Screen Objects
    public GameObject mainTitleScreenCanvas;
    public Button mainStartButton;
    public Button mainOptionsButton;

    public GameObject optionsMenuPrefab;

    // Start is called before the first frame update
    public void StartGame()
    {
        SceneManager.LoadScene(serverMenuPath);
    }

    public void OptionsMenu()
    {
        GameObject optionsMenu = Instantiate(optionsMenuPrefab, transform.position, Quaternion.identity);
        //optionsMenu.GetComponent<PauseManager>().parent = gameObject;
    }
}
