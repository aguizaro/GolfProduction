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
    public PlayerNetworkData _playerNetworkData;
    private BoxCollider _golfClubCollider;
    private SwingManager _swingManager;

    private float ragdollDelay = 0.5f; //Slight delay defore ragdoll mode is activated
    private float getUpDelay = 12f;
    private float delay;
    private bool isRagdoll = false; //is player in ragdoll mode
    private bool isActive = false; //is player instance active
    private Transform _hipsBone;

    private bool firstDone = false;
    public bool alreadyLaunched = false;

    private BoneTransform[] _standUpBoneTransforms;
    private BoneTransform[] _ragdollBoneTransforms;
    private Transform[] _bones;

    private float _timeToResetBones = 0.5f;
    private float _elapsedResetBonesTime;


    private class BoneTransform
    {
        public Vector3 Position { get; set; }

        public Quaternion Rotation { get; set; }
    }

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

        _bones = _hipsBone.GetComponentsInChildren<Transform>();
        _standUpBoneTransforms = new BoneTransform[_bones.Length];
        _ragdollBoneTransforms = new BoneTransform[_bones.Length];

        for (int boneIndex = 0; boneIndex < _bones.Length; boneIndex++)
        {
            _standUpBoneTransforms[boneIndex] = new BoneTransform();
            _ragdollBoneTransforms[boneIndex] = new BoneTransform();
        }

        PopulateAnimationStartBoneTransforms("StandUp", _standUpBoneTransforms);

        // Find and reference the players hips bone
        foreach (Transform child in transform)
        {
            if (child.CompareTag("Hips"))
            {
                _hipsBone = child;
                break;
            }
        }

        foreach (Collider col in ragdollColliders)
        {
            col.enabled = false;
        }
        foreach (Rigidbody rb in limbsRigidBodies)
        {
            if (rb != playerRB) rb.isKinematic = true;
        }

        _playerAnimator.enabled = true;
        //_basicPlayerController.enabled = true;
        mainCollider.enabled = true;
        playerRB.isKinematic = false;
        isRagdoll = false;
        playerRB.useGravity = true;

    }

    public void Deactivate() => isActive = false;


    // Update Loop -------------------------------------------------------------------------------------------------------------
    void Update()
    {
        if (!isActive) return; //prevent updates until player is fully activated
        if (!IsOwner) return;

        if (!firstDone)
        {
            firstDone = true;
            PerformRagdoll();
            ResetRagdoll();
            Debug.Log("Finished first hack fix");
        }

        if (isRagdoll) //auto reset ragdoll after delay
        {
            if (_basicPlayerController.canInput) _basicPlayerController.DisableInput(); //disable input while in ragdoll mode

            delay -= Time.deltaTime;
            if (delay <= 0)
            {
                delay = getUpDelay;
                ResetRagdoll();

                Debug.Log("Update: after reset ragdoll: pos: " + transform.position);

            }
        }
        
        if(Input.GetKeyDown(KeyCode.T))
        {
            RagdollModeOn();
        }
        if(Input.GetKeyDown(KeyCode.Y))
        {
            RagdollModeOff();
        }
    }


    // Ragdoll Logic ------------------------------------------------------------------------------------------------------------

    // Dev Note: Use this public function to activate the ragdoll mode
    public void PerformRagdoll()
    {
        if (IsServer) RagdollModeOnClientRpc();
        else RagdollModeOnServerRpc();


    }
    // Dev Note: Use this public function to deactivate the ragdoll mode
    public void ResetRagdoll()
    {
        if (IsServer) RagdollModeOffClientRpc();
        else RagdollModeOffServerRpc();
    }

    Collider[] ragdollColliders;
    Rigidbody[] limbsRigidBodies;

    // Dev Note: Don't call this function directly. Use the RPCs instead. - this will only exectute locally
    void RagdollModeOn()
    {

        if (isRagdoll) return; //don't activate if already in ragdoll mode

        delay = getUpDelay; // reset delay every time ragdoll mode is activated - avoids instant reset
        if (_swingManager.isInSwingState())
        {
            _swingManager.ExitSwingMode();
        }
        _playerAnimator.enabled = false;
        //_basicPlayerController.enabled = false;
        _basicPlayerController.DisableInput(); // it would be nice to disable input but still allow the player to move the camera (only allow input rotation)

        foreach (Collider col in ragdollColliders)
        {
            col.enabled = true;
        }
        foreach (Rigidbody rb in limbsRigidBodies)
        {
            if (rb != playerRB) rb.isKinematic = false;
        }

        mainCollider.enabled = false;
        playerRB.isKinematic = true;
        isRagdoll = true;

        Debug.Log("RagdollModeOn done for owner: " + OwnerClientId + " isOwner: " + IsOwner);

    }
    // Dev Note: Don't call this function directly. Use the RPCs instead. - this will only exectute locally
    void RagdollModeOff()
    {

        if (!isRagdoll) return; //don't deactivate if not in ragdoll mode

        foreach (Collider col in ragdollColliders)
        {
            col.enabled = false;
        }
        foreach (Rigidbody rb in limbsRigidBodies)
        {
            if (rb != playerRB) rb.isKinematic = true;
        }

        _playerAnimator.enabled = true;

        // Update the main colliders position to the hips using helper function
        AlignMainColliderToHips();
        PopulateBoneTransforms(_ragdollBoneTransforms);

        _elapsedResetBonesTime = 0;
        ResetBones();
    }

    // Collision Detection ------------------------------------------------------------------------------------------------------------

    // we might only need ontriggerstay - but it needs testing
    private void OnTriggerStay(Collider other)
    {
        RagdollTrigger(other);
    }
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("OnTriggerEnter: " + other.gameObject.name);
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
                Debug.Log("RagdollTrigger: got hit by player strike");
                if (!IsOwner) return;
                Debug.Log("RagdollTrigger: Owner - perform ragdoll");
                PerformRagdoll();
            }
        }
    }


    // Remote Procedure Calls ------------------------------------------------------------------------------------------------------------

    [ServerRpc]
    public void RagdollModeOnServerRpc()
    {

        RagdollModeOnClientRpc();
    }

    [ServerRpc]
    public void RagdollModeOffServerRpc()
    {
        RagdollModeOffClientRpc();
    }

    [ClientRpc]
    public void RagdollModeOnClientRpc()
    {

        RagdollModeOn();
    }

    [ClientRpc]
    public void RagdollModeOffClientRpc()
    {
        RagdollModeOff();
    }

    [ServerRpc]
    private void AddForceToSelfServerRpc(Vector3 force)
    {
        AddForceToSelfClientRpc(force);
    }

    [ClientRpc]
    private void AddForceToSelfClientRpc(Vector3 force)
    {
        foreach (Rigidbody limb in limbsRigidBodies)
        {
            if (limb != playerRB) limb.AddForce(force, ForceMode.Impulse);
        }
        alreadyLaunched = true;
        Debug.Log($"Already launched: {alreadyLaunched} for owner: {OwnerClientId} isOwner: {IsOwner}");
    }

    // helper functions -----------------------------------------
    private void ResetBones()
    {
        _elapsedResetBonesTime += Time.deltaTime;
        float elapsedPercentage = _elapsedResetBonesTime / _timeToResetBones;

        for (int boneIndex = 0; boneIndex < _bones.Length; boneIndex++)
        {
            _bones[boneIndex].localPosition = Vector3.Lerp(
                _ragdollBoneTransforms[boneIndex].Position,
                _standUpBoneTransforms[boneIndex].Position,
                elapsedPercentage);
            
            _bones[boneIndex].localRotation = Quaternion.Lerp(
                _ragdollBoneTransforms[boneIndex].Rotation,
                _standUpBoneTransforms[boneIndex].Rotation,
                elapsedPercentage);
        }

        if (elapsedPercentage >= 1)
        {
            _playerAnimator.Play("StandUp");
            // Execute rest of the logic after the standup animation
            StartCoroutine(WaitForAnimationAndExecuteLogic("StandUp"));
        }
    }

    private void AlignMainColliderToHips()
    {
        Vector3 originalHipsPosition = _hipsBone.position;
        transform.position = _hipsBone.position;

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hitInfo))
        {
            transform.position = new Vector3(transform.position.x, hitInfo.point.y, transform.position.z);
        }

        _hipsBone.position = originalHipsPosition;
    }

    private void PopulateBoneTransforms(BoneTransform[] boneTransforms)
    {
        for (int boneIndex = 0; boneIndex < _bones.Length; boneIndex++)
        {
            boneTransforms[boneIndex].Position = _bones[boneIndex].localPosition;
            boneTransforms[boneIndex].Rotation = _bones[boneIndex].localRotation;
        }
    }

    private void PopulateAnimationStartBoneTransforms(string clipName, BoneTransform[] boneTransforms)
    {
        Vector3 positionBeforeSampling = transform.position;
        Quaternion rotationBeforeSampling = transform.rotation;

        foreach (AnimationClip clip in _playerAnimator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
            {
                clip.SampleAnimation(gameObject, 0);
                PopulateBoneTransforms(_standUpBoneTransforms);
                break;
            }
        }

        transform.position = positionBeforeSampling;
        transform.rotation = rotationBeforeSampling;
    }

    
    //
    private IEnumerator WaitForAnimationAndExecuteLogic(string animationName)
    {
        AnimatorStateInfo animationState = _playerAnimator.GetCurrentAnimatorStateInfo(0);

        while (!animationState.IsName(animationName))
        {
            yield return null;
            animationState = _playerAnimator.GetCurrentAnimatorStateInfo(0);
        }

        while (animationState.normalizedTime < 0.62f)
        {
            yield return null;
            animationState = _playerAnimator.GetCurrentAnimatorStateInfo(0);
        }
        // Animation finished, execute further logic
        _basicPlayerController.EnableInput();
        mainCollider.enabled = true;
        playerRB.isKinematic = false;
        isRagdoll = false;
        alreadyLaunched = false;
        Debug.Log($"Already launched: {alreadyLaunched} for owner: {OwnerClientId} isOwner: {IsOwner}");
        StartCoroutine(DelayedGravityActivation());
    }

    // this coroutine is required to set the gravity after a delay - if the gravity is immediately set true, the player will not have its position updated correctly - this is a hack fix
    private IEnumerator DelayedGravityActivation()
    {
        yield return new WaitForSeconds(ragdollDelay);
        playerRB.useGravity = true;
    }

    // public functions ------------------------------------------------------------------------------------------------------------

    public bool IsRagdoll()
    {
        return isRagdoll;
    }

    public void AddForceToSelf(Vector3 force)
    {
        if (IsOwner)
        {
            delay = getUpDelay; //reset delay to avoid instant reset after force is applied
            AddForceToSelfServerRpc(force * 2.5f);
            playerRB.useGravity = false;
            playerRB.isKinematic = false;
        }
    }

}
