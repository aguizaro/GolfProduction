using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;
using Unity.VisualScripting;
using UnityEditor.TerrainTools;
using System.Threading.Tasks;

public class RagdollOnOff : NetworkBehaviour
{
    public CapsuleCollider mainCollider;
    public Rigidbody playerRB;
    public GameObject playerRig;
    private Animator _playerAnimator;
    private BasicPlayerController _basicPlayerController;
    public PlayerNetworkData _playerNetworkData;
    private SwingManager _swingManager;

    private float ragdollDelay = 0.5f; //Slight delay defore ragdoll mode is activated
    private float getUpDelay = 12f;
    private float delay;
    private bool isRagdoll = false; //is player in ragdoll mode
    private bool isActive = false; //is player instance active
    public Transform _hipsBone;

    public bool alreadyLaunched = false;
    public bool beingLaunched = false; //this will be true if another player has entered swing state on this player

    private BoneTransform[] _standUpBoneTransforms;
    private BoneTransform[] _standUp2BoneTransforms;
    private BoneTransform[] _ragdollBoneTransforms;
    private Transform[] _bones;
    private bool _isFacingUp;

    [SerializeField]
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
        _swingManager = GetComponentInChildren<SwingManager>();
        delay = getUpDelay;

        _hipsBone = playerRig.transform;

        _bones = _hipsBone.GetComponentsInChildren<Transform>();
        _standUpBoneTransforms = new BoneTransform[_bones.Length];
        _standUp2BoneTransforms = new BoneTransform[_bones.Length];
        _ragdollBoneTransforms = new BoneTransform[_bones.Length];

        for (int boneIndex = 0; boneIndex < _bones.Length; boneIndex++)
        {
            _standUpBoneTransforms[boneIndex] = new BoneTransform();
            _standUp2BoneTransforms[boneIndex] = new BoneTransform();
            _ragdollBoneTransforms[boneIndex] = new BoneTransform();
        }

        PopulateAnimationStartBoneTransforms("StandUp", _standUpBoneTransforms);
        PopulateAnimationStartBoneTransforms("StandUp2", _standUp2BoneTransforms);

        foreach (Collider col in ragdollColliders)
        {
            col.enabled = false;
        }
        foreach (Rigidbody rb in limbsRigidBodies)
        {
            if (rb != playerRB) rb.isKinematic = true;
        }

        _playerAnimator.enabled = true;
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

        if (isRagdoll) //auto reset ragdoll after delay
        {

            delay -= Time.deltaTime;
            if (delay <= 0)
            {
                delay = getUpDelay;
                ResetRagdoll();
            }
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            PerformRagdoll();
        }
        if (Input.GetKeyDown(KeyCode.Y))
        {
            ResetRagdoll();
        }


