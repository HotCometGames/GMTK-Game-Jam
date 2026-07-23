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
}