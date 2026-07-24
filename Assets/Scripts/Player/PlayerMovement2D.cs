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

    [Header("Ground / Wall Detection")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float wallCheckRadius = 0.15f;
    [SerializeField] private LayerMask wallLayer;

    [SerializeField] private VoidEventChannelSO onPlayerDied;
    public Rigidbody2D Rb { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }
    public int FacingDirection { get; private set; } = 1; // 1 = right, -1 = left

    private float horizontalInput;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        if (horizontalInput > 0) FacingDirection = 1;
        else if (horizontalInput < 0) FacingDirection = -1;
        transform.localScale = new Vector3(FacingDirection, 1f, 1f);

        IsGrounded = groundCheck != null &&
            Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        IsTouchingWall = wallCheck != null &&
            Physics2D.OverlapCircle(wallCheck.position, wallCheckRadius, wallLayer);

        if (transform.position.y < -20f)
        {
            onPlayerDied?.Raise();
        }
    }

    private void FixedUpdate()
    {
        // Free horizontal movement — actions can override this per-frame via SetHorizontalOverride
        if (!horizontalOverrideActive)
        {
            Rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, Rb.linearVelocity.y);
        }
    }

    // ---- Hooks for action scripts (Jump, Dash, etc.) to use ----

    private bool horizontalOverrideActive;

    /// <summary>Actions call this to temporarily take over horizontal movement (e.g. during a dash).</summary>
    public void SetHorizontalOverride(bool active) => horizontalOverrideActive = active;

    /// <summary>Set vertical velocity directly (used by Jump).</summary>
    public void SetVerticalVelocity(float y)
    {
        Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, y);
    }

    /// <summary>Apply an instantaneous velocity, e.g. a dash burst.</summary>
    public void SetVelocity(Vector2 velocity)
    {
        Rb.linearVelocity = velocity;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (groundCheck != null) Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        Gizmos.color = Color.cyan;
        if (wallCheck != null) Gizmos.DrawWireSphere(wallCheck.position, wallCheckRadius);
    }
}