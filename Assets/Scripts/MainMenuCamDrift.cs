using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuCamDrift : MonoBehaviour
{
    // Start is called before the first frame update
    private Vector3 _newPosition;
    private Quaternion _newRotation;
    public float lerpSpeed;

    private void Awake()
    {
        _newPosition = transform.position;
        _newRotation = transform.rotation;
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(transform.position, _newPosition, Time.deltaTime * lerpSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, _newRotation, Time.deltaTime * lerpSpeed);

        if (Vector3.Distance(transform.position, _newPosition) < 1f)
        {
            GetNewPosition();
        }
    }

    private void GetNewPosition()
    {
        var xPos = UnityEngine.Random.Range(-10, 10);
        var yPos = UnityEngine.Random.Range(-10, 10);
        var zPos = UnityEngine.Random.Range(-10, 10);
        _newRotation = Quaternion.Euler(UnityEngine.Random.Range(-10, 10), UnityEngine.Random.Range(-10, 10), 0);
        _newPosition = new Vector3(transform.position.x + xPos, transform.position.y + yPos, transform.position.z + zPos);
    }
}
