using UnityEngine;
using Unity.Netcode;

public class StartCameraFollow : NetworkBehaviour
{
    public float followSpeed = 100f;
    public float xCamRotation = 15f;
    public Vector3 camOffset = new(0f, -2f, 5f);
    private bool isActive = true;
    private bool isSwingState = false;

    // transitioning between camera states
    private bool transitioning = false;
    private float transitionProgress = 0f;
    private Vector3 regularPosition;
    private Quaternion regularRotation;
    private float transitionSpeed = 1f;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) isActive = false;
    }

    private void LateUpdate()
    {
        if (!isActive) return;

        if (isSwingState)
        {
            if (!transitioning)
            {
                // Start transition
                transitioning = true;
                transitionProgress = 0f;
                regularPosition = Camera.main.transform.position;
                regularRotation = Camera.main.transform.rotation;
            }

            // Calculate camera position and rotation for alternate mode
            float angle = transform.eulerAngles.y;
            Quaternion camRotation = Quaternion.Euler(xCamRotation, angle, 0f);

            // Define the offset for the alternate mode (forward and downward)
            Vector3 alternateCamOffset = new Vector3(0f, -0.5f, 1.5f); // Adjust these values as needed

            // Calculate the target position using the alternate offset
            Vector3 targetPosition = transform.position - (camRotation * alternateCamOffset);

            // Keep the same rotation as regular mode
            Quaternion targetRotation = camRotation;

            // Interpolate between regular and alternate mode
            Camera.main.transform.position = Vector3.Lerp(regularPosition, targetPosition, transitionProgress);
            Camera.main.transform.rotation = Quaternion.Slerp(regularRotation, targetRotation, transitionProgress);

            // Update transition progress
            transitionProgress += Time.deltaTime * transitionSpeed;
            transitionProgress = Mathf.Clamp01(transitionProgress);
        }
        else
        {
            // Reset transition when switching back to regular mode
            transitioning = false;

            // Calculate camera position and rotation for regular follow mode
            float angle = transform.eulerAngles.y;
            Quaternion camRotation = Quaternion.Euler(xCamRotation, angle, 0f);
            Vector3 targetPosition = transform.position - (camRotation * camOffset);
            Quaternion targetRotation = camRotation;

            // Smoothly interpolate camera position and rotation
            Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, targetPosition, Time.deltaTime * followSpeed);
            Camera.main.transform.rotation = Quaternion.Lerp(Camera.main.transform.rotation, targetRotation, Time.deltaTime * followSpeed);
        }

    }

    public void SetSwingState(bool swing)
    {
        isSwingState = swing;
    }
}
