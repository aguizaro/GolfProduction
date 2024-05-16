using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{

    [SerializeField] Pool[] projectilePools;
    static Dictionary<GameObject, Pool> dictionary;
    void Start()
    {
        dictionary = new Dictionary<GameObject, Pool>();
        Initialize(projectilePools);
    }

    void Initialize(Pool[] pools)
    {
        foreach (var pool in pools)
        {
#if UNITY_EDITOR
            //skip if key has already been added
            if (dictionary.ContainsKey(pool.Prefab)) {
                Debug.LogWarning("Pool key already exists: " + pool.Prefab.name);
                continue;
            }
#endif
            dictionary.Add(pool.Prefab, pool);
            Transform poolParent = new GameObject("Pool:" + pool.Prefab.name).transform;
            poolParent.parent = transform;
            pool.Initialize(poolParent);
        }
    }

    public static GameObject Release(GameObject prefab)
    {
#if UNIIY_EDITOR
        if(!dictionary.ContainsKey(prefab)){
        Debug.LogError(string.Format("No pool exists for prefab:", prefab.name));
            reutrn null;
        }
#endif
        return dictionary[prefab].PrepareObject();
    }

    public static GameObject Release(GameObject prefab, Vector3 pos)
    {
#if UNIIY_EDITOR
        if(!dictionary.ContainsKey(prefab)){
        Debug.LogError(string.Format("No pool exists for prefab:", prefab.name));
            reutrn null;
        }
#endif
        return dictionary[prefab].PrepareObject(pos);
    }

    public static GameObject Release(GameObject prefab, Vector3 pos, Quaternion rot)
    {
#if UNIIY_EDITOR
        if(!dictionary.ContainsKey(prefab)){
        Debug.LogError(string.Format("No pool exists for prefab:", prefab.name));
            reutrn null;
        }
#endif
        return dictionary[prefab].PrepareObject(pos, rot);
    }

    public static GameObject Release(GameObject prefab, Vector3 pos, Quaternion rot, Vector3 localScale)
    {
#if UNIIY_EDITOR
        if(!dictionary.ContainsKey(prefab)){
        Debug.LogError(string.Format("No pool exists for prefab:", prefab.name));
            reutrn null;
        }
#endif
        return dictionary[prefab].PrepareObject(pos, rot, localScale);
    }
}
