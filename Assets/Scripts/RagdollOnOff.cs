using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class RagdollOnOff : NetworkBehaviour
{
    public CapsuleCollider mainCollider;
    public Rigidbody playerRB;
    public GameObject playerRig;
    private Animator _playerAnimator;
    private BasicPlayerController _basicPlayerController;
    private BoxCollider _golfClubCollider;
    //public GameObject _golfClubPrefab;
    //private GameObject _golfClubInstance;


    public float ragdollResetTimer = 300f;

    private bool isActive = false;

    // Activation -------------------------------------------------------------------------------------------------------------
    public void Activate()
    {
        isActive = true;
        ragdollColliders = playerRig.GetComponentsInChildren<Collider>();
        limbsRigidBodies = playerRig.GetComponentsInChildren<Rigidbody>();
        _basicPlayerController = GetComponent<BasicPlayerController>();
        _playerAnimator = GetComponent<Animator>();
        _golfClubCollider = GetComponentInChildren<BoxCollider>();

        if (IsOwner) ResetRagdoll(); // owners deactivate ragdoll using RPCs
        else RagdollModeOff(); // non owners deactivate ragdoll locally
    }


    // Update Loop -------------------------------------------------------------------------------------------------------------
    void Update()
    {

        if (!isActive) return; //prevent updates until player is fully activated

        Debug.Log("before: Trigger activated for " + OwnerClientId + " " + _golfClubCollider.enabled);

        if (!_golfClubCollider.enabled)
        {
            _golfClubCollider.enabled = true;
            _golfClubCollider.isTrigger = true;
            Debug.Log("after: Trigger activated for " + OwnerClientId + " " + _golfClubCollider.enabled);
        }

        if (!IsOwner) return;
        if (Input.GetKey("q")) PerformRagdoll();
        if (Input.GetKey("r")) ResetRagdoll();

    }


    // Ragdoll Logic ------------------------------------------------------------------------------------------------------------

    // Dev Note: Use this public function to activate the ragdoll mode
    public void PerformRagdoll()
    {
        Debug.Log("PerformRagdoll called for " + OwnerClientId);

        if (IsServer) RagdollModeOnClientRpc();
        else RagdollModeOnServerRpc();

        RagdollModeOn();
    }
    // Dev Note: Use this public function to deactivate the ragdoll mode
    public void ResetRagdoll()
    {
        if (IsServer) RagdollModeOffClientRpc();
        else RagdollModeOffServerRpc();

        RagdollModeOff();
    }

    Collider[] ragdollColliders;
    Rigidbody[] limbsRigidBodies;

    // Dev Note: Don't call this function directly. Use the RPCs instead. - this will only exectute locally
    void RagdollModeOn()
    {
        _playerAnimator.enabled = false;
        _basicPlayerController.enabled = false;

        foreach (Collider col in ragdollColliders)
        {
            col.enabled = true;
        }
        foreach (Rigidbody rb in limbsRigidBodies)
        {
            rb.isKinematic = false;
        }

        mainCollider.enabled = false;
        playerRB.isKinematic = true;

    }
    // Dev Note: Don't call this function directly. Use the RPCs instead. - this will only exectute locally
    void RagdollModeOff()
    {
        foreach (Collider col in ragdollColliders)
        {
            col.enabled = false;
        }
        foreach (Rigidbody rb in limbsRigidBodies)
        {
            rb.isKinematic = true;
        }

        _playerAnimator.enabled = true;
        _basicPlayerController.enabled = true;
        mainCollider.enabled = true;
        playerRB.isKinematic = false;
    }

    // Collision Detection ------------------------------------------------------------------------------------------------------------


    /*private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Collision detected from: " + collision.gameObject.name + " " + collision.gameObject.tag);
        if (!isActive) return;

        if (!IsOwner) // non-owners perform ragdolls locally (reading)
        {
            RagdollModeOn();
            //start timer
            float timer = ragdollResetTimer;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
            }
            RagdollModeOff();
            return;

        }
        // Owners perform ragdolls using RPCs (writing)
        if (collision.gameObject.tag == "PlayerCollision")
        {
            //ragdoll mode on
            PerformRagdoll();
            //start timer
            float timer = ragdollResetTimer;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
            }
            ResetRagdoll();
        }
    }*/

    // collision
    private void OnTriggerStay(Collider other)
    {
        Debug.Log("Trigger detected from: " + other.gameObject.name + " " + other.gameObject.tag);

        if (other.gameObject.CompareTag("Player"))
        {
            Debug.Log("Player " + other.gameObject.GetComponent<NetworkObject>().OwnerClientId + " is in the triggerVolume of player " + OwnerClientId);
            if (other.gameObject.GetComponent<Animator>().GetBool("isStriking") == true)
            {
                Debug.Log("Player " + other.gameObject.GetComponent<NetworkObject>().OwnerClientId + " has struck player " + OwnerClientId);

                if (!IsOwner) return;
                PerformRagdoll();

            }
        }
    }


    // Remote Procedure Calls ------------------------------------------------------------------------------------------------------------

    [ServerRpc]
    public void RagdollModeOnServerRpc()
    {
        Debug.Log("RagdollModeOnServerRpc called for " + OwnerClientId);
        RagdollModeOn();
    }

    [ServerRpc]
    public void RagdollModeOffServerRpc()
    {
        RagdollModeOff();
    }

    [ClientRpc]
    public void RagdollModeOnClientRpc()
    {
        Debug.Log("RagdollModeOnClientRpc called for " + OwnerClientId);
        RagdollModeOn();
    }

    [ClientRpc]
    public void RagdollModeOffClientRpc()
    {
        RagdollModeOff();
    }


}
