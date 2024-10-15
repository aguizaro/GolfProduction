using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadingBar : MonoBehaviour
{

    public float networkDelay = 1.0f;

    void Start()
    {
        // disable image component on start - GameObject is active
        GetComponent<Image>().enabled = false;
    }

    public void StartLoadingBar(float duration)
    {
        GetComponent<Image>().enabled = true;
        StartCoroutine(Loading(duration));
    }

    public void RestartLoadingBar(float duration)
    {
        GetComponent<Image>().enabled = true;
        StopAllCoroutines();
        StartCoroutine(Loading(duration));
    }

    private IEnumerator Loading(float duration)
    {
        float time = duration + networkDelay;
        Image image = GetComponent<Image>();
        while (time > 0)
        {
            time -= Time.deltaTime;
            image.fillAmount = time / duration;
            yield return null;
        }

        GetComponent<Image>().enabled = false;
        
        // play sound to indicate loading is complete (Ragdoll getup)
    }

}
