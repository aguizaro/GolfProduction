// written by chatgpt

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicPlayerController : MonoBehaviour
{
    public float moveSpeed = 6.0f;
    public float turnSpeed = 3.0f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    public float groundRaycastDistance = 1.1f; // Adjust this value based on your character's height

    private Vector3 moveDirection = Vector3.zero;
    private CharacterController controller;
    private float verticalSpeed = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Player Movement
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        moveDirection = new Vector3(horizontalInput, 0.0f, verticalInput);
        moveDirection = transform.TransformDirection(moveDirection);
        moveDirection *= moveSpeed;

        // Apply gravity
        if (controller.isGrounded)
        {
            verticalSpeed = 0f;
        }
        else
        {
            verticalSpeed -= gravity * Time.deltaTime;
        }

        // Jumping
        if (controller.isGrounded && Input.GetButton("Jump"))
        {
            verticalSpeed = jumpSpeed;
        }

        // Move the player along the ground
        moveDirection.y = verticalSpeed;

        // Perform ground raycast to adjust player position
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, groundRaycastDistance))
        {
            float targetHeight = hit.point.y + controller.height / 2f;
            if (targetHeight > transform.position.y)
            {
                transform.position = new Vector3(transform.position.x, targetHeight, transform.position.z);
            }
        }

        // Move the controller
        controller.Move(moveDirection * Time.deltaTime);

        // Player Rotation
        float mouseX = Input.GetAxis("Mouse X") * turnSpeed;

        // Rotate the player horizontally
        transform.Rotate(0, mouseX, 0);
    }
}
