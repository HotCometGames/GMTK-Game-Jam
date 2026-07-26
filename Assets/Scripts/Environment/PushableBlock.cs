using UnityEngine;

/// <summary>
/// A crate/block that can be shoved one tile by PushAction. Grid-snapped rather than
/// physics-driven, since Tilemap-based levels usually want predictable, exact placement
/// rather than a block that can drift or get stuck at an odd angle.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PushableBlock : MonoBehaviour
{
    [SerializeField] private float tileSize = 1f;
    [SerializeField] private float pushSpeed = 5f;
    [SerializeField] private float friction = 2f;
    [SerializeField] private float stopThreshold = 0.05f;
    [Tooltip("What counts as 'blocked' when checking the destination tile - walls, other blocks, etc.")]
    [SerializeField] private LayerMask blockingLayer;
    [SerializeField] private LayerMask pushingLayer;
    private Rigidbody2D rb;
    private Vector2 moveDirection = Vector2.zero;
    private ContactFilter2D contactFilter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        contactFilter = new ContactFilter2D();
        contactFilter.useTriggers = false;
        contactFilter.useLayerMask = true;
        contactFilter.layerMask = blockingLayer.value | pushingLayer.value;
    }

    private void FixedUpdate()
    {
        if (rb.linearVelocity.sqrMagnitude > 0f)
        {
            if (moveDirection != Vector2.zero)
            {
                RaycastHit2D[] hits = new RaycastHit2D[1];
                float distance = rb.linearVelocity.magnitude * Time.fixedDeltaTime + 0.01f;
                if (rb.Cast(moveDirection, contactFilter, hits, distance) > 0)
                {
                    rb.linearVelocity = Vector2.zero;
                    moveDirection = Vector2.zero;
                    return;
                }
            }

            float speed = rb.linearVelocity.magnitude;
            speed = Mathf.Max(0f, speed - friction * Time.fixedDeltaTime);

            if (speed <= stopThreshold)
            {
                rb.linearVelocity = Vector2.zero;
                moveDirection = Vector2.zero;
            }
            else if (moveDirection != Vector2.zero)
            {
                rb.linearVelocity = moveDirection * speed;
            }
        }
    }

    /// <summary>Checked by PushAction before spending a move, so a blocked push never gets charged.</summary>
    public bool CanBePushed(Vector2 direction)
    {
        Vector2 destination = (Vector2)transform.position + direction * tileSize;
        Collider2D hit = Physics2D.OverlapBox(destination, Vector2.one * 0.9f, 0f, blockingLayer);
        return hit == null;
    }

    public void Push(Vector2 direction)
    {
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        moveDirection = direction.normalized;
        rb.linearVelocity = moveDirection * pushSpeed;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Stop moving when we hit a wall or another block.
        if (IsInLayerMask(collision.gameObject.layer, blockingLayer) || IsInLayerMask(collision.gameObject.layer, pushingLayer))
        {
            rb.linearVelocity = Vector2.zero;
            moveDirection = Vector2.zero;
        }
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}