using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object.Synchronizing;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float groundDrag = 5f;
    [SerializeField] private Animator animator;

    [Header("Jumping Settings")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float jumpCooldown = 1f;
    [SerializeField] private float airMultiplier = 1.5f;
    private bool readyToJump;

    [Header("Input Settings")]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Ground Check Settings")]
    [SerializeField] private float playerHeight = 2f;
    [SerializeField] private LayerMask groundLayer;
    private bool grounded;

    [Header("Slope Check Settings")]
    [SerializeField] private float maxSlopeAngle = 45f;
    [SerializeField] private float minSlopeAngle = 0f;
    private RaycastHit slopeHit;

    [Header("Character Settings")]
    [SerializeField] private GameObject[] characters;
    [SyncVar] public int characterIndex;
    private List<GameObject> gunLights = new List<GameObject>();
    private List<GameObject> tankLights = new List<GameObject>();

    [SyncVar] private bool canSteal;

    [SerializeField] private Transform orientation;
    [SerializeField] private TMP_Text speedText;

    private float horizontalInput;
    private float verticalInput;

    private Vector3 moveDirection;

    private Rigidbody rb;

    [SyncVar][SerializeField] private bool isStunned;
    public bool IsStunned
    {
        get { return isStunned; }
        set { isStunned = value; }
    }

    [SerializeField] private float stunDuration = 5f;
    [SerializeField] private float raycastLength = 0.5f;
    private bool isWallWalking;

    [SerializeField] private StofZuiger stofZuiger;

    public MovementState state;

    public enum MovementState
    {
        Walking,
        Running,
        Airborne
    }

    /// <summary>
    /// Initialize the Rigidbody and readyToJump flag.
    /// </summary>
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        readyToJump = true;
    }

    /// <summary>
    /// Sets up the player for the network environment, including character selection.
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        // Disable script for non-owner or non-host
        if (IsHost && gameObject.GetComponent<PlayerData>().playerId != 0)
        {
            this.enabled = false;
            return;
        }
        else if (!IsOwner)
        {
            this.enabled = false;
            return;
        }

        int characterId = PlayerPrefs.GetInt("Character");
        SetCharacter(characterId);
    }

    /// <summary>
    /// Sets the active character for the player.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void SetCharacter(int index)
    {
        characterIndex = index;

        // Deactivate all characters and activate the selected one
        for (int i = 0; i < characters.Length; i++)
        {
            characters[i].SetActive(i == index);
        }

        animator = characters[index].GetComponent<Animator>();
        stofZuiger.animator = characters[index].GetComponent<Animator>();

        // Initialize lights and components associated with the selected character
        gunLights.AddRange(new GameObject[]
        {
            characters[index].transform.GetChild(1).gameObject,
            characters[index].transform.GetChild(2).gameObject,
            characters[index].transform.GetChild(3).gameObject
        });

        tankLights.AddRange(new GameObject[]
        {
            characters[index].transform.GetChild(5).gameObject,
            characters[index].transform.GetChild(6).gameObject,
            characters[index].transform.GetChild(7).gameObject
        });

        stofZuiger.tornado = characters[index].transform.GetChild(4).GetChild(1).GetChild(0).GetChild(0).gameObject;
    }

    private bool characterSet;

    /// <summary>
    /// Handles updates for input, ground checks, movement state, and animation.
    /// </summary>
    private void Update()
    {
        if (!characterSet && characterIndex >= 0)
        {
            characterSet = true;
            SetCharacter(characterIndex);
        }

        if (animator == null) return;

        // Handle stun effect
        if (isStunned)
        {
            SetBoolAnim("IsHit", true);
            return;
        }
        else
        {
            SetBoolAnim("IsHit", false);
        }

        // Ground Check
        grounded = Physics.Raycast(transform.position, Vector3.down, 0.1f, groundLayer);

        ProcessInput();
        ApplySpeedControl();
        HandleMovementState();
        SetBoolAnim("OnGround", grounded);

        // Handle drag
        rb.drag = grounded ? groundDrag : 0f;

        UpdateTankLights(stofZuiger.GhostPoints);
    }

    /// <summary>
    /// Handles player movement input.
    /// </summary>
    private void ProcessInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        UpdateBlendTree(horizontalInput, verticalInput);

        SetBoolAnim("IsWalking", horizontalInput != 0 || verticalInput != 0);

        if (Input.GetKey(jumpKey) && readyToJump && grounded)
        {
            readyToJump = false;
            Jump();
            SetBoolAnim("Jump", true);
            Invoke(nameof(ResetJump), jumpCooldown);
        }
        else if (!Input.GetKey(jumpKey))
        {
            SetBoolAnim("Jump", false);
        }
    }

    /// <summary>
    /// Handles different movement states (walking, running, airborne).
    /// </summary>
    private void HandleMovementState()
    {
        if (grounded && Input.GetKey(sprintKey))
        {
            state = MovementState.Running;
            moveSpeed = sprintSpeed;
        }
        else if (grounded)
        {
            state = MovementState.Walking;
            moveSpeed = walkSpeed;
        }
        else
        {
            state = MovementState.Airborne;
        }
    }

    /// <summary>
    /// Handles the player's movement physics.
    /// </summary>
    private void FixedUpdate()
    {
        if (isStunned) return;

        MovePlayer();
    }

    /// <summary>
    /// Applies movement force based on the current movement state.
    /// </summary>
    private void MovePlayer()
    {
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        // Check for wall walking
        RaycastHit hit;
        if (Physics.Raycast(transform.position, moveDirection, out hit, raycastLength) && hit.transform.tag != "SuckBox")
        {
            isWallWalking = true;
        }
        else
        {
            isWallWalking = false;
        }

        if (isWallWalking)
        {
            rb.AddForce(Vector3.down * 10f, ForceMode.Force);
            return;
        }

        // Slope Handling
        if (OnSlope())
        {
            rb.AddForce(GetSlopeMoveDirection() * moveSpeed * 10f, ForceMode.Force);
            if (rb.velocity.y > 0)
                rb.AddForce(Vector3.down * 40f, ForceMode.Force);
        }
        else if (grounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        }
        else
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
        }

        // Turn off gravity while on slope
        rb.useGravity = !OnSlope();
    }

    /// <summary>
    /// Limits the player's speed based on terrain and movement state.
    /// </summary>
    private void ApplySpeedControl()
    {
        if (OnSlope() && rb.velocity.magnitude > moveSpeed)
        {
            rb.velocity = rb.velocity.normalized * moveSpeed;
        }
        else
        {
            Vector3 flatVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (flatVelocity.magnitude > moveSpeed)
            {
                rb.velocity = new Vector3(flatVelocity.normalized.x, rb.velocity.y, flatVelocity.normalized.z) * moveSpeed;
            }
        }
    }

    /// <summary>
    /// Performs a jump by applying force to the Rigidbody.
    /// </summary>
    private void Jump()
    {
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    /// <summary>
    /// Resets the ability to jump after the cooldown.
    /// </summary>
    private void ResetJump()
    {
        readyToJump = true;
    }

    /// <summary>
    /// Determines if the player is on a slope based on the ground angle.
    /// </summary>
    private bool OnSlope()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight))
        {
            float angle = Vector3.Angle(slopeHit.normal, Vector3.up);
            return angle <= maxSlopeAngle && angle >= minSlopeAngle;
        }
        return false;
    }

    /// <summary>
    /// Gets the direction to move the player while on a slope.
    /// </summary>
    private Vector3 GetSlopeMoveDirection()
    {
        return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
    }

    /// <summary>
    /// Updates the animator with movement input values.
    /// </summary>
    private void UpdateBlendTree(float horizontal, float vertical)
    {
        animator.SetFloat("MoveX", horizontal);
        animator.SetFloat("MoveY", vertical);
    }

    /// <summary>
    /// Updates the tank lights based on the ghost points.
    /// </summary>
    private void UpdateTankLights(int ghostPoints)
    {
        if (ghostPoints >= 5)
        {
            foreach (var light in tankLights)
            {
                light.SetActive(true);
            }
        }
        else
        {
            foreach (var light in tankLights)
            {
                light.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Sets a boolean parameter in the animator.
    /// </summary>
    private void SetBoolAnim(string param, bool value)
    {
        if (animator != null)
        {
            animator.SetBool(param, value);
        }
    }
}