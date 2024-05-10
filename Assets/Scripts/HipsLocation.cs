using UnityEngine;

public class HipsLocation : MonoBehaviour
{

    public Vector3 endPosition = Vector3.zero;

    void OnCollisionStay(Collision collision)
    {
        //make sure we are checkign if collision with floor (tagged "Ground" - not yet implemented)

        if (/*collision.gameObject.CompareTag("Ground") &&*/ true)
        {
            float diffMagnitude = (endPosition - transform.position).magnitude;
            //Debug.Log("HipsLocation: Updated end position: " + transform.position + "\n diffMagnitude: " + diffMagnitude);
            endPosition = transform.position;
            if (diffMagnitude > 0.1f)
            {
                //Debug.Log("HipsLocation: Player is moving");
            }
        }
    }
}
