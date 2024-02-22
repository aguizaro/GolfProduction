using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GolfClubController : MonoBehaviour
{
    public Transform player;
    public float distance = 2f;
    public float height = 1f;
    public float rotationSpeed = 5f;
    public float maxRotationAngle = 90f;

    private float currentRotation = 0f;

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X");

        float rotationAmount = mouseX * rotationSpeed;

        currentRotation = Mathf.Clamp(currentRotation + rotationAmount, -maxRotationAngle, maxRotationAngle);

        transform.rotation = Quaternion.Euler(0f, player.eulerAngles.y + currentRotation, 0f);

        transform.position = player.position + player.right * distance + player.up * height;
    }
}
