using UnityEngine;

/// <summary>
/// Shoves a PushableBlock one tile in the direction the player is facing.
/// Like GrappleAction, CanExecute() detects and caches the target block for Execute() to use,
/// and also checks the block's own CanBePushed() so a blocked push is rejected BEFORE any
/// moves get spent - the player should never lose a move to a push that couldn't happen.
/// </summary>
public class PushAction : MoveActionBase
{
    [Header("Push Settings")]
    [SerializeField] private float checkDistance = 0.6f;
    [SerializeField] private LayerMask pushableLayer;

    private PushableBlock targetBlock;
    private Vector2 pushDirection;

    protected override bool CanExecute()
    {
        pushDirection = new Vector2(movement.FacingDirection, 0f);
        Vector2 origin = transform.position;

        RaycastHit2D hit = Physics2D.Raycast(origin, pushDirection, checkDistance, pushableLayer);
        if (hit.collider == null) return false;

        targetBlock = hit.collider.GetComponent<PushableBlock>();
        return targetBlock != null && targetBlock.CanBePushed(pushDirection);
    }

    protected override void Execute()
    {
        targetBlock.Push(pushDirection);
    }
}