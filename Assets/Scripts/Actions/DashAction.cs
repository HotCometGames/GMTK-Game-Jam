using System.Collections;
using UnityEngine;

/// <summary>
/// Dash in the direction the player is facing. While dashing, colliders on the
/// Destroyable layer are destroyed. Costs 1 move by default.
/// Usable anywhere (ground or air) — remove the "return true" override in CanExecute
/// if you want to restrict it, e.g. air-only or ground-only.
/// </summary>
public class DashAction : MoveActionBase
{
    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDuration = 0.15f;

    private bool isDashing;

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
        isDashing = true;
        movement.SetHorizontalOverride(true);
        movement.SetVelocity(new Vector2(movement.FacingDirection * dashSpeed, 0f));

        yield return new WaitForSeconds(dashDuration);

        isDashing = false;
        movement.SetHorizontalOverride(false);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDestroy(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDestroy(other.gameObject);
    }

    private void TryDestroy(GameObject target)
    {
        if (!isDashing || target.layer != LayerMask.NameToLayer("Destroyable"))
            return;

        Destroy(target);
    }
}
