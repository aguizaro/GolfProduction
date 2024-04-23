using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class PowerMeter : MonoBehaviour
{

    private Slider power;
    public float changeSpeed = 1.0f;
    private bool increasing = true;
    private bool mouseDown = false;
    private bool playerShot = false;
    void Start()
    {
        power = GetComponentInChildren<Slider>();
    }


    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            mouseDown = true;
        }
        else {
            mouseDown = false;
        }

        if (power != null && mouseDown)
        {
            if (increasing)
            {
                power.value += changeSpeed * Time.deltaTime;
                if (power.value >= power.maxValue)
                {
                    increasing = false;
                }
            }
            else
            {
                power.value -= changeSpeed * Time.deltaTime;
                if (power.value <= power.minValue)
                {
                    increasing = true;
                }
            }
        }
    }
}
