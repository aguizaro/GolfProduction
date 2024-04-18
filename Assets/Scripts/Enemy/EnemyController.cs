using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using Unity.Netcode;
using UnityEngine;
using Unity.VisualScripting;
using System;


public enum EnemyState
{
    GUARD,
    PATROL,
    CHASE,
    DEAD
}

[RequireComponent(typeof(NavMeshAgent))]
public class NetworkEnemyController : NetworkBehaviour
{
    private NetworkVariable<EnemyState> enemyState = new NetworkVariable<EnemyState>();
    private EnemyState _currentState;
    private NavMeshAgent agent;
    private Animator animator;
    private CharacterStats characterStats;

    public float sightRadius;
    public bool isGuard;
    private float speed;

    public bool isReturnToOrigin = false;
    public GameObject attackTarget;
    private NetworkVariable<float> lastAttackTime = new NetworkVariable<float>();
    public float lookAtTime;
    private float remainLookAtTime;
    private Quaternion guardRotation;
    public LayerMask targetLayer;

    public float patrolRange;
    private Vector3 wayPoint;
    public Vector3 guardPos;

    private bool isWalk;
    private bool isChase;
    private bool isFollow;

    void Awake()
    {

    }

    public override void OnNetworkSpawn()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        characterStats = GetComponent<CharacterStats>();
        speed = agent.speed;
        isGuard = true;
        guardPos = transform.position;
        guardRotation = transform.rotation;

        if (IsServer)
        {
            if (isGuard)
            {
                enemyState.Value = EnemyState.GUARD;
            }
            else
            {
                enemyState.Value = EnemyState.PATROL;
                GetNewWayPointServerRpc();
            }
        }

