using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class BallCollision : NetworkBehaviour
{
    Rigidbody _rb;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // Start is called before the first frame update
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == 3)
        {
            if (IsOwner)
            {
                Debug.Log(_rb.velocity);
                AudioManager.instance.PlayOneShotForAllClients(FMODEvents.instance.playerGolfSwing, this.transform.position, IsOwner);
            }
        }
    }
}
