using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;

public class RagdollOnOff : NetworkBehaviour
{
    public CapsuleCollider mainCollider;
    public Rigidbody playerRB;
    public GameObject playerRig;
    private Animator _playerAnimator;
    private BasicPlayerController _basicPlayerController;
    public PlayerNetworkData _playerNetworkData;
    private BoxCollider _golfClubCollider;
    private SwingManager _swingManager;

    public float ragdollDelay = 0.5f; //Slight delay defore ragdoll mode is activated
    public float getUpDelay = 10f;
    private float delay;
    private bool isRagdoll = false; //is player in ragdoll mode
    private bool isActive = false; //is player instance active



    // Activation -------------------------------------------------------------------------------------------------------------
    public void Activate()
    {
        isActive = true;
        ragdollColliders = playerRig.GetComponentsInChildren<Collider>();
        limbsRigidBodies = playerRig.GetComponentsInChildren<Rigidbody>();
        _basicPlayerController = GetComponent<BasicPlayerController>();
        _playerAnimator = GetComponent<Animator>();
        _golfClubCollider = GetComponentInChildren<BoxCollider>();
        _swingManager = GetComponentInChildren<SwingManager>();
        delay = getUpDelay;

        if (IsOwner) ResetRagdoll(); // owners deactivate ragdoll using RPCs
        else RagdollModeOff(); // non owners deactivate ragdoll locally
    }

    public void Deactivate() => isActive = false;


    // Update Loop -------------------------------------------------------------------------------------------------------------
    void Update()
    {

        if (!isActive) return; //prevent updates until player is fully activated

        if (!_golfClubCollider.enabled) //not sure why box collider spawns in as not active but i force it here
        {
            _golfClubCollider.enabled = true;
            _golfClubCollider.isTrigger = true;
        }

        if (!IsOwner) return;

        // dev cheat keys
        if (Input.GetKey("q")) PerformRagdoll();
        if (Input.GetKey("r")) ResetRagdoll();

        if (isRagdoll) //auto reset ragdoll after delay
        {
            delay -= Time.deltaTime;
            if (delay <= 0)
            {
                delay = getUpDelay;
                ResetRagdoll();
            }
        }

    }


    // Ragdoll Logic ------------------------------------------------------------------------------------------------------------

    // Dev Note: Use this public function to activate the ragdoll mode
    public void PerformRagdoll()
    {
        //Debug.Log("PerformRagdoll called for " + OwnerClientId);

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
        if (_swingManager.isInSwingState())
        {
            _swingManager.ExitSwingMode();
        }
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
        isRagdoll = true;


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
        isRagdoll = false;
    }

    // Collision Detection ------------------------------------------------------------------------------------------------------------
    private void OnTriggerStay(Collider other)
    {
        RagdollTrigger(other);
    }
    private void OnTriggerEnter(Collider other)
    {
        RagdollTrigger(other);
    }
    private void OnTriggerExit(Collider other)
    {
        RagdollTrigger(other);
    }

    // called by trigger events
    private void RagdollTrigger(Collider other)
    {
        if (isRagdoll) return; // don't detect collisions while in ragdoll mode
        if (other.gameObject.CompareTag("Player"))
        {
            if (!isRagdoll && other.gameObject.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Strike"))
            {
                if (!IsOwner) return;
                PerformRagdoll();
            }
        }
    }



    // Remote Procedure Calls ------------------------------------------------------------------------------------------------------------

    [ServerRpc]
    public void RagdollModeOnServerRpc()
    {
        //Debug.Log("RagdollModeOnServerRpc called for " + OwnerClientId);
        RagdollModeOnClientRpc();
        RagdollModeOn();
    }

    [ServerRpc]
    public void RagdollModeOffServerRpc()
    {
        RagdollModeOffClientRpc();
        RagdollModeOff(); // this may be redundant - idk if we need to call this on the server since it will be called on each client anyway
    }

    [ClientRpc]
    public void RagdollModeOnClientRpc()
    {
        //Debug.Log("RagdollModeOnClientRpc called for " + OwnerClientId);
        RagdollModeOn();
    }

    [ClientRpc]
    public void RagdollModeOffClientRpc()
    {
        RagdollModeOff();
    }


    // public functions ------------------------------------------------------------------------------------------------------------

    // public function to check if ragdoll mode is active
    public bool IsRagdoll()
    {
        return isRagdoll;
    }

    public void AddForceToSelf(Vector3 force)
    {
        Debug.Log("RagDollOnOff: AddForceToSelf running on player " + OwnerClientId + " from client " + NetworkManager.Singleton.LocalClientId + " is owner: " + IsOwner);

        if (IsOwner)
        {
            playerRB.isKinematic = false; //this needs to be set back to true later but idk how to find out when the force is done being applied

            playerRB.AddForce(new Vector3(0, 100, 0), ForceMode.Impulse);

        }
    }

    /*
    [ServerRpc]
    private void AddForceToSelfServerRpc(Vector3 force)
    {
        Debug.Log("RagDollOnOff: AddForceToSelfServerRpc running on " + OwnerClientId + " from client " + NetworkManager.Singleton.LocalClientId);
        AddForceToSelfClientRpc(force);
    }

    [ClientRpc]
    private void AddForceToSelfClientRpc(Vector3 force)
    {
        Debug.Log("RagDollOnOff: AddForceToSelfClientRpc running on " + OwnerClientId + " from client " + NetworkManager.Singleton.LocalClientId);
        playerRB.isKinematic = false; //this needs to be set back to true later but idk how to find out when the force is done being applied
        foreach (Rigidbody rb in playerRB.gameObject.GetComponentsInChildren<Rigidbody>())
        {
            rb.AddForce(force);
            Debug.Log("Added force to " + rb.gameObject.name + " with force " + force);
        }
        //playerRB.AddForce(new Vector3(0, 100, 0), ForceMode.Impulse);
    }
    */

}
