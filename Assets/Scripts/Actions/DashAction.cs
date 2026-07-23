using System.Collections;
using UnityEngine;

/// <summary>
/// Dash in the direction the player is facing. Costs 1 move by default.
/// Usable anywhere (ground or air) — remove the "return true" override in CanExecute
/// if you want to restrict it, e.g. air-only or ground-only.
/// </summary>
public class DashAction : MoveActionBase
{
    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDuration = 0.15f;

    protected override bool CanExecute()
    {
        return true; // dash is available anywhere; add conditions here if you want limits
    }

    protected override void Execute()
    {
        StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        movement.SetHorizontalOverride(true);
        movement.SetVelocity(new Vector2(movement.FacingDirection * dashSpeed, 0f));

        yield return new WaitForSeconds(dashDuration);

        movement.SetHorizontalOverride(false);
    }
}