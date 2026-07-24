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
    [Tooltip("What counts as 'blocked' when checking the destination tile - walls, other blocks, etc.")]
    [SerializeField] private LayerMask blockingLayer;

    /// <summary>Checked by PushAction before spending a move, so a blocked push never gets charged.</summary>
    public bool CanBePushed(Vector2 direction)
    {
        Vector2 destination = (Vector2)transform.position + direction * tileSize;
        Collider2D hit = Physics2D.OverlapBox(destination, Vector2.one * 0.9f, 0f, blockingLayer);
        return hit == null;
    }

    public void Push(Vector2 direction)
    {
        transform.position = (Vector2)transform.position + direction * tileSize;
    }
}