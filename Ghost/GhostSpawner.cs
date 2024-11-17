using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class GhostSpawner : NetworkBehaviour
{
    [SerializeField] private GhostManager ghostManager;
    [SerializeField] private GameObject[] ghosts;
    [SerializeField] private float ghostSpawnChance;
    [SerializeField] private float[] typeGhostChance;
    [SerializeField] private int[] ghostFavor;

    [SyncVar] public GameObject currentGhost;

    /// <summary>
    /// Picks a ghost to spawn based on spawn chances and ghost types.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void PickGhost()
    {
        if (CalculateSpawnChance() <= ghostSpawnChance)
        {
            if (typeGhostChance[ghostFavor[0]] > CalculateSpawnChance())
            {
                SpawnGhost(ghostFavor[0]);
            }
            else if (typeGhostChance[ghostFavor[1]] > CalculateSpawnChance())
            {
                SpawnGhost(ghostFavor[1]);
            }
            else if (typeGhostChance[ghostFavor[2]] > CalculateSpawnChance())
            {
                SpawnGhost(ghostFavor[2]);
            }
            else
            {
                SpawnGhost(0);
            }
        }
    }

    /// <summary>
    /// Spawns a ghost at the spawner's position based on the specified index.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SpawnGhost(int index)
    {
        if (currentGhost == null)
        {
            currentGhost = Instantiate(ghosts[index], transform.position, transform.rotation);
            Settrans();
            currentGhost.transform.position = this.transform.position;
            Spawn(currentGhost);
            ghostManager.ChangeGhostAlive(1);
            ghostManager.ChangeGhostPoint(currentGhost.GetComponent<GhostMovement>().ghostData.points);
        }
    }

    /// <summary>
    /// Syncs the ghost's position to the spawner.
    /// </summary>
    [ObserversRpc]
    public void Settrans()
    {
        if (currentGhost != null)
            currentGhost.transform.position = this.transform.position;
    }

    /// <summary>
    /// Calculates a random spawn chance.
    /// </summary>
    private float CalculateSpawnChance()
    {
        return Random.Range(1, 100);
    }
}