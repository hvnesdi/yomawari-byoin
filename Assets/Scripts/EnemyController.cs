using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    [Header("Patrol")]
    public Transform[] waypoints;
    public float patrolSpeed = 2f;

    [Header("Detection")]
    public float detectionRange = 10f;
    public float detectionAngle = 60f;
    public float catchDistance = 1.5f;

    [Header("Chase")]
    public float chaseSpeed = 4f;

    [Header("Capture")]
    public Transform playerSpawnPoint;

    private NavMeshAgent agent;
    private Transform player;
    private int waypointIndex = 0;
    private enum State { Patrol, Chase }
    private State state = State.Patrol;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        GoToNextWaypoint();
    }

    void Update()
    {
        switch (state)
        {
            case State.Patrol: Patrol(); break;
            case State.Chase:  Chase();  break;
        }
    }

    void Patrol()
    {
        agent.speed = patrolSpeed;
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
            GoToNextWaypoint();

        if (CanSeePlayer()) state = State.Chase;
    }

    void Chase()
    {
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position);

        if (Vector3.Distance(transform.position, player.position) <= catchDistance)
            CapturePlayer();
        else if (!CanSeePlayer())
            state = State.Patrol;
    }

    bool CanSeePlayer()
    {
        Vector3 dir = player.position - transform.position;
        if (dir.magnitude > detectionRange) return false;
        if (Vector3.Angle(transform.forward, dir) > detectionAngle * 0.5f) return false;
        return !Physics.Raycast(transform.position + Vector3.up, dir.normalized, dir.magnitude, LayerMask.GetMask("Default"));
    }

    void GoToNextWaypoint()
    {
        if (waypoints.Length == 0) return;
        agent.SetDestination(waypoints[waypointIndex].position);
        waypointIndex = (waypointIndex + 1) % waypoints.Length;
    }

    void CapturePlayer()
    {
        if (playerSpawnPoint != null)
            player.position = playerSpawnPoint.position;
        state = State.Patrol;
        GoToNextWaypoint();
    }
}
