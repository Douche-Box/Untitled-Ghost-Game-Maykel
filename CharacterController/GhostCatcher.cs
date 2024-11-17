using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class GhostCatcher : NetworkBehaviour
{
    [Header("Controls")]
    [SerializeField] KeyCode suckKey;
    [SerializeField] KeyCode shootKey;
    [SerializeField] LayerMask mask;
    [SerializeField] PlayerData playerData;
    [SerializeField] MovementAdvanced movement;

    [Header("Shooting")]
    [SerializeField] Transform shootPos;
    [SerializeField] float suckRange;
    [SerializeField] float fireSpeed;
    [SerializeField] GameObject ghostToShoot;
    [SerializeField] GameObject playerBullet;
    RaycastHit hit;
    float timeSinceLastShoot;

    public GameObject tornado;
    public Animator animator;
    GameManager gameManager;

    [SerializeField] int maxGhostPoints = 3;
    [SyncVar][SerializeField] int ghostPoints;
    public int GhostPoints => ghostPoints;
    bool maxGhost;

    [Tooltip("Set the fire rate to the amount of seconds you want to wait between shots")]
    [SerializeField] float fireRate;
    [SerializeField] float fireTime;

    [SerializeField] List<GameObject> targets = new();
    string GhostTag = "Ghost";

    [SyncVar] public bool sucking;

    [SerializeField] GameObject beamparticlePrefab;
    [SerializeField] Transform beamstart;

    int oldTargetCount;

    // Called when the client starts
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!base.IsOwner)
        {
            GetComponent<MeshCollider>().enabled = false;
            this.enabled = false;
        }
    }

    // Called once per frame to handle ghost sucking, shooting, and other interactions
    void Update()
    {
        if (animator == null)
        {
            return;
        }
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        // Remove dead ghosts from the target list
        for (int z = 0; z < targets.Count; z++)
        {
            if (targets[z].transform.GetComponent<GhostMovement>().isDead)
            {
                targets.Remove(targets[z]);
            }
        }

        // Check if ghost points exceed the maximum limit
        if (ghostPoints > maxGhostPoints)
        {
            SetGhostPoints(maxGhostPoints);
            if (!maxGhost)
            {
                if (ghostPoints == maxGhostPoints)
                {
                    maxGhost = true;
                }
            }
        }
        else if (ghostPoints == maxGhostPoints)
        {
            maxGhost = true;
        }
        else if (ghostPoints < maxGhostPoints)
        {
            maxGhost = false;
        }

        // Handle sucking ghosts if the suck key is held
        if (Input.GetKey(suckKey))
        {
            if (maxGhost || targets == null)
            {
                return;
            }
            Suck();
            SuckAnimation(true);
            SetTornadoObserver(true);
        }
        else
        {
            for (int x = 0; x < targets.Count; x++)
            {
                targets[x].transform.GetComponent<GhostMovement>().isHit(false);
            }
            StopSuck();
            SetTornadoObserver(false);
        }

        // Handle shooting ghosts if the shoot key is pressed and cooldown allows
        if (fireTime > 0)
        {
            fireTime -= Time.deltaTime;
        }
        if (Input.GetKeyDown(shootKey) && ghostPoints > 0)
        {
            if (fireTime <= 0)
            {
                fireTime = fireRate;
                ShootAnimation();
                Shoot();
            }
        }
    }

    /// <summary>
    /// Activates or deactivates the tornado effect for all observers
    /// </summary>
    /// <param name="value"></param>
    [ObserversRpc]
    void SetTornadoObserver(bool value)
    {
        tornado.SetActive(value);
    }

    /// <summary>
    /// Sucks in ghosts within the defined range and interacts with them based on their state
    /// </summary>
    public void Suck()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets.Count >= 1)
            {
                Debug.DrawRay(shootPos.position, targets[i].transform.position - shootPos.transform.position);
                if (Physics.Raycast(shootPos.position, targets[i].transform.position - shootPos.position, out hit, suckRange, mask))
                {
                    if (hit.transform.tag == GhostTag && !hit.transform.GetComponent<GhostMovement>().isDead)
                    {
                        if (targets[i].transform.GetComponent<GhostMovement>().timeLeft() <= 0)
                        {
                            AddPoints(targets[i].transform.GetComponent<GhostMovement>().Points());
                            targets[i].transform.GetComponent<GhostMovement>().Die();
                            targets.Remove(targets[i]);
                        }
                        else if (targets[i].transform.GetComponent<GhostMovement>().timeLeft() > 0)
                        {
                            targets[i].transform.GetComponent<GhostMovement>().isHit(true);
                            SetSucking(true);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Adds points to the ghost catcher, called on the server
    /// </summary>
    /// <param name="points"></param>
    [ServerRpc(RequireOwnership = true)]
    void AddPoints(int points)
    {
        ghostPoints += points;
    }

    /// <summary>
    /// Sets the number of ghost points, called on the server
    /// </summary>
    /// <param name="amount"></param>
    [ServerRpc(RequireOwnership = true)]
    void SetGhostPoints(int amount)
    {
        ghostPoints = amount;
    }

    /// <summary>
    /// Sets the sucking state on the server
    /// </summary>
    /// <param name="state"></param>
    [ServerRpc(RequireOwnership = false)]
    public void SetSucking(bool state)
    {
        sucking = state;
    }

    /// <summary>
    /// Stops the sucking effect and updates observers
    /// </summary>
    [ServerRpc(RequireOwnership = true)]
    public void StopSuck()
    {
        SuckAnimation(false);
        SetSucking(false);
        StopSuckObserver();
    }

    /// <summary>
    /// Stops the sucking animation for observers
    /// </summary>
    [ObserversRpc]
    public void StopSuckObserver()
    {
        if (IsHost)
            return;
        SuckAnimation(false);
        SetSucking(false);
    }

    /// <summary>
    /// Steals points from another player (enemy) and adds them to this player
    /// </summary>
    /// <param name="points"></param>
    /// <param name="enemy"></param>
    [ServerRpc(RequireOwnership = false)]
    public void StealPoints(int points, StofZuiger enemy)
    {
        int pointsCanBeGained = maxGhostPoints - ghostPoints;
        for (int i = 0; i < pointsCanBeGained; i++)
        {
            if (points > 0)
            {
                ghostPoints++;
                enemy.LosePoints(1);
            }
            else
            {
                print("Enemy has no points!");
            }
        }
    }

    /// <summary>
    /// Reduces the ghost points by the specified amount
    /// </summary>
    /// <param name="points"></param>
    [ServerRpc(RequireOwnership = false)]
    public void LosePoints(int points)
    {
        ghostPoints -= points;
    }

    /// <summary>
    /// Called when a ghost exits the trigger area
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(GhostTag))
        {
            other.transform.GetComponent<GhostMovement>().isHit(false);
            SetSucking(false);
            targets.Remove(other.gameObject);
        }
    }

    /// <summary>
    /// Adds ghosts to the target list when they enter the trigger area
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(GhostTag))
        {
            if (targets.Contains(other.gameObject))
            {
                return;
            }
            targets.Add(other.gameObject);
        }
    }

    /// <summary>
    /// Shoots a ghost object from the player
    /// </summary>
    [ServerRpc(RequireOwnership = true)]
    public void Shoot()
    {
        ReleaseGhost(this.NetworkObject, fireSpeed);
        ghostPoints -= 1;
    }

    /// <summary>
    /// Releases a ghost as a projectile in the game world
    /// </summary>
    /// <param name="netObj"></param>
    /// <param name="speed"></param>
    [ServerRpc(RequireOwnership = false)]
    public void ReleaseGhost(NetworkObject netObj, float speed)
    {
        GameObject spawnedBullet = Instantiate(playerBullet, shootPos.position, shootPos.rotation);
        Spawn(spawnedBullet, netObj.Owner);
        spawnedBullet.GetComponent<Bullet>().ownerofObject = this.NetworkObject;

        spawnedBullet.GetComponent<Rigidbody>().velocity = shootPos.forward * speed;
        UpdatePos(spawnedBullet, speed);
    }

    /// <summary>
    /// Updates the position of the ghost projectile for all observers
    /// </summary>
    /// <param name="spawnedBullet"></param>
    /// <param name="speed"></param>
    [ObserversRpc]
    void UpdatePos(GameObject spawnedBullet, float speed)
    {
        spawnedBullet.GetComponent<Rigidbody>().velocity = shootPos.forward * speed;
    }

    /// <summary>
    /// Stores the points in the game and updates the score
    /// </summary>
    public void StorePoints()
    {
        playerData.GainPoints(ghostPoints);
        gameManager.AddPoints(playerData.teamID, ghostPoints);
        ghostPoints = 0;
    }

    #region Animations

    /// <summary>
    /// Triggers the shooting animation for the player
    /// </summary>
    [ServerRpc(RequireOwnership = true)]
    public void ShootAnimation()
    {
        animator.SetTrigger("IsShooting");
        ShootAnimationObserver();
    }

    /// <summary>
    /// Triggers the shooting animation for observers
    /// </summary>
    [ObserversRpc]
    public void ShootAnimationObserver()
    {
        if (IsHost)
            return;
        animator.SetTrigger("IsShooting");
    }

    /// <summary>
    /// Triggers the sucking animation for the player
    /// </summary>
    /// <param name="suckstate"></param>
    [ServerRpc(RequireOwnership = true)]
    public void SuckAnimation(bool suckstate)
    {
        animator.SetBool("IsSucking", suckstate);
        SuckAnimationObserver(suckstate);
    }

    /// <summary>
    /// Triggers the sucking animation for observers
    /// </summary>
    /// <param name="suckstate"></param>
    [ObserversRpc]
    public void SuckAnimationObserver(bool suckstate)
    {
        if (IsHost)
            return;
        if (animator != null)
            animator.SetBool("IsSucking", suckstate);
    }

    #endregion
}
