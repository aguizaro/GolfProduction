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
    [field: Header("Golf Swing")]
    [field: SerializeField] public EventReference playerGolfSwing { get; private set; }
    [field: Header("Golf Hole Enter")]
    [field: SerializeField] public EventReference golfHoleEnter { get; private set; }
    [field: Header("Golf Clap")]
    [field: SerializeField] public EventReference golfClap { get; private set; }

    // UI Sounds
    [field: Header("UI Select")]
    [field: SerializeField] public EventReference uiSelect { get; private set; }

    // Player Sounds
    [field: Header("Player Footsteps")]
    [field: SerializeField] public EventReference playerFootsteps { get; private set; }

    public static FMODEvents instance { get; private set; }

    // Lists of event references and strings
    private List<EventReference> events = new List<EventReference>();

    // Add Dictionaries to map strings to ulongs to EventReferences
    private Dictionary<EventReference, ulong> eventReferenceToUlongLookup = new Dictionary<EventReference, ulong>();
    private Dictionary<ulong, EventReference> ulongToEventReferenceLookup = new Dictionary<ulong, EventReference>();

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than one FMOD Events in the scene.");
        }
        instance = this;

        events.AddMany(playerGolfSwing, golfHoleEnter, golfClap, uiSelect, playerFootsteps);

        // Use the event list to construct the lookup table
        for (ulong i=0; i < (ulong)events.Count; i++)
        {
            eventReferenceToUlongLookup.Add(events[(int)i], i);
            ulongToEventReferenceLookup.Add(i, events[(int)i]);
        }
    }

    public ulong GetEventIDFromEventReference(EventReference event_ref)
    {
        if (eventReferenceToUlongLookup.TryGetValue(event_ref, out ulong eventID)) 
        {
            return eventID;
        }
        else 
        {
            Debug.Log("Cannot find ID from reference: " + event_ref);
            return ulong.MaxValue;
        }
    }

    public EventReference GetEventRefenceFromEventID(ulong event_id)
    {
        if (ulongToEventReferenceLookup.TryGetValue(event_id, out EventReference eventRef)) {
            return eventRef;
        }
        else
        {
            Debug.Log("Cannot find Event from id: " + event_id);
            return new EventReference();
        }
    }
}
