using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

public class CameraController : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float sensitivity = 1f;
    [SerializeField] private Transform orientation;
    [SerializeField] private Camera camera;
    [SerializeField] private StofZuiger stofZuiger;
    [SerializeField] private PlayerData playerData;
    [SerializeField] private KeyCode useKey = KeyCode.E;

    [SerializeField] private float useRange = 5f;
    private RaycastHit hit;

    private float xRotation = 0f;
    private float yRotation = 0f;

    private bool hasFov = false;

    /// <summary>
    /// Initialize the camera and lock/unlock cursor based on client ownership.
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
        {
            this.enabled = false;
            return;
        }

        camera = FindObjectOfType<Camera>();
        camera.transform.SetParent(transform);
        camera.transform.position = this.transform.position;
        camera.transform.rotation = Quaternion.identity;
    }

    /// <summary>
    /// Sets the cursor visibility and lock state.
    /// </summary>
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    /// <summary>
    /// Update the camera view based on mouse input and handle interaction with objects.
    /// </summary>
    private void Update()
    {
        if (camera != null && !hasFov)
        {
            hasFov = true;
            camera.fieldOfView = PlayerPrefs.GetInt("fov");
        }

        HandleInteraction();
        HandleMouseLook();
    }

    /// <summary>
    /// Handles player interaction with objects based on the player's team.
    /// </summary>
    private void HandleInteraction()
    {
        if (Input.GetKeyDown(useKey))
        {
            if (playerData.teamID == 0)
            {
                TryInteract("Canister", stofZuiger.StorePoints);
                TryInteractWithPlayer(1);
                TryInteractWithDoor();
            }
            else
            {
                TryInteract("Canister2", stofZuiger.StorePoints);
                TryInteractWithPlayer(0);
                TryInteractWithDoor();
            }
        }
    }

    /// <summary>
    /// Tries to interact with a specific tagged object.
    /// </summary>
    private void TryInteract(string tag, System.Action interaction)
    {
        if (Physics.Raycast(transform.position, transform.forward, out hit, useRange))
        {
            if (hit.transform.CompareTag(tag))
            {
                interaction();
            }
        }
    }

    /// <summary>
    /// Tries to interact with another player on the opposite team for stealing points.
    /// </summary>
    private void TryInteractWithPlayer(int opposingTeamID)
    {
        if (Physics.Raycast(transform.position, transform.forward, out hit, useRange))
        {
            if (hit.transform.CompareTag("Player"))
            {
                var otherPlayerData = hit.transform.GetComponent<PlayerData>();
                if (otherPlayerData != null && otherPlayerData.teamID == opposingTeamID)
                {
                    var movement = hit.transform.GetComponent<MovementAdvanced>();
                    if (movement != null && movement.IsStunned && movement.GetCanSteal())
                    {
                        movement.SetCanSteal(false);
                        stofZuiger.StealPoints(hit.transform.GetChild(1).GetChild(0).GetChild(0).GetComponent<StofZuiger>().GhostPoints,
                            hit.transform.GetChild(1).GetChild(0).GetChild(0).GetComponent<StofZuiger>());
                    }
                }
            }
        }
    }

    /// <summary>
    /// Tries to interact with a door object and triggers its animation.
    /// </summary>
    private void TryInteractWithDoor()
    {
        if (Physics.Raycast(transform.position, transform.forward, out hit, useRange))
        {
            if (hit.transform.CompareTag("Door"))
            {
                SetBoolAnim(hit.transform);
            }
        }
    }

    /// <summary>
    /// Updates the camera's rotation based on mouse movement input.
    /// </summary>
    private void HandleMouseLook()
    {
        if (!GameManager.MouseLocked) return;

        float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sensitivity;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);
    }

    /// <summary>
    /// Sends a request to the server to trigger the animation for the door.
    /// </summary>
    [ServerRpc(RequireOwnership = true)]
    public void SetBoolAnim(Transform hit)
    {
        hit.GetComponent<Animator>().SetTrigger("Toggle");
        SetBoolObserver(hit);
    }

    /// <summary>
    /// Triggers the animation for all clients observing the door.
    /// </summary>
    [ObserversRpc]
    public void SetBoolObserver(Transform hit)
    {
        if (IsHost) return;

        hit.GetComponent<Animator>().SetTrigger("Toggle");
    }
}