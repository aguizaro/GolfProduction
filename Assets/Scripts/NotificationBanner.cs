using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class NotificationBanner : MonoBehaviour
{

    private readonly Dictionary<string, Color> primaryColorValues = new Dictionary<string, Color>()
    {
        { "Black", Color.black },
        { "Blue", Color.blue },
        { "Clear", Color.clear },
        { "Cyan", Color.cyan },
        { "Gray", Color.gray },
        { "Green", Color.green },
        { "Magenta", Color.magenta },
        { "Red", Color.red },
        { "White", Color.white },
        { "Yellow", Color.yellow }
    };

    // darker versions of primary colors
    private readonly Dictionary<string, Color> secondaryColorValues = new Dictionary<string, Color>()
    {
        { "Black", new Color(0.3f, 0.3f, 0.3f) },
        { "Blue", new Color(0f, 0f, 0.5f) },
        { "Clear", new Color(0.1f, 0.1f, 0.1f) },
        { "Cyan", new Color(0.1f, 0.3f, 0.3f) },
        { "Gray", new Color(0.3f, 0.3f, 0.3f) },
        { "Green", new Color(0.1f, 0.3f, 0.1f) },
        { "Magenta", new Color(0.3f, 0.1f, 0.3f) },
        { "Red", new Color(0.3f, 0.1f, 0.1f) },
        { "White", new Color(0.3f, 0.3f, 0.3f) },
        { "Yellow", new Color(0.3f, 0.3f, 0.1f) }
    };

    [SerializeField] private GameObject Border;
    [SerializeField] private GameObject Background;
    [SerializeField] private GameObject Message;
    bool isActive = false;
    void Start(){
        Border.SetActive(false);
        Background.SetActive(false);
        Message.SetActive(false);
    }

    // Show the message and begin fading out
    public void Show(string message, string highlightColor = null, int duration = 4 ){
        if(isActive) Hide(); // hide previous message if still active
        isActive = true;

        // Set the message
        Message.GetComponent<TMP_Text>().text = message;

        // Set the alpha of all objects to 1
        Border.GetComponent<Image>().color = (highlightColor == null) ? primaryColorValues["White"] : primaryColorValues[highlightColor];
        Color borderColor = Border.GetComponent<Image>().color;
        borderColor.a = 1;
        Border.GetComponent<Image>().color = borderColor;

        Background.GetComponent<Image>().color = (highlightColor == null) ? secondaryColorValues["White"] : secondaryColorValues[highlightColor];
        Color backgroundColor = Background.GetComponent<Image>().color;
        backgroundColor.a = 1;
        Background.GetComponent<Image>().color = backgroundColor;

        Color messageColor = Message.GetComponent<TMP_Text>().color;
        messageColor.a = 1;
        Message.GetComponent<TMP_Text>().color = messageColor;

        // Activate all objects
        Border.SetActive(true);
        Background.SetActive(true);
        Message.SetActive(true);

        // Fade out the message after duration
        StartCoroutine(FadeOut(duration));
    }

    // Fade out the message by reducing the alpha of all objects
    IEnumerator FadeOut(int duration){
        yield return new WaitForSeconds(duration);
        float fadeSpeed = 0.05f;
        while(true){
            Color borderColor = Border.GetComponent<Image>().color;
            borderColor.a -= fadeSpeed;
            Border.GetComponent<Image>().color = borderColor;

            Color backgroundColor = Background.GetComponent<Image>().color;
            backgroundColor.a -= fadeSpeed;
            Background.GetComponent<Image>().color = backgroundColor;

            Color messageColor = Message.GetComponent<TMP_Text>().color;
            messageColor.a -= fadeSpeed;
            Message.GetComponent<TMP_Text>().color = messageColor;

            if(borderColor.a <= 0){
                Border.SetActive(false);
                Background.SetActive(false);
                Message.SetActive(false);
                isActive = false;
                break;
            }
            yield return new WaitForSeconds(0.01f);
        }
    }

    // Hide the message immediately
    public void Hide(){
        StopAllCoroutines();
        Border.SetActive(false);
        Background.SetActive(false);
        Message.SetActive(false);
        isActive = false;
    }
}
