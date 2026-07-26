using UnityEngine;

/// <summary>
/// Standard jump with coyote time and input buffering. Costs 1 move by default (set in the Inspector).
/// </summary>
public class JumpAction : MoveActionBase
{
    private const float FirstLevel2KJumpMultiplier = 0.9212f; // 2% weaker than the previous 0.94x.

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 12f;
    [Tooltip("How long after leaving a ledge the player may still jump.")]
    [SerializeField, Min(0f)] private float coyoteTime = 0.05f;
    [Tooltip("How long a jump press is remembered before the player lands.")]
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;

    private static bool hasAttemptedLevel2K;
    private bool reduceFirst2KJump;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool coyoteJumpConsumed;
    private bool hasBeenAirborneSinceJump;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAttemptState() => hasAttemptedLevel2K = false;

    protected override void Awake()
    {
        base.Awake();
        reduceFirst2KJump = gameObject.scene.name == "Level 2K" && !hasAttemptedLevel2K;
        hasAttemptedLevel2K |= reduceFirst2KJump;
    }

    protected override void Update()
    {
        UpdateCoyoteTimer();
        UpdateJumpBuffer();
        base.Update();
    }

    private void UpdateCoyoteTimer()
    {
        // A coyote jump is spent until the player has genuinely gone airborne and landed again.
        // This prevents the ground overlap from granting a second jump for a frame or two after
        // the initial jump has already applied its upward velocity.
        if (coyoteJumpConsumed)
        {
            if (!movement.IsGrounded)
            {
                hasBeenAirborneSinceJump = true;
            }
            else if (hasBeenAirborneSinceJump)
            {
                coyoteJumpConsumed = false;
                hasBeenAirborneSinceJump = false;
            }
        }

        if (movement.IsGrounded && !coyoteJumpConsumed)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.deltaTime);
    }

    private void UpdateJumpBuffer()
    {
        if (!movement.AcceptsInput || !IsUnlocked)
        {
            jumpBufferTimer = 0f;
            return;
        }

        if (Input.GetKeyDown(inputKey))
            jumpBufferTimer = jumpBufferTime;
        else
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
    }

    protected override bool IsTriggerRequested()
    {
        // Keep the normal exact-frame input path when buffering is configured to zero.
        return Input.GetKeyDown(inputKey) || jumpBufferTimer > 0f;
    }

    protected override bool CanExecute()
    {
        // Keep the normal grounded jump path when coyote time is configured to zero.
        return (movement.IsGrounded || coyoteTimer > 0f) && !coyoteJumpConsumed;
    }

    protected override void Execute()
    {
        movement.SetVerticalVelocity(jumpForce * (reduceFirst2KJump ? FirstLevel2KJumpMultiplier : 1f));
        reduceFirst2KJump = false;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        coyoteJumpConsumed = true;
        hasBeenAirborneSinceJump = false;
    }

    protected override void OnDepleted()
    {
        // A buffered press represents one attempt, so do not retry it every frame after depletion.
        jumpBufferTimer = 0f;
        base.OnDepleted();
    }

    // Push off downward: the dust is what the player kicked away to get airborne, so it should
    // stay on the ground rather than following them up. VfxOrigin is already the feet.
    protected override Vector2 VfxDirection => Vector2.down;
}
