using UnityEngine;
using Unity.Netcode;

public class StartCameraFollow : NetworkBehaviour
{
    public float followSpeed = 100f;
    public float xCamRotation = 15f;
    public Vector3 camOffset = new(0f, -2f, 5f);
    private bool isActive = false;
    private bool isSwingState = false;

    private bool transitioning = false;
    private float transitionProgress = 0f;
    private Vector3 regularPosition;
    private Quaternion regularRotation;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private float transitionSpeed = 1f;

    private bool isFirstFrame = true;

    private RagdollOnOff ragdollOnOff;
    private Transform targetTransform;

    private Transform currentFollowTarget;
    public Transform newFollowTarget;
    private bool isFollowTransitioning = false;
    private float followTransitionProgress = 0f;
    [SerializeField]
    private float followTransitionSpeed = 3f; // Adjust the speed of transition between follow targets


    public override void OnDestroy()
    {
        Deactivate();
        base.OnDestroy();
    }


    // Camera will only follow player when Active = true
    public void Activate()
    {
        if (!IsOwner) return;
        
        isActive = true;
        // Initialize camera to regular position and rotation
        regularPosition = transform.position - (Quaternion.Euler(xCamRotation, transform.eulerAngles.y, 0f) * camOffset);
        regularRotation = Quaternion.Euler(xCamRotation, transform.eulerAngles.y, 0f);
        Camera.main.transform.SetPositionAndRotation(regularPosition, regularRotation);

        // Get the RagdollOnOff component
        ragdollOnOff = GetComponent<RagdollOnOff>();
        if (ragdollOnOff != null)
        {
            currentFollowTarget = ragdollOnOff.mainCollider.transform; // Start with mainCollider as the target
        }
    }

    public void Deactivate()
    {
        if (IsOwner) isActive = false;
    }


    private void LateUpdate()
    {
        if (!isActive) return;

        // Determine the new follow target based on the alreadyLaunched state
        if (ragdollOnOff != null && ragdollOnOff.alreadyLaunched)
        {
            newFollowTarget = ragdollOnOff._hipsBone;
        }
        else
        {
            newFollowTarget = ragdollOnOff.mainCollider.transform;
        }

        // Smooth transition between follow targets
        if (newFollowTarget != currentFollowTarget)
        {
            if (!isFollowTransitioning)
            {
                isFollowTransitioning = true;
                followTransitionProgress = 0f;
            }

            followTransitionProgress += Time.deltaTime * followTransitionSpeed;
            if (followTransitionProgress >= 1f)
            {
                followTransitionProgress = 1f;
                isFollowTransitioning = false;
                currentFollowTarget = newFollowTarget;
            }
        }
        else
        {
            followTransitionProgress = 1f;
        }

        Vector3 followPosition = Vector3.Lerp(currentFollowTarget.position, newFollowTarget.position, followTransitionProgress);
        Quaternion followRotation = Quaternion.Slerp(currentFollowTarget.rotation, newFollowTarget.rotation, followTransitionProgress);

        if (isSwingState)
        {
            if (!transitioning)
            {
                // Start transition
                transitioning = true;
                transitionProgress = 0f;
                regularPosition = Camera.main.transform.position;
                regularRotation = Camera.main.transform.rotation;

                // Calculate camera position and rotation for alternate mode
                float angle = followRotation.eulerAngles.y;
                Quaternion camRotation = Quaternion.Euler(xCamRotation, angle, 0f);

                // Define the offset for the alternate mode (forward and downward)
                Vector3 alternateCamOffset = new Vector3(0f, -0.5f, 1.5f); // Adjust these values as needed

                // Calculate the target position using the alternate offset
                targetPosition = followPosition - (camRotation * alternateCamOffset);

                // Keep the same rotation as regular mode
                targetRotation = camRotation;
            }

            // Interpolate between regular and alternate mode
            Camera.main.transform.position = Vector3.Lerp(regularPosition, targetPosition, transitionProgress);
            Camera.main.transform.rotation = Quaternion.Slerp(regularRotation, targetRotation, transitionProgress);

            // Update transition progress
            transitionProgress += Time.deltaTime * transitionSpeed;
            transitionProgress = Mathf.Clamp01(transitionProgress);
        }
        else
        {
            if (transitioning)
            {
                // Start transition back to regular mode
                transitioning = false;
                transitionProgress = 0f;
                regularPosition = Camera.main.transform.position;
                regularRotation = Camera.main.transform.rotation;
            }

            // Calculate camera position and rotation for regular follow mode
            float angle = followRotation.eulerAngles.y;
            Quaternion camRotation = Quaternion.Euler(xCamRotation, angle, 0f);
            targetPosition = followPosition - (camRotation * camOffset);
            targetRotation = camRotation;

            // Smoothly interpolate camera position and rotation
            Camera.main.transform.position = Vector3.Lerp(regularPosition, targetPosition, transitionProgress);
            Camera.main.transform.rotation = Quaternion.Lerp(regularRotation, targetRotation, transitionProgress);

            // Update transition progress
            transitionProgress += Time.deltaTime * transitionSpeed;
            transitionProgress = Mathf.Clamp01(transitionProgress);
        }
    }

    public void SetSwingState(bool swing)
    {
        isSwingState = swing;
    }
}
