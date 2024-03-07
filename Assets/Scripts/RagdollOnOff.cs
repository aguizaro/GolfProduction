using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RagdollOnOff : MonoBehaviour
{
    public CapsuleCollider mainCollider;
    public Rigidbody playerRB;
    public GameObject playerRig;
    public Animator playerAnimator;
    public GameObject playerClub;
    public BasicPlayerController BasicPlayerController;
    
    // PLAYER WILL RAGDOLL ON COLLISION WITH PlayerCollision TAG
    // PRESS R TO RESET RAGDOLL

    void Start()
    {
        GetRagdollBits();
        RagdollModeOff();
    }

    void Update()
    {
        if(Input.GetKey("r"))
        {
            RagdollModeOff();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "PlayerCollision")
        {
            RagdollModeOn();
        }
    }

    Collider[] ragdollColliders;
    Rigidbody[] limbsRigidBodies;

    void GetRagdollBits()
    {
        ragdollColliders = playerRig.GetComponentsInChildren<Collider>();
        limbsRigidBodies = playerRig.GetComponentsInChildren<Rigidbody>();
    }

    void RagdollModeOn()
    {
        playerAnimator.enabled = false;
        BasicPlayerController.enabled = false;

        foreach(Collider col in ragdollColliders)
        {
            col.enabled = true;
        }
        foreach(Rigidbody rb in limbsRigidBodies)
        {
            rb.isKinematic = false;
        }

        mainCollider.enabled = false;
        playerRB.isKinematic = true;

    }

    void RagdollModeOff()
    {
        foreach(Collider col in ragdollColliders)
        {
            col.enabled = false;
        }
        foreach(Rigidbody rb in limbsRigidBodies)
        {
            rb.isKinematic = true;
        }

        playerAnimator.enabled = true;
        BasicPlayerController.enabled = true;
        mainCollider.enabled = true;
        playerRB.isKinematic = false;
    }

}
