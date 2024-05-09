using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Pool
{
    public GameObject Prefab => prefab;
    [SerializeField] GameObject prefab;
    [SerializeField] int size = 10;
    Queue<GameObject> queue;

    Transform parent;
    public void Initialize(Transform parent)
    {
        queue = new Queue<GameObject>();
        this.parent = parent;
        for (int i = 0; i < size; i++)
        {
            queue.Enqueue(Copy());
        }
    }

    public GameObject Copy()
    {
        var copy = GameObject.Instantiate(prefab, parent);
        copy.SetActive(false);
        return copy;
    }

    public GameObject GetAvalivaibleObject()
    {
        GameObject obj = null;
        if (queue.Count > 0 && !queue.Peek().activeSelf)
        {
            obj = queue.Dequeue();
        }
        else
        {
            obj = Copy();
        }
        ReturnPool(obj);
        return obj;
    }

    public GameObject PrepareObject()
    {
        GameObject obj = GetAvalivaibleObject();
        obj.SetActive(true);
        return obj;
    }

    public GameObject PrepareObject(Vector3 pos)
    {
        GameObject obj = GetAvalivaibleObject();
        obj.transform.position = pos;
        obj.SetActive(true);
        return obj;
    }

    public GameObject PrepareObject(Vector3 pos, Quaternion rot)
    {
        GameObject obj = GetAvalivaibleObject();
        obj.transform.position = pos;
        obj.transform.rotation = rot;
        obj.SetActive(true);
        return obj;
    }

    public GameObject PrepareObject(Vector3 pos, Quaternion rot, Vector3 loclScale)
    {
        GameObject obj = GetAvalivaibleObject();
        obj.transform.position = pos;
        obj.transform.rotation = rot;
        obj.transform.localScale = loclScale;
        obj.SetActive(true);
        return obj;
    }

    public void ReturnPool(GameObject obj)
    {
        queue.Enqueue(obj);
    }
}
