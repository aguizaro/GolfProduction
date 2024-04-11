using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


public enum EnemyState
{
    GUARD,
    PATROL,
    CHASE,
    DEAD
}


[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    public EnemyState enemyState;
    private NavMeshAgent agent;
    private Animator animator;
    private CharacterStats characterStats;

    [Header("Basic Settings")]
    public float sightRadius;
    bool isGuard;
    private float speed;
    private GameObject attackTarget;
    private float lastAttackTime;
    public float lookAtTime;
    float remainLookAtTime;
    private Quaternion guardRotation;
    public LayerMask targetLayer;

    [Header("Patrol State")]
    public float patrolRange;
    public Vector3 wayPoint;
    private Vector3 guardPos;


    bool isWalk;
    bool isChase;
    bool isFollow;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        characterStats = GetComponent<CharacterStats>();
        speed = agent.speed;
        guardPos = transform.position;
        remainLookAtTime = lookAtTime;
        guardRotation = transform.rotation;
        isGuard = enemyState == EnemyState.GUARD;
    }

    void Start()
    {
        if (isGuard)
        {
            enemyState = EnemyState.GUARD;
        }
        else
        {
            enemyState = EnemyState.PATROL;
            GetNewWayPoint();
        }
    }

    void Update()
    {
        SwitchState();
        SwitchAnimation();
        lastAttackTime -= Time.deltaTime;
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
            enemyState = EnemyState.CHASE;
        }

        switch (enemyState)
        {
            case EnemyState.GUARD:
                isChase = false;

                if (transform.position != guardPos)
                {
                    isWalk = true;
                    agent.isStopped = false;
                    agent.destination = guardPos;

                    if (Vector3.SqrMagnitude(guardPos - transform.position) <= agent.stoppingDistance)
                    {
                        isWalk = false;
                        transform.rotation = Quaternion.Lerp(transform.rotation, guardRotation, 0.01f);
                    }
                }
                break;
            case EnemyState.PATROL:
                isChase = false;
                agent.speed = speed * 0.5f;

                //Determine whether the waypoint has been reached
                if (Vector3.Distance(transform.position, wayPoint) <= agent.stoppingDistance)
                {
                    isWalk = false;
                    if (remainLookAtTime > 0)
                        remainLookAtTime -= Time.deltaTime;
                    else
                        GetNewWayPoint();
                }
                else
                {
                    isWalk = true;
                    agent.SetDestination(wayPoint);
                }

                break;
            case EnemyState.CHASE:
                isWalk = false;
                isChase = true;

                agent.speed = speed;
                Debug.Log(animator.speed);

                if (!FoundPlayer())
                {
                    isFollow = false;
                    if (remainLookAtTime > 0)
                    {
                        agent.SetDestination(transform.position);
                        remainLookAtTime -= Time.deltaTime;
                    }
                    else if (isGuard)
                    {
                        enemyState = EnemyState.GUARD;
                    }
                    else
                    {
                        enemyState = EnemyState.PATROL;
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

                    if (lastAttackTime <= 0)
                    {
                        lastAttackTime = characterStats.attackData.coolDown;
                        characterStats.isCritical = Random.value < characterStats.attackData.criricalChance;
                        Attack();
                    }
                }


                break;
            case EnemyState.DEAD:
                break;
            default:
                break;
        }
    }

    void Attack()
    {
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
        var colliders = Physics.OverlapSphere(transform.position, sightRadius);

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

    void GetNewWayPoint()
    {
        remainLookAtTime = lookAtTime;

        float randomX = Random.Range(-patrolRange, patrolRange);
        float randomZ = Random.Range(-patrolRange, patrolRange);

        Vector3 randomPoint = new Vector3(guardPos.x + randomX, transform.position.y, guardPos.z + randomZ);

        NavMeshHit hit;
        wayPoint = NavMesh.SamplePosition(randomPoint, out hit, patrolRange, 1) ? hit.position : transform.position;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, sightRadius);
    }

    //Animation Event
    void Hit()
    {
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
                    Debug.Log("Hit Plyer!");
                    Debug.Log(hit.collider.gameObject.name);
                    //add hit logic here
                }
            }
            else
            {
                Debug.DrawRay(ray.origin, ray.direction * attackRange, Color.yellow, 2f);
            }
        }

    }
}
