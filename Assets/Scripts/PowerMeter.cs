using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class PowerMeter : MonoBehaviour
{

    private Slider power;
    public float changeSpeed = 1.0f;
    private bool increasing = true;
    [SerializeField] private bool _mouseDown = false;
    public bool MouseDown
    {
        get { return _mouseDown; }
        set { _mouseDown = value; }
    }
    [SerializeField] private bool _playerShot = false;
    public bool PlayerShot
    {
        get { return _playerShot; }
        set { _playerShot = value; }
    }
    private Image handleImage;

    void OnEnable()
    {
        increasing = true;
        _mouseDown = false;
        _playerShot = false;
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
        if (power != null && _mouseDown && !_playerShot)
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
        return _playerShot;
    }
}
