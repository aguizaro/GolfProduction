using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class HoleFlagPoleManager : NetworkBehaviour
{
    private Collider holeTrigger;
    private PlayerNetworkData _playerNetworkData;
    private bool isActive = false;

    public List<Vector3> holeStartPositions;
    private UIManager _UIManager;

    private List<ulong> _playerIDs = new List<ulong>();

    private void Start()
    {
        holeTrigger = GetComponent<Collider>();
        _UIManager = GameObject.Find("Canvas").GetComponent<UIManager>();
    }

    public void Activate()
    {
        _playerNetworkData = GameObject.FindWithTag("StateManager").GetComponent<PlayerNetworkData>();
        isActive = true;

        //Debug.Log("HoleFlagPoleManager activated for " + OwnerClientId + " isOwner: " + IsOwner);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return; //prevent updates until player is fully activated

        if (other.CompareTag("Ball"))
        {
            if (!_playerIDs.Contains(other.gameObject.GetComponent<NetworkObject>().OwnerClientId))
            {
                PlayerData oldData = _playerNetworkData.GetPlayerState();
                oldData.completedHoles++;

                ulong playerID = other.gameObject.GetComponent<NetworkObject>().OwnerClientId;
                _playerNetworkData.UpdateCompletedHoleCount(oldData.completedHoles, playerID);
                _UIManager.UpdateHoleCountText(oldData.completedHoles + 1);
                _playerIDs.Add(playerID);

                if (oldData.completedHoles == holeStartPositions.Count)
                {
                    Debug.Log("Game Over, you made it!");
                    other.gameObject.SetActive(false);
                }
                else
                {
                    //stop the ball
                    other.GetComponent<Rigidbody>().velocity = Vector3.zero;
                    other.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
                    //move the ball to the next start pos
                    other.transform.position = GetNextHoleStartPosition(oldData.completedHoles);
                    Debug.Log("Hole " + (oldData.completedHoles - 1) + " completed!\nMoving to next position " + other.transform.position);
                }
            }
        }
    }

    private Vector3 GetNextHoleStartPosition(int holeIndex)
    {
        return holeStartPositions[holeIndex] + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));

    }
}
