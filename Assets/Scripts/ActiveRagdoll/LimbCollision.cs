// Code inspired by https://www.youtube.com/watch?v=ZvNOt7I4C3I active ragdoll controller tutorial

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LimbCollision : MonoBehaviour
{
    public RagdollController playerController;

    private void Start()
    {
        playerController = GameObject.FindObjectOfType<RagdollController>().GetComponent<RagdollController>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        playerController.isGrounded = true;
    }
}
