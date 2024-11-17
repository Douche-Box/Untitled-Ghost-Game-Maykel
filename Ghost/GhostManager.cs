using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object.Synchronizing;
using FishNet.Object;

public class GhostManager : NetworkBehaviour
{
    [Header("GhostSpawners settings")]
    [SerializeField] private GhostSpawner[] ghostSpawners;
    [SerializeField] private int ghostsAlive;
    [SerializeField] private int maxGhosts;
    [SerializeField] private int globalGhostPoints;

    [SerializeField] private List<GhostSpawner> availableSpawners = new();
    [SerializeField] private float spawnDelay = 1f;
    [SyncVar][SerializeField] private bool isStarted;

    private bool isSpawning;

    /// <summary>
    /// Starts the spawners if the host and not already started.
    /// </summary>
    public void Start()
    {
        if (IsHost && !isStarted)
        {
            StartSpawners();
        }
    }

    /// <summary>
    /// Server RPC to check and add available spawners for spawning ghosts.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void CheckAvailable()
    {
        if (ghostsAlive >= maxGhosts)
        {
            return;
        }

        for (int i = 0; i < ghostSpawners.Length; i++)
        {
            if (ghostSpawners[i].currentGhost == null && !availableSpawners.Contains(ghostSpawners[i]))
            {
                availableSpawners.Add(ghostSpawners[i]);
            }
        }

        isSpawning = true;
        StartCoroutine(BigTimer());
    }

    /// <summary>
    /// Server RPC to start the spawners and begin spawning ghosts.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    void StartSpawners()
    {
        isStarted = true;
        CheckAvailable();
    }

    /// <summary>
    /// Picks a random spawner from available spawners and spawns a ghost.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    void PickSpawner(int spawnerCount)
    {
        if (ghostsAlive >= maxGhosts)
        {
            return;
        }

        int i = Random.Range(0, spawnerCount);
        availableSpawners[i].PickGhost();
        availableSpawners.Remove(availableSpawners[i]);
        CheckAvailable();
    }

    /// <summary>
    /// Update method to check spawners and spawn ghosts if necessary.
    /// </summary>
    private void Update()
    {
        CheckSpawners();

        if (ghostsAlive == maxGhosts)
        {
            isSpawning = false;
        }

        if (!isSpawning && ghostsAlive < maxGhosts)
        {
            StartCoroutine(CheckTime());
            isSpawning = true;
        }
    }

    /// <summary>
    /// Coroutine to wait for a spawn delay before checking available spawners.
    /// </summary>
    IEnumerator CheckTime()
    {
        yield return new WaitForSeconds(spawnDelay);
        CheckAvailable();
    }

    /// <summary>
    /// Server RPC to check and remove spawners that are no longer available.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    void CheckSpawners()
    {
        for (int i = 0; i < availableSpawners.Count; i++)
        {
            if (availableSpawners[i].currentGhost != null)
            {
                availableSpawners.Remove(availableSpawners[i]);
            }
        }
    }

    /// <summary>
    /// Coroutine to pick a random spawner after a spawn delay.
    /// </summary>
    IEnumerator BigTimer()
    {
        yield return new WaitForSeconds(spawnDelay);
        PickSpawner(availableSpawners.Count - 1);
    }

    /// <summary>
    /// Changes the number of ghosts alive by the given amount.
    /// </summary>
    public void ChangeGhostAlive(int amount)
    {
        ghostsAlive += amount;
    }

    /// <summary>
    /// Changes the total points for ghosts by the given amount.
    /// </summary>
    public void ChangeGhostPoint(int amount)
    {
        globalGhostPoints += amount;
    }
}