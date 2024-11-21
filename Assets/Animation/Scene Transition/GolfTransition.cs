using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GolfTransition : MonoBehaviour
{
    public Animator transition;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            transition.SetTrigger("Start");
        }
    }

}
