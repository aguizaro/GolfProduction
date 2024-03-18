using UnityEngine;
using Unity.Netcode;

public class StartCameraFollow : NetworkBehaviour
{
    public float followSpeed = 100f;
    public float xCamRotation = 15f;
    public Vector3 camOffset = new(0f, -2f, 5f);
    private bool isActive = true;
    private bool isSwingState = false;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) isActive = false;
    }

    private void LateUpdate()
    {
        if (!isActive) return;

        float angle = transform.eulerAngles.y;
        Quaternion camRotation = Quaternion.Euler(xCamRotation, angle, 0f);

        Vector3 targetPosition;
        Quaternion targetRotation;

        if (isSwingState)
        {
            // Adjust camera position and rotation for swing state mode
            targetPosition = transform.position - (transform.right * camOffset.x) + (transform.up * camOffset.y) + (transform.forward * camOffset.z);
            targetRotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position, Vector3.up);
        }
        else
        {
            // Calculate camera position and rotation for regular follow mode
            targetPosition = transform.position - (camRotation * camOffset);
            targetRotation = camRotation;
            
            // Interpolate camera position and rotation
            Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, targetPosition, followSpeed * Time.deltaTime);
            Camera.main.transform.rotation = Quaternion.Lerp(Camera.main.transform.rotation, targetRotation, followSpeed * Time.deltaTime);
        }

        

        /*  Temp commenting out old code
        // chat GPT helped me figure out how to calculate my camera position and add interpolation
        Vector3 camPosition = transform.position - (camRotation * camOffset);
        Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, camPosition, followSpeed * Time.deltaTime);
        Camera.main.transform.rotation = camRotation;

        //Debug.Log("Cam pos: " + Camera.main.transform.position + "rot: " + Camera.main.transform.rotation.eulerAngles);
        //Debug.Log("player pos: " + transform.position + "rot: " + transform.rotation.eulerAngles);
        */

    }

    public void SetSwingState(bool swing)
    {
        isSwingState = swing;
    }
}
