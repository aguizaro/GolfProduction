using UnityEngine;
using UnityEngine.UI;

public class SliderColor : MonoBehaviour
{
    private Slider slider;
    private Image handleImage;

    void Start()
    {
        slider = GetComponent<Slider>();
        handleImage = slider.fillRect.GetComponentInChildren<Image>();
        slider.onValueChanged.AddListener(HandleSliderValueChanged);
        HandleSliderValueChanged(slider.value);
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
        slider.onValueChanged.RemoveListener(HandleSliderValueChanged);
    }
}