        if (Input.GetKeyDown(KeyCode.U))
        {
            PerformRagdoll();
            AddForceToSelf(Vector3.forward * 30);
        }
        if (Input.GetKeyDown(KeyCode.I))
        {
            UnParentHips();
        }
        if (Input.GetKeyDown(KeyCode.O))
        {
            ReParentHips();
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            StartCoroutine(ResetBonesCoroutine());
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            transform.position = _hipsBone.position;
        }
    }
    // Parenting Logic ------------------------------------------------------------------------------------------------------------

    private bool UnParentHips(){
        
        Vector3 hipsPosition = _hipsBone.position;
        Quaternion hipsRotation = _hipsBone.rotation;
        _hipsBone.parent = null;

        _hipsBone.position = hipsPosition;
        _hipsBone.rotation = hipsRotation;

        return _hipsBone.parent == null;
    }

    private bool ReParentHips(){
        _hipsBone.parent = transform;
        return _hipsBone.parent == transform;
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
        _basicPlayerController.canMove = false; // it would be nice to disable input but still allow the player to move the camera (only allow input rotation)

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

        // dont send rotation updates while in ragdoll mode
        GetComponent<CustomClientNetworkTransform>().SyncRotAngleY = false;

    }
    // Dev Note: Don't call this function directly. Use the RPCs instead. - this will only exectute locally
    async void RagdollModeOff()
    {
        if (!isRagdoll) return; //don't deactivate if not in ragdoll mode

        _basicPlayerController.canMove = false;
        _basicPlayerController.canLook = false;

        await Task.Delay(500);
        if (!UnParentHips()) {Debug.LogError("Hips could not be unparented"); return;}
        transform.position = _hipsBone.position;
        Debug.Log($"Unparented hips -- hips position: {_hipsBone.position} transform position: {transform.position}");
        await Task.Delay(500);
        if (!ReParentHips()) {Debug.LogError("Hips could not be reparented"); return;}
        Debug.Log($"Reparented hips -- hips position: {_hipsBone.position} transform position: {transform.position}");


        _isFacingUp = _hipsBone.forward.y > 0;

        foreach (Collider col in ragdollColliders)
        {
            col.enabled = false;
        }
        foreach (Rigidbody rb in limbsRigidBodies)
        {
            if (rb != playerRB) rb.isKinematic = true;
        }

        // Update the main colliders position to the hips using helper function
        //AlignRotationToHips();
        //AlignMainColliderToHips();
        PopulateBoneTransforms(_ragdollBoneTransforms);

        _elapsedResetBonesTime = 0;
        StartCoroutine(ResetBonesCoroutine());

        // restore rotation updates
        GetComponent<CustomClientNetworkTransform>().SyncRotAngleY = true;
    }

    // Collision Detection ------------------------------------------------------------------------------------------------------------

    // we might only need ontriggerstay - but it needs testing
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
                //Debug.Log("RagdollTrigger: got hit by player strike");
                if (!IsOwner) return;
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

        //Debug.Log($"Already launched: {alreadyLaunched} for owner: {OwnerClientId} isOwner: {IsOwner}");
    }

    // helper functions -----------------------------------------
    private IEnumerator ResetBonesCoroutine()
    {
        _elapsedResetBonesTime = 0f;
        float elapsedPercentage = 0f;

        while (elapsedPercentage < 1f)
        {
            _elapsedResetBonesTime += Time.deltaTime;
            elapsedPercentage = _elapsedResetBonesTime / _timeToResetBones;

            BoneTransform[] standUpBoneTransforms = GetStandUpBoneTransforms();
            for (int boneIndex = 0; boneIndex < _bones.Length; boneIndex++)
            {
                _bones[boneIndex].localPosition = Vector3.Lerp(
                    _ragdollBoneTransforms[boneIndex].Position,
                    standUpBoneTransforms[boneIndex].Position,
                    elapsedPercentage);

                _bones[boneIndex].localRotation = Quaternion.Lerp(
                    _ragdollBoneTransforms[boneIndex].Rotation,
                    standUpBoneTransforms[boneIndex].Rotation,
                    elapsedPercentage);
            }

            //Debug.Log(elapsedPercentage + " elapsedPercentage");
            yield return null; // Wait for the next frame
        }

        _playerAnimator.enabled = true;
        _playerAnimator.Play(GetStandUpStateName(), 0, 0);
        // Execute rest of the logic after the standup animation
        StartCoroutine(WaitForAnimationAndExecuteLogic(GetStandUpStateName()));
    }

    private void AlignMainColliderToHips()
    {
        Vector3 originalHipsPosition = GameObject.FindWithTag("Hips").transform.position;
        //find dist between hips and pos
        float dist = Vector3.Distance(transform.position, originalHipsPosition);
        if (dist < 100) 
        {
            transform.position = originalHipsPosition;
        }

        /*
        // This section is meant to put the hips in the right spot to prevent the little amount of sliding that happens \
        // before standing up, but the position is not correct.
        Vector3 positionOffset = GetStandUpBoneTransforms()[0].Position;
        positionOffset.y = 0;
        positionOffset = transform.position * positionOffset;
        transform.position -= positionOffset;
        */

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hitInfo))
        {
            transform.position = new Vector3(transform.position.x, hitInfo.point.y, transform.position.z);
        }

        _hipsBone.position = originalHipsPosition;
    }

    private void AlignRotationToHips()
    {
        Vector3 originalHipsPosition = _hipsBone.position;
        Quaternion originalHipsRotation = _hipsBone.rotation;

        Vector3 desiredDirection = _hipsBone.up;
        if (_isFacingUp)
        {
            desiredDirection *= -1;
        }

        desiredDirection.y = 0; // Flatten the direction on the y-axis
        desiredDirection.Normalize();

        Quaternion fromToRotation = Quaternion.FromToRotation(transform.forward, desiredDirection);
        transform.rotation *= fromToRotation;

        _hipsBone.position = originalHipsPosition;
        _hipsBone.rotation = originalHipsRotation;
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
                PopulateBoneTransforms(boneTransforms);
                break;
            }
        }

        //transform.position = positionBeforeSampling;
        //transform.rotation = rotationBeforeSampling;
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
        _basicPlayerController.canMove = true;
        _basicPlayerController.canLook = true;

        mainCollider.enabled = true;
        playerRB.isKinematic = false;
        isRagdoll = false;
        alreadyLaunched = false;
        StartCoroutine(DelayedGravityActivation());
    }

    // this coroutine is required to set the gravity after a delay - if the gravity is immediately set true, the player will not have its position updated correctly - this is a hack fix
    private IEnumerator DelayedGravityActivation()
    {
        yield return new WaitForSeconds(ragdollDelay);
        playerRB.useGravity = true;
    }

    private string GetStandUpStateName()
    {
        if (_isFacingUp) { return "StandUp2"; } else return "StandUp";
    }

    private BoneTransform[] GetStandUpBoneTransforms()
    {
        if (_isFacingUp)
        {
            return _standUp2BoneTransforms;
        }
        else return _standUpBoneTransforms;
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