        enemyState.OnValueChanged += OnSpiderStateChange;


    }

    private void OnSpiderStateChange(EnemyState prev, EnemyState next)
    {
        _currentState = next;
        Debug.Log($"Spider state changed to {next}");
        Debug.Log($"is local client: {IsLocalPlayer}\nCurrent State:  {_currentState}, ");

    }

    public override void OnDestroy()
    {
        enemyState.OnValueChanged -= OnSpiderStateChange;
    }

    void Update()
    {
        if (IsServer)
        {
            SwitchState();
            lastAttackTime.Value -= Time.deltaTime;
            SwitchAnimation();
        }

        


    }

    void SwitchAnimation()
    {
        animator.SetBool("Walk", isWalk);
        animator.SetBool("Chase", isChase);
        animator.SetBool("Follow", isFollow);
    }

    void SwitchState()
    {
        if (FoundPlayer())
        {
            enemyState.Value = EnemyState.CHASE;
        }

        switch (enemyState.Value)
        {
            case EnemyState.GUARD:
                GuardBehavior();
                break;
            case EnemyState.PATROL:
                PatrolBehavior();
                break;
            case EnemyState.CHASE:
                ChaseBehavior();
                break;
            case EnemyState.DEAD:
                // Implement Death behavior
                break;
        }
    }

    [ClientRpc]
    void ClientStateChangeClientRpc()
    {

    }

    [ServerRpc]
    void GetNewWayPointServerRpc()
    {
        remainLookAtTime = lookAtTime;

        float randomX = UnityEngine.Random.Range(-patrolRange, patrolRange);
        float randomZ = UnityEngine.Random.Range(-patrolRange, patrolRange);

        Vector3 randomPoint = new Vector3(guardPos.x + randomX, transform.position.y, guardPos.z + randomZ);

        NavMeshHit hit;
        wayPoint = NavMesh.SamplePosition(randomPoint, out hit, patrolRange, 1) ? hit.position : transform.position;
    }

    [ServerRpc]
    void AttackServerRpc()
    {
        if (!IsServer || attackTarget == null) return;

        transform.LookAt(attackTarget.transform);

        if (TargetInAttackRange())
        {
            animator.SetTrigger("Attack");
        }
    }

    bool TargetInAttackRange()
    {
        if (attackTarget != null)
            return Vector3.Distance(attackTarget.transform.position, transform.position) <= characterStats.attackData.attackRange;
        else
            return false;
    }

    bool FoundPlayer()
    {
        if (isReturnToOrigin)
        {
            return false;
        }

        var colliders = Physics.OverlapSphere(transform.position, sightRadius, targetLayer);

        foreach (var target in colliders)
        {
            if (target.gameObject == attackTarget)
            {
                return true;
            }
        }

        foreach (var target in colliders)
        {
            if (target.CompareTag("Player"))
            {
                attackTarget = target.gameObject;
                return true;
            }
        }

        attackTarget = null;
        return false;
    }

    void GuardBehavior()
    {
        isChase = false;
        if (transform.position != guardPos)
        {
            isWalk = true;

            
            agent.isStopped = false;
            agent.destination = guardPos;

            Debug.LogWarning($"GuardPos: {guardPos}, CurrentPos: {transform.position}, Distance.x: {guardPos.x - transform.position.x}, Distance.z: {guardPos.z - transform.position.z}");
        
           if (Math.Abs(guardPos.x - transform.position.x) < agent.stoppingDistance && Math.Abs(guardPos.z - transform.position.z) < agent.stoppingDistance)
            {
                isWalk = false;
                transform.rotation = Quaternion.Lerp(transform.rotation, guardRotation, 0.01f);
                isReturnToOrigin = false;
            }
            Debug.Log("Iswalk: "+ isWalk);
        }
    }

    void PatrolBehavior()
    {
        isChase = false;
        agent.speed = speed * 0.5f;

        if (Vector3.Distance(transform.position, wayPoint) <= agent.stoppingDistance)
        {
            isWalk = false;
            isReturnToOrigin = false;
            if (remainLookAtTime > 0)
                remainLookAtTime -= Time.deltaTime;
            else
                GetNewWayPointServerRpc();
        }
        else
        {
            isWalk = true;
            agent.SetDestination(wayPoint);
        }
    }

    void ChaseBehavior()
    {
        isWalk = false;
        isChase = true;

        agent.speed = speed;
        if (!FoundPlayer())
        {
            isFollow = false;
            if (remainLookAtTime > 0)
            {
                agent.SetDestination(transform.position);
                remainLookAtTime -= Time.deltaTime;
            }
            else
            {
                isReturnToOrigin = true;
                enemyState.Value = isGuard ? EnemyState.GUARD : EnemyState.PATROL;
            }
        }
        else
        {
            isFollow = true;
            agent.isStopped = false;
            agent.SetDestination(attackTarget.transform.position);
        }


        if (TargetInAttackRange())
        {
            isFollow = false;
            agent.isStopped = true;
            if (lastAttackTime.Value <= 0)
            {
                lastAttackTime.Value = characterStats.attackData.coolDown;
                AttackServerRpc();
            }
        }

    }

    // Animation Event
    [ServerRpc(RequireOwnership = false)]
    public void HitServerRpc()
    {
        if (!IsServer) return;

        float attackRange = characterStats.attackData.attackRange;
        float attackAngle = 45f;
        int numberOfRays = 10;
        Vector3 origin = transform.position + new Vector3(0, 0.5f, 0);
        float startAngle = -attackAngle;
        float angleIncrement = attackAngle * 2 / numberOfRays;
        HashSet<GameObject> hitTargets = new HashSet<GameObject>();

        for (int i = 0; i <= numberOfRays; i++)
        {
            float currentAngle = startAngle + (angleIncrement * i);
            Vector3 direction = Quaternion.Euler(0, currentAngle, 0) * transform.forward;
            Ray ray = new Ray(origin, direction);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, attackRange, targetLayer))
            {
                Debug.DrawLine(ray.origin, hit.point, Color.red, 2f);
                if (!hitTargets.Contains(hit.collider.gameObject))
                {
                    hitTargets.Add(hit.collider.gameObject);
                    NetworkObject targetNetworkObject = hit.collider.gameObject.GetComponent<NetworkObject>();
                    if (targetNetworkObject != null)
                    {
                        ApplyDamageServerRpc(targetNetworkObject.NetworkObjectId);
                    }
                }

            }
            else
            {
                Debug.DrawRay(ray.origin, ray.direction * attackRange, Color.yellow, 2f);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void ApplyDamageServerRpc(ulong targetNetworkObjectId)
    {
        if (!IsServer) return;

        NetworkObject networkObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetNetworkObjectId];
        if (networkObject != null)
        {
            GameObject target = networkObject.gameObject;
            Debug.Log($"Damage applied to {target.name}");
            // TODO: Apply damage to target
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, sightRadius);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(guardPos, 0.1f);
    }
    
    
}
