// Framework built using:
// https://www.youtube.com/watch?v=rcBHIOjZDpk&t=1171s&ab_channel=ShapedbyRainStudios

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

public static class ListExtenstions
{
    public static void AddMany<T>(this List<T> list, params T[] elements)
    {
        list.AddRange(elements);
    }
}

public class FMODEvents : MonoBehaviour
{
    // Assign Event References
    [field: Header("golf_swing")]
    [field: SerializeField] public EventReference playerGolfSwing { get; private set; }
    [field: Header("golf_hole_enter")]
    [field: SerializeField] public EventReference golfHoleEnter { get; private set; }
    [field: Header("golf_clap")]
    [field: SerializeField] public EventReference golfClap { get; private set; }

    // UI Sounds
    [field: Header("ui_select")]
    [field: SerializeField] public EventReference uiSelect { get; private set; }

    // Player Sounds
    [field: Header("player_footsteps")]
    [field: SerializeField] public EventReference playerFootsteps { get; private set; }

    public static FMODEvents instance { get; private set; }

    // Lists of event references and strings
    private List<EventReference> events = new List<EventReference>();
    private List<string> strings = new List<string>();

    // Add Dictionaries to map strings to ulongs to EventReferences
    private Dictionary<string, ulong> stringToUlongLookup = new Dictionary<string, ulong>();
    private Dictionary<ulong, EventReference> ulongToEventReferenceLookup = new Dictionary<ulong, EventReference>();

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than one FMOD Events  in the scene.");
        }
        instance = this;

        events.AddMany(playerGolfSwing, golfHoleEnter, golfClap, uiSelect, playerFootsteps);
        strings.AddMany("golf_swing", "golf_hole_enter", "golf_clap", "ui_select", "player_footsteps");

        // Use the event and strings list to construct the lookup table
        for (ulong i=0; i < (ulong)events.Count; i++)
        {
            stringToUlongLookup.Add(strings[(int)i], i);
            ulongToEventReferenceLookup.Add(i, events[(int)i]);
        }
    }

    public ulong GetEventIDFromString(string event_name)
    {
        if (stringToUlongLookup.ContainsKey(event_name)) {
            return stringToUlongLookup[event_name];
        }
        else 
        {
            Debug.Log("Cannot find ID from string: " + event_name);
            return 0;
        }
    }

    public EventReference GetEventRefenceFromEventID(ulong event_id)
    {
        if (ulongToEventReferenceLookup.ContainsKey(event_id)) {
            return ulongToEventReferenceLookup[event_id];
        }

        Debug.Log("Cannot find Event from id: " + event_id);
        return ulongToEventReferenceLookup[0];
    }

    public string GetStringFromEventReference(EventReference eventRef)
    {
        return "test";
    }
}
