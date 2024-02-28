// Code inspired by https://www.youtube.com/watch?v=ZvNOt7I4C3I active ragdoll controller tutorial

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RagdollController : MonoBehaviour
{
    public Animator animator;

    public float speed;
    public float strafeSpeed;
    public float maxSpeed;
    public float jumpForce;

    public Rigidbody hips;
    public bool isGrounded;

    private float currentMaxSpeed;

    void Start()
    {
        hips = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        // Calculate current velocity magnitude
        float currentSpeed = hips.velocity.magnitude;

        // Check if current speed exceeds maximum speed
        if (currentSpeed > maxSpeed)
        {
            // If so, clamp the velocity to the maximum speed
            hips.velocity = hips.velocity.normalized * maxSpeed;
        }

        // In the future: have animation states be controlled by procedural checks on speed,
        // object position, and animation states. Rather than controlled by keystrokes.
        if(Input.GetKey(KeyCode.W))
        {
            if(Input.GetKey(KeyCode.LeftShift))
            {
                animator.SetBool("isWalk", true);
                animator.SetBool("isRun", true);
                currentMaxSpeed = maxSpeed * 1.5f;
                hips.AddForce(hips.transform.forward * speed * 1.5f);
            }else 
            {
                animator.SetBool("isRun", false);
                animator.SetBool("isWalk", true);
                currentMaxSpeed = maxSpeed;
                hips.AddForce(hips.transform.forward * speed);
            }
        }else
        {
            animator.SetBool("isWalk", false);
            animator.SetBool("isRun", false);
        }

        if(Input.GetKey(KeyCode.A))
        {
            animator.SetBool("isStrafeLeft", true);
            hips.AddForce(-hips.transform.right * strafeSpeed);
        }else
        {
            animator.SetBool("isStrafeLeft", false);
        }

        if(Input.GetKey(KeyCode.S))
        {
            animator.SetBool("isWalk", true);
            hips.AddForce(-hips.transform.forward * speed);
        }else if(!Input.GetKey(KeyCode.W))
        {
            animator.SetBool("isWalk", false);
        }

        if(Input.GetKey(KeyCode.D))
        {
            animator.SetBool("isStrafeRight", true);
            hips.AddForce(hips.transform.right * strafeSpeed);
        }else
        {
            animator.SetBool("isStrafeRight", false);
        }

        if(Input.GetAxis("Jump") > 0)
        {
            if(isGrounded)
            {
                hips.AddForce(new Vector3(0, jumpForce, 0));
                isGrounded = false;
            }
        }


    }
}
