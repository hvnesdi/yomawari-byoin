using UnityEngine;
using UnityEngine.AI;

public enum NPCState { Idle, Walk, Talk }

/// <summary>
/// NPC basic behavior: Idle / Walk (patrol) / Talk (interact with player).
/// Appearance changes based on local player's hallucination band.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NPCManager : MonoBehaviour
{
    [Header("Patrol")]
    public Transform[] waypoints;
    public float walkSpeed = 1.2f;
    public float idleTime = 3f;

    [Header("Interaction")]
    public float talkRange = 2.5f;
    public string[] dialogueLines;

    [Header("Appearance by hallucination band")]
    public Renderer npcRenderer;
    public Material normalMat;
    public Material ghostMat;       // 30-60
    public Material ghostHighMat;   // 60+

    private NavMeshAgent agent;
    private Transform player;
    private NPCState state = NPCState.Idle;
    private int waypointIndex;
    private float idleTimer;
    private int dialogueIndex;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = walkSpeed;
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) player = go.transform;
        GoToNextWaypoint();
    }

    void Update()
    {
        UpdateAppearance();

        switch (state)
        {
            case NPCState.Idle: DoIdle(); break;
            case NPCState.Walk: DoWalk(); break;
            case NPCState.Talk: DoTalk(); break;
        }
    }

    void DoIdle()
    {
        agent.isStopped = true;
        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f)
        {
            state = NPCState.Walk;
            agent.isStopped = false;
            GoToNextWaypoint();
        }
        CheckTalkRange();
    }

    void DoWalk()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            state = NPCState.Idle;
            idleTimer = idleTime;
        }
        CheckTalkRange();
    }

    void DoTalk()
    {
        agent.isStopped = true;
        if (player != null)
            transform.LookAt(new Vector3(player.position.x, transform.position.y, player.position.z));
    }

    void CheckTalkRange()
    {
        if (player == null) return;
        if (Vector3.Distance(transform.position, player.position) <= talkRange)
            StartTalk();
    }

    public void StartTalk()
    {
        if (state == NPCState.Talk) return;
        state = NPCState.Talk;

        // Listening to NPC reduces hallucination
        HallucinationSystem.Instance?.ApplyModifier("local", HallucinationModifier.ListenedNPC);
        FlagManager.Instance?.SetFlag(FlagType.listenedToNPC, true);

        string line = dialogueLines is { Length: > 0 }
            ? dialogueLines[dialogueIndex % dialogueLines.Length]
            : "…";
        dialogueIndex++;
        UIManager.Instance?.ShowAnnouncement(line);
        Debug.Log($"[NPC] {name}: {line}");
    }

    public void EndTalk()
    {
        state = NPCState.Walk;
        agent.isStopped = false;
        GoToNextWaypoint();
    }

    void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        agent.SetDestination(waypoints[waypointIndex].position);
        waypointIndex = (waypointIndex + 1) % waypoints.Length;
    }

    void UpdateAppearance()
    {
        if (npcRenderer == null) return;
        var band = HallucinationSystem.Instance?.GetBand("local") ?? HallucinationBand.Low;
        npcRenderer.material = band switch
        {
            HallucinationBand.Low  => normalMat,
            HallucinationBand.Mid  => ghostMat,
            _                      => ghostHighMat,
        };
    }
}
