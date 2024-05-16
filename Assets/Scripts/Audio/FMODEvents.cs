// Framework built using:
// https://www.youtube.com/watch?v=rcBHIOjZDpk&t=1171s&ab_channel=ShapedbyRainStudios

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

public class FMODEvents : MonoBehaviour
{
    // Assign Event References
    [field: Header("Golf Swing")]
    [field: SerializeField] public EventReference playerGolfSwing { get; private set; }
    [field: Header("Golf Hole Enter")]
    [field: SerializeField] public EventReference golfHoleEnter { get; private set; }
    [field: Header("Golf Clap")]
    [field: SerializeField] public EventReference golfClap { get; private set; }
    

    [field: Header("Footsteps")]
    [field: SerializeField] public EventReference playerFootsteps { get; private set; }

    public static FMODEvents instance { get; private set; }

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than one FMOD Events  in the scene.");
        }
        instance = this;
    }
}
