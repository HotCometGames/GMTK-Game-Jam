using UnityEngine;

/// <summary>
/// Handles FREE movement only: horizontal walking, gravity, ground/wall checks, facing direction.
/// Costed abilities (jump, dash, etc.) are separate components under Scripts/Actions
/// that read state from here and apply forces through the public methods below.
///
/// Nothing about "spending a move" lives in this file — that keeps walking/falling free
/// forever, no matter how many action types get added later.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    [Header("Walking (always free)")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 40f;   // units/sec^2 while input is held
    [SerializeField] private float deceleration = 50f;   // units/sec^2 while no input (or overshooting)

    [Header("Ground / Wall Detection")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float wallCheckRadius = 0.15f;
    [SerializeField] private LayerMask wallLayer;

    [Header("Wall Contact")]
    [Tooltip("If no material is assigned, one with 0 friction/bounciness is created automatically. " +
             "This prevents the physics solver from bleeding horizontal push into vertical drag " +
             "(the 'stuck to the wall' freeze).")]
    [SerializeField] private PhysicsMaterial2D wallFrictionOverride;

    [Header("Landing")]
    [Tooltip("Soft 'cushion' thud on touchdown. Leave empty for silence.")]
    [SerializeField] private SoundEventSO landingSound;
    [Tooltip("Minimum downward speed on touchdown to count as a landing — keeps stepping off a small ledge silent.")]
    [SerializeField] private float landingSpeedThreshold = 5f;

    [SerializeField] private VoidEventChannelSO onPlayerDied;
    public Rigidbody2D Rb { get; private set; }
    public Animator Animator { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }
    public int FacingDirection { get; private set; } = 1; // 1 = right, -1 = left
    public bool AcceptsInput { get; private set; } = true;

    /// <summary>True only on the single frame the player touches down. Free for landing VFX/squash later.</summary>
    public bool JustLanded { get; private set; }

    private float horizontalInput;
    private float currentHorizontalVelocity;
    private bool wasGrounded;
    private float lastAirborneFallSpeed;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        Animator = GetComponent<Animator>();

        // Ensure the rigidbody has zero friction so pressing into a wall never
        // produces contact friction that drags down vertical velocity.
        if (Rb.sharedMaterial == null)
        {
            Rb.sharedMaterial = wallFrictionOverride != null
                ? wallFrictionOverride
                : new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
        }
    }

    private void Update()
    {
        if (AcceptsInput)
        {
            horizontalInput = Input.GetAxisRaw("Horizontal");

            if (horizontalInput > 0) FacingDirection = 1;
            else if (horizontalInput < 0) FacingDirection = -1;
        }
        else
        {
            horizontalInput = 0f;
        }

        transform.localScale = new Vector3(FacingDirection, 1f, 1f);

        IsGrounded = groundCheck != null &&
            Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // Touchdown is a rising edge on IsGrounded. By the time physics reports us grounded the
        // Rigidbody has already been stopped by the collision, so judge the impact on the speed we
        // carried in the air last frame rather than on the current (near-zero) velocity.
        JustLanded = IsGrounded && !wasGrounded;
        if (JustLanded && lastAirborneFallSpeed < -landingSpeedThreshold)
            AudioManager.Play(landingSound);

        wasGrounded = IsGrounded;
        lastAirborneFallSpeed = IsGrounded ? 0f : Rb.linearVelocity.y;

        IsTouchingWall = wallCheck != null &&
            Physics2D.OverlapCircle(wallCheck.position, wallCheckRadius, wallLayer);

        if (transform.position.y < -20f)
        {
            Die();
        }

        Animation();
    }

    private void FixedUpdate()
    {
        // Free horizontal movement — actions can override this per-frame via SetHorizontalOverride
        if (!horizontalOverrideActive)
        {
            float targetVelocity = horizontalInput * moveSpeed;

            // If we're touching a wall AND actively pushing into it (input matches the
            // direction we're facing, which is the direction wallCheck points), don't
            // command velocity into the wall at all. This is what actually stops the
            // "freeze" — we're not fighting the solver, so it never has to apply a
            // corrective/frictional force that could bleed into the vertical axis.
            // Vertical velocity (gravity, jump arcs, etc.) is completely untouched below,
            // so the player keeps falling/rising and slides freely along the wall.
            bool pushingIntoWall = IsTouchingWall && Mathf.Sign(horizontalInput) == FacingDirection && horizontalInput != 0;
            if (pushingIntoWall)
            {
                targetVelocity = 0f;
            }

            float rate = Mathf.Abs(horizontalInput) > 0.01f && !pushingIntoWall ? acceleration : deceleration;
            currentHorizontalVelocity = Mathf.MoveTowards(currentHorizontalVelocity, targetVelocity, rate * Time.fixedDeltaTime);

            Rb.linearVelocity = new Vector2(currentHorizontalVelocity, Rb.linearVelocity.y);
        }
        else
        {
            // Keep our tracked value in sync so acceleration resumes smoothly once
            // an override (dash, etc.) ends, instead of snapping/jerking.
            currentHorizontalVelocity = Rb.linearVelocity.x;
        }
    }

    private void Animation()
    {
        Animator.SetFloat("Speed", Mathf.Abs(currentHorizontalVelocity));
        Animator.SetBool("IsGrounded", IsGrounded);
        Animator.SetFloat("YSpeed", Rb.linearVelocity.y);
    }

    // ---- Hooks for action scripts (Jump, Dash, etc.) to use ----

    private bool horizontalOverrideActive;

    /// <summary>Actions call this to temporarily take over horizontal movement (e.g. during a dash).</summary>
    public void SetHorizontalOverride(bool active) => horizontalOverrideActive = active;

    /// <summary>Enables or blocks player-controlled movement and actions during scene transitions.</summary>
    public void SetInputEnabled(bool enabled)
    {
        AcceptsInput = enabled;
        if (!enabled)
        {
            horizontalInput = 0f;
            SetHorizontalOverride(false);
        }
    }

    /// <summary>Set vertical velocity directly (used by Jump).</summary>
    public void SetVerticalVelocity(float y)
    {
        Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, y);
    }

    /// <summary>Apply an instantaneous velocity, e.g. a dash burst.</summary>
    public void SetVelocity(Vector2 velocity)
    {
        Rb.linearVelocity = velocity;
        currentHorizontalVelocity = velocity.x;
    }

    /// <summary>Raises the shared death event used by hazards, move depletion, and the run manager.</summary>
    public void Die()
    {
        onPlayerDied?.Raise();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (groundCheck != null) Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        Gizmos.color = Color.cyan;
        if (wallCheck != null) Gizmos.DrawWireSphere(wallCheck.position, wallCheckRadius);
    }
}
