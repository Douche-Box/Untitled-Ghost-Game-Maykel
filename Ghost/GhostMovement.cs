using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class GhostMovement : NetworkBehaviour
{
    NavMeshAgent agent;

    [SerializeField] Animator animator;
    public GhostData ghostData;

    [Header("Walk Options")]
    [Tooltip("The layer the agent can walk on")]
    [SerializeField] LayerMask walkableLayer;
    [Tooltip("Max distance the agent can walk")]
    [SerializeField] float walkRadius;
    [SyncVar] float speed;
    [Tooltip("The time an agent will wait before going to its next point")]
    [SerializeField] float waitTime;

    [Header("Ghost Options")]
    public float timeToSuck;
    float rechargeRate;
    public float suckieTimer;
    int points;
    [SerializeField] float ghostStoppingDistance;
    float timer;
    GhostManager ghostManager;

    public StofZuiger[] stofzuigers;
    [SyncVar] public bool isDead;
    public bool hitness;
    public bool[] hits;

    /// <summary>
    /// Initializes the ghost movement settings at the start.
    /// </summary>
    void Start()
    {
        if (!IsHost)
        {
            GetComponent<NavMeshAgent>().enabled = false;
        }

        agent = GetComponent<NavMeshAgent>();
        ghostManager = FindObjectOfType<GhostManager>();
        points = ghostData.points;
        speed = ghostData.speed;
        walkRadius = ghostData.walkradius;
        timeToSuck = ghostData.suckTime;
        rechargeRate = ghostData.rechargeRate;

        agent.autoBraking = false;
        agent.stoppingDistance = ghostStoppingDistance;
        suckieTimer = timeToSuck;
        PatrolToNextPoint();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        stofzuigers = FindObjectsOfType<StofZuiger>();
    }

    bool someoneSucks;

    /// <summary>
    /// Handles ghost behavior, including patrol and checking for suction interactions.
    /// </summary>
    void Update()
    {
        if (isDead)
            return;

        PatrolToNextPoint();

        for (int i = 0; i < stofzuigers.Length; i++)
        {
            hits[i] = stofzuigers[i].sucking;
            for (int x = 0; x < hits.Length; x++)
            {
                if (hits[x])
                {
                    SetSpeed(0);
                    someoneSucks = true;
                }
                if (x == hits.Length && !someoneSucks)
                {
                    SetSpeed(ghostData.speed);
                    someoneSucks = false;
                }
            }
        }

        if (hitness)
        {
            GetSucked();
            BoolAnim("IsSucked", true);
        }
        else
        {
            SetSpeed(ghostData.speed);
            BoolAnim("IsSucked", false);
        }

        ResetSuckie();
    }

    /// <summary>
    /// Sets the ghost's movement speed.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SetSpeed(float speeds)
    {
        speed = speeds;
    }

    [SerializeField] bool stop;

    /// <summary>
    /// Handles ghost patrolling logic by setting a random destination within the walk radius.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    void PatrolToNextPoint()
    {
        if (stop)
            return;

        agent.speed = speed;
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            BoolAnim("IsMoving", false);
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                BoolAnim("IsMoving", true);
                timer = waitTime;

                Vector3 randomDirection = Random.insideUnitSphere * walkRadius;
                randomDirection += transform.position;
                NavMeshHit hit;
                NavMesh.SamplePosition(randomDirection, out hit, walkRadius, 1);
                Vector3 finalPosition = hit.position;

                agent.destination = finalPosition;
            }
        }
    }

    /// <summary>
    /// Sets an animation parameter on the ghost's animator.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    void BoolAnim(string name, bool b)
    {
        animator.SetBool(name, b);
        AnimationObserver(name, b);
    }

    /// <summary>
    /// Observes and syncs the animation state across all clients.
    /// </summary>
    [ObserversRpc]
    public void AnimationObserver(string name, bool b)
    {
        if (IsHost)
            return;
        animator.SetBool(name, b);
    }

    /// <summary>
    /// Returns the time remaining for the ghost to be sucked.
    /// </summary>
    public float timeLeft()
    {
        return suckieTimer;
    }

    /// <summary>
    /// Sets the hit status of the ghost when it is being sucked.
    /// </summary>
    public void isHit(bool hit)
    {
        hitness = hit;
    }

    /// <summary>
    /// Returns the point value of the ghost.
    /// </summary>
    public int Points()
    {
        return points;
    }

    /// <summary>
    /// Handles the sucking behavior and decreases the timer.
    /// </summary>
    void GetSucked()
    {
        suckieTimer -= Time.deltaTime;
    }

    /// <summary>
    /// Kills the ghost, decreases points, and despawns it.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void Die()
    {
        isDead = true;
        ghostManager.ChangeGhostPoint(-points);
        ghostManager.ChangeGhostAlive(-1);
        this.NetworkObject.Despawn();
    }

    /// <summary>
    /// Resets the sucking timer based on the recharge rate.
    /// </summary>
    void ResetSuckie()
    {
        if (!hitness && suckieTimer < timeToSuck)
        {
            suckieTimer += rechargeRate * Time.deltaTime;
        }
        else if (!hitness && suckieTimer > timeToSuck)
        {
            suckieTimer = timeToSuck;
        }
    }

    /// <summary>
    /// Returns the ghost's point value.
    /// </summary>
    public int GetGhostValue()
    {
        return points;
    }
}