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

    [Header("Dash Trail")]
    [Tooltip("Streak drawn behind the player for the length of the dash. A trail reads as a pen " +
             "stroke in a way a particle burst can't, which is why speed gets a trail and " +
             "everything else gets particles. Leave empty to skip.")]
    [SerializeField] private TrailRenderer dashTrail;

    private bool isDashing;

    protected override void Awake()
    {
        base.Awake();

        // Off until a dash actually happens, otherwise the player drags a permanent streak around.
        if (dashTrail != null)
        {
            dashTrail.Clear();
            dashTrail.emitting = false;
        }
    }

    protected override bool CanExecute()
    {
        return true; // dash is available anywhere; add conditions here if you want limits
    }

    protected override void Execute()
    {
        StartCoroutine(DashRoutine());
    }

    // The burst fires backwards out of the dash, so it reads as something left behind rather than
    // something pushing the player along.
    protected override Vector2 VfxDirection => new Vector2(-movement.FacingDirection, 0.25f);

    private IEnumerator DashRoutine()
    {
        isDashing = true;
        movement.SetHorizontalOverride(true);
        movement.SetVelocity(new Vector2(movement.FacingDirection * dashSpeed, 0f));

        if (dashTrail != null)
        {
            // Clear before emitting: the renderer keeps its last points, and without this the new
            // streak is drawn joined to wherever the previous dash ended.
            dashTrail.Clear();
            dashTrail.emitting = true;
        }

        yield return new WaitForSeconds(dashDuration);

        isDashing = false;
        movement.SetHorizontalOverride(false);

        // Stop adding points but let the existing streak fade out on its own over the trail's time.
        if (dashTrail != null) dashTrail.emitting = false;
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
