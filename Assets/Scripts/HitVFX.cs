using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitVFX : MonoBehaviour
{

    //using coroutine to control livetime
    [SerializeField] private float lifetime = 3f;
    public bool destoryObj;

    private WaitForSeconds waitForSeconds;

    void Awake()
    {
        waitForSeconds = new WaitForSeconds(lifetime);
    }
    void OnEnable()
    {
        StartCoroutine(DeactivateCoroutine());
    }
    IEnumerator DeactivateCoroutine()
    {
        yield return waitForSeconds;
        if (destoryObj)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
