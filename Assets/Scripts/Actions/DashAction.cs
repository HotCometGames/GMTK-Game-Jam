using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dash in the direction the player is facing. While dashing, colliders on the
/// Destroyable layer are destroyed. Costs 1 move by default.
/// Usable anywhere (ground or air); add conditions in CanExecute to restrict it.
/// </summary>
public class DashAction : MoveActionBase
{
    private const float BreakLookAheadPadding = 0.05f;
    private const float GlassExitSpeedMultiplier = 0.7f;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDuration = 0.15f;

    [Header("Dash Trail")]
    [Tooltip("Streak drawn behind the player for the length of the dash. A trail reads as a pen " +
             "stroke in a way a particle burst can't, which is why speed gets a trail and " +
             "everything else gets particles. Leave empty to skip.")]
    [SerializeField] private TrailRenderer dashTrail;

    private bool isDashing;
    private int dashDirection;
    private int destroyableLayerMask;
    private Collider2D playerCollider;
    private ContactFilter2D destroyableFilter;
    private readonly List<RaycastHit2D> breakHits = new List<RaycastHit2D>(4);

    protected override void Awake()
    {
        base.Awake();

        playerCollider = GetComponent<Collider2D>();
        destroyableLayerMask = LayerMask.GetMask("Destroyable");
        destroyableFilter = new ContactFilter2D();
        destroyableFilter.SetLayerMask(destroyableLayerMask);
        destroyableFilter.useTriggers = true;

        // Off until a dash actually happens, otherwise the player drags a permanent streak around.
        if (dashTrail != null)
        {
            dashTrail.Clear();
            dashTrail.emitting = false;
        }
    }

    protected override bool CanExecute()
    {
        return !isDashing;
    }

    protected override void Execute()
    {
        StartCoroutine(DashRoutine());
    }

    // The burst fires backwards out of the dash, so it reads as something left behind rather than
    // something pushing the player along.
    protected override Vector2 VfxDirection => new Vector2(-movement.FacingDirection, 0.25f);

    private void FixedUpdate()
    {
        if (isDashing)
            BreakGlassAhead();
    }

    private IEnumerator DashRoutine()
    {
        isDashing = true;
        dashDirection = movement.FacingDirection;
        movement.SetHorizontalOverride(true);
        movement.SetVelocity(new Vector2(dashDirection * dashSpeed, movement.Rb.linearVelocity.y));

        // Remove glass before the next physics simulation so its solid collider never gets a
        // chance to stop the dash. This also catches panes the player is already touching.
        BreakGlassAhead();

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

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDestroy(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDestroy(other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDestroy(other.gameObject);
    }

    private void BreakGlassAhead()
    {
        if (playerCollider == null || destroyableLayerMask == 0)
            return;

        Vector2 nextStep = movement.Rb.linearVelocity * Time.fixedDeltaTime;
        nextStep.x = dashDirection *
            (Mathf.Abs(dashSpeed) * Time.fixedDeltaTime + BreakLookAheadPadding);

        float castDistance = nextStep.magnitude;
        if (castDistance <= 0f)
            return;

        breakHits.Clear();
        playerCollider.Cast(
            nextStep / castDistance,
            destroyableFilter,
            breakHits,
            castDistance);

        foreach (RaycastHit2D hit in breakHits)
            TryDestroy(hit.collider.gameObject);
    }

    private void TryDestroy(GameObject target)
    {
        if (!isDashing || target == null ||
            (destroyableLayerMask & (1 << target.layer)) == 0)
            return;

        // Destroy is deferred until the end of the frame. Deactivating immediately removes the
        // solid collider now, before another physics step can absorb the dash's velocity.
        target.SetActive(false);
        Destroy(target);

        // Collision callbacks run after contact resolution, so restore enough horizontal speed
        // to carry through the pane, with a 30% impact penalty. Vertical momentum stays untouched.
        movement.SetVelocity(new Vector2(
            dashDirection * dashSpeed * GlassExitSpeedMultiplier,
            movement.Rb.linearVelocity.y));
    }
}
