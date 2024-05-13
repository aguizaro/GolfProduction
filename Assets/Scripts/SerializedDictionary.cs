using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnitySerializedDictionary {
[Serializable]
public class SerializedDictionary<TKey, TValue>
{
    [Serializable]
    private class SerializedDictionaryItem
    {
        [SerializeField] private TKey id;
        [SerializeField] private TValue texture;

        public TKey GetID() => id;
        public TValue GetTexture() => texture;
    }

    [SerializeField] SerializedDictionaryItem[] items;

    public Dictionary<TKey, TValue> ToDictionary()
    {
        Dictionary<TKey, TValue> newDict = new Dictionary<TKey, TValue>();

        foreach (var item in items)
        {
            newDict.Add(item.GetID(), item.GetTexture());
        }

        return newDict;
    }
}
}
