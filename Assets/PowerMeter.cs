using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class PowerMeter : MonoBehaviour
{
    // Start is called before the first frame update
    private Slider power;
    public float changeSpeed = 1.0f;
    private bool increasing = true;
    private bool mouseDown = false;
    void Start()
    {
        power = GetComponentInChildren<Slider>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            mouseDown = true;
        }

        if (power != null && !mouseDown)
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
