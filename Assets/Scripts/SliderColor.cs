using UnityEngine;
using UnityEngine.UI;

public class SliderColor : MonoBehaviour
{
    private Slider power;
    private Image handleImage;

    void Start()
    {
        power = GetComponent<Slider>();
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

    private void OnDestroy()
    {
        power.onValueChanged.RemoveListener(HandleSliderValueChanged);
    }
}
