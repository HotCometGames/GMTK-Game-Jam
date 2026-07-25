using System.Collections;
using UnityEngine;

/// <summary>
/// Pulls the player to a Boundary collider above and in front of them, within range.
/// The grapple direction uses the player's horizontal velocity when available and
/// otherwise falls back to their facing direction. No GrappleAnchor component is needed.
/// </summary>
public class GrappleAction : MoveActionBase
{
    [Header("Grapple Settings")]
    [SerializeField] private float range = 6f;
    [SerializeField, Min(0.01f)] private float pullSpeed = 20f;
    [SerializeField, Min(0.01f)] private float maxGrappleDuration = 2f;
    [SerializeField, Range(0f, 1f)] private float upwardBias = 0.7f;
    [SerializeField] private float castRadius = 0.1f;
    [SerializeField] private float upwardExitBoost = 3f;

    [Header("Visuals (Optional)")]
    [Tooltip("A LineRenderer used as the visible grapple rope. Leave empty to disable the rope visual.")]
    [SerializeField] private LineRenderer grappleLine;
    [Tooltip("Optional override material for the rope. Defaults to the Player SpriteRenderer material.")]
    [SerializeField] private Material grappleLineMaterial;

    private Vector2 targetPosition;
    private Vector2 grappleSurface;

    protected override void Awake()
    {
        base.Awake();

        if (grappleLine == null)
            grappleLine = GetComponent<LineRenderer>();

        if (grappleLine != null)
        {
            grappleLine.positionCount = 2;
            if (grappleLineMaterial != null)
                grappleLine.sharedMaterial = grappleLineMaterial;
            else if (grappleLine.sharedMaterial == null)
            {
                SpriteRenderer playerSprite = GetComponent<SpriteRenderer>();
                if (playerSprite != null)
                    grappleLine.sharedMaterial = playerSprite.sharedMaterial;
            }

            grappleLine.enabled = false;
        }
    }

    private void OnDisable()
    {
        if (grappleLine != null)
            grappleLine.enabled = false;
    }

    protected override bool CanExecute()
    {
        return TryFindGrappleTarget(out targetPosition);
    }

    protected override void Execute()
    {
        StartCoroutine(PullRoutine(targetPosition, grappleSurface));
    }

    private bool TryFindGrappleTarget(out Vector2 grappleTarget)
    {
        Vector2 origin = transform.position;
        Vector2 velocity = movement.Rb.linearVelocity;

        // Preserve the player's momentum direction while airborne. A near-zero
        // horizontal velocity means they are standing still or travelling straight up/down,
        // so their facing direction is the best indication of where they intend to grapple.
        float forward = Mathf.Abs(velocity.x) > 0.01f
            ? Mathf.Sign(velocity.x)
            : movement.FacingDirection;
        Vector2 direction = new Vector2(forward, upwardBias).normalized;

        int boundariesLayer = LayerMask.GetMask("Boundaries");
        if (boundariesLayer == 0)
        {
            Debug.LogWarning("GrappleAction requires a layer named 'Boundaries'.", this);
            grappleTarget = default;
            return false;
        }

        RaycastHit2D hit = Physics2D.CircleCast(origin, castRadius, direction, range, boundariesLayer);
        if (hit.collider == null)
        {
            grappleTarget = default;
            return false;
        }

        grappleSurface = hit.point;

        // Aim for a point just outside the wall rather than its contact point. This gives
        // the player's own collider room to stop cleanly instead of being pulled into it.
        Collider2D playerCollider = GetComponent<Collider2D>();
        float playerExtent = playerCollider == null
            ? 0f
            : Mathf.Abs(hit.normal.x) * playerCollider.bounds.extents.x +
              Mathf.Abs(hit.normal.y) * playerCollider.bounds.extents.y;
        grappleTarget = hit.point + hit.normal * (playerExtent + 0.02f);
        return true;
    }

    private IEnumerator PullRoutine(Vector2 targetPosition, Vector2 ropeEndPosition)
    {
        movement.SetHorizontalOverride(true);

        float originalGravity = movement.Rb.gravityScale;
        movement.Rb.gravityScale = 0f; // ignore gravity for the duration of the pull

        float elapsed = 0f;
        while ((movement.Rb.position - targetPosition).sqrMagnitude > 0.0001f &&
               elapsed < maxGrappleDuration)
        {
            UpdateGrappleLine(ropeEndPosition);

            Vector2 toTarget = targetPosition - movement.Rb.position;
            float finalStepSpeed = toTarget.magnitude / Time.fixedDeltaTime;
            movement.SetVelocity(toTarget.normalized * Mathf.Min(pullSpeed, finalStepSpeed));
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        movement.SetVelocity(Vector2.zero);
        movement.Rb.gravityScale = originalGravity;
        movement.SetHorizontalOverride(false);
        movement.SetVerticalVelocity(upwardExitBoost);

        if (grappleLine != null)
            grappleLine.enabled = false;
    }

    private void UpdateGrappleLine(Vector2 grappleTarget)
    {
        if (grappleLine == null)
            return;

        grappleLine.enabled = true;
        grappleLine.SetPosition(0, transform.position);
        grappleLine.SetPosition(1, grappleTarget);
    }

    private void OnDrawGizmosSelected()
    {
        float forward = movement != null && Mathf.Abs(movement.Rb.linearVelocity.x) > 0.01f
            ? Mathf.Sign(movement.Rb.linearVelocity.x)
            : (movement != null ? movement.FacingDirection : 1f);
        Vector2 direction = new Vector2(forward, upwardBias).normalized;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, castRadius);
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + direction * range);
    }
}
