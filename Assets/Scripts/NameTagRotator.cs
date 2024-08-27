using TMPro;
using UnityEngine;

public class NameTagRotator : MonoBehaviour
{
    public Transform target;
    private Transform canvas;
    public float scaleFactor = 0.025f;
    private Vector3 offsetRot = new Vector3(0, 180, 0);
    void Start()
    {
        target = GameObject.Find("Main Camera").transform;
        canvas = transform.parent;
    }
    void LateUpdate()
    {
        transform.LookAt(target);
        transform.Rotate(offsetRot);

        float distance = Vector3.Distance(target.position, transform.position);
        canvas.transform.localScale = Vector3.one * (scaleFactor * distance);
    }

    public void UpdateNameTag(string name)
    {
        GetComponent<TMP_Text>().text = name;
    }

    public void UpdateColor(Color color)
    {
        GetComponent<TMP_Text>().color = color;
    }

    public string GetName()
    {
        return GetComponent<TMP_Text>().text;
    }
}
