using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallDuration : MonoBehaviour
{
    [SerializeField] private float lifeInSecs = 5f;

    // Start is called before the first frame update
    void Start()
    {
        Invoke("DestroySelf", lifeInSecs);
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }
}
