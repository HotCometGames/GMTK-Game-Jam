using UnityEngine;

/// <summary>Standard jump. Costs 1 move by default (set in the Inspector). Only usable when grounded.</summary>
public class JumpAction : MoveActionBase
{
    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 12f;

    protected override bool CanExecute()
    {
        return movement.IsGrounded;
    }

    protected override void Execute()
    {
        movement.SetVerticalVelocity(jumpForce);
    }

    // Push off downward: the dust is what the player kicked away to get airborne, so it should
    // stay on the ground rather than following them up. VfxOrigin is already the feet.
    protected override Vector2 VfxDirection => Vector2.down;
}