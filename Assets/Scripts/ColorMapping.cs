using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class ColorMapping
{
    public GameObject uiElement; // Reference to the UI element
    public Color originalColor;
    public Color colorBlindFriendlyColor;
}
