using UnityEngine;
using Unity.Netcode;

public class StartCameraFollow : NetworkBehaviour
{
    public float followSpeed = 100f;
    public float xCamRotation = 15f;
    public Vector3 camOffset = new(0f, -2f, 5f);
    private bool isActive = true;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) isActive = false;
    }

    private void LateUpdate()
    {
        if (!isActive) return;

        float angle = transform.eulerAngles.y;
        Quaternion camRotation = Quaternion.Euler(xCamRotation, angle, 0f);

        // chat GPT helped me figure out how to calculate my camera position and add interpolation
        Vector3 camPosition = transform.position - (camRotation * camOffset);
        Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, camPosition, followSpeed * Time.deltaTime);
        Camera.main.transform.rotation = camRotation;

        //Debug.Log("Cam pos: " + Camera.main.transform.position + "rot: " + Camera.main.transform.rotation.eulerAngles);
        //Debug.Log("player pos: " + transform.position + "rot: " + transform.rotation.eulerAngles);

    }
}
