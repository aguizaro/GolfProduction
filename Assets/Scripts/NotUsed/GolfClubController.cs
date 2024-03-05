using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GolfClubController : MonoBehaviour
{
    public Transform player; // Reference to the player object
    public float distance = 2f; // Distance from the player
    public float height = 1f; // Height above the player
    public float rotationSpeed = 5f; // Speed of rotation
    public float maxRotationAngle = 90f; // Maximum rotation angle

    private float currentRotation = 0f; // Current rotation of the golf club

    void Update()
    {
        // Get mouse input for rotation
        float mouseX = Input.GetAxis("Mouse X");

        // Calculate rotation based on mouse input
        float rotationAmount = mouseX * rotationSpeed;

        // Calculate the desired rotation relative to the player's right side
        currentRotation = Mathf.Clamp(currentRotation + rotationAmount, -maxRotationAngle, maxRotationAngle);

        // Set the rotation of the golf club
        transform.rotation = Quaternion.Euler(0f, player.eulerAngles.y + currentRotation, 0f);

        // Set position relative to the player
        transform.position = player.position + player.right * distance + player.up * height;
    }
}
