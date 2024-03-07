using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyWander : MonoBehaviour
{
    public float wanderRadius = 10f;
    public float wanderTimer = 5f;
    public float distanceThreshold = 10f;
    public float snapDistance = 4f;
    public float lungeSpeed = 12f;
    private float regularSpeed = 3.0f;
    public float returnDistance = 20f;

    private GameObject target;
    private NavMeshAgent agent;
    private float timer;
    private Vector3 startingPosition;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = true;
        timer = wanderTimer;
        SetNewRandomDestination();
        target = GameObject.FindWithTag("Player");
        startingPosition = transform.position;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
        float distanceToStartingPosition = Vector3.Distance(transform.position, startingPosition);

        if (distanceToStartingPosition > returnDistance)
        {
            ReturnToStartingPosition();
            return;
        }

        // Check other behaviors only if not returning to starting position
        if (distanceToTarget < snapDistance)
        {
            agent.speed = lungeSpeed;
            agent.SetDestination(target.transform.position);
        }
        else if (distanceToTarget < distanceThreshold)
        {
            agent.speed = regularSpeed;
            agent.SetDestination(target.transform.position);
        }
        else
        {
            agent.speed = regularSpeed;
            if (timer <= 0f)
            {
                SetNewRandomDestination();
                timer = wanderTimer;
            }
        }
    }

    void SetNewRandomDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;
        NavMeshHit hit;
        NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, 1);
        Vector3 finalPosition = hit.position;
        agent.SetDestination(finalPosition);
    }

    void ReturnToStartingPosition()
    {
        agent.speed = regularSpeed;
        agent.SetDestination(startingPosition);
    }
}
