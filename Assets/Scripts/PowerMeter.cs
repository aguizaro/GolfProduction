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
    private Image handleImage;

    void OnEnable()
    {
        increasing = true;
        mouseDown = false;
        playerShot = false;
        power = GetComponentInChildren<Slider>();
        power.value = 0;
    }
    void Start()
    {
        handleImage = power.fillRect.GetComponentInChildren<Image>();
        power.onValueChanged.AddListener(HandleSliderValueChanged);
        HandleSliderValueChanged(power.value);
    }
    void HandleSliderValueChanged(float value)
    {
        Color orange = new Color(1f, 0.5f, 0f);
        Color handleColor;
        if (value <= 1f / 3f)
        {
            handleColor = Color.Lerp(Color.red, orange, value * 3);
        }
        else if (value <= 2f / 3f)
        {
            handleColor = Color.Lerp(orange, Color.yellow, (value - 1f / 3f) * 3);
        }
        else
        {
            handleColor = Color.Lerp(Color.yellow, Color.green, (value - 2f / 3f) * 3);
        }
        handleImage.color = handleColor;
    }

    void Update()
    {

        if (Input.GetMouseButton(0))
        {
            mouseDown = true;
        }
        else
        {
            mouseDown = false;

        }
        if (Input.GetMouseButtonUp(0))
        {
            playerShot = true;
        }

        if (power != null && mouseDown && !playerShot)
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


    public float GetPowerValue()
    {
        return power.value;
    }

    public bool GetShotStatus()
    {
        return playerShot;
    }
}
