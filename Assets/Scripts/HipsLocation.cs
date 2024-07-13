using UnityEngine;

public class HipsLocation : MonoBehaviour
{

    public Vector3 endPosition = Vector3.zero;

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.name == "WaitingRoom" || collision.gameObject.name == "Terrain")
        {
            float diffMagnitude = (endPosition - transform.position).magnitude;
            //Debug.Log("HipsLocation: Updated end position: " + transform.position + "\n diffMagnitude: " + diffMagnitude);
            endPosition = transform.position;
        }
    }
}
