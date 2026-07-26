using UnityEngine;

/// <summary>
/// Add this to the solid platform collider underneath a FallingSpikes2D trap.
/// The first time the player contacts the top of that platform while not moving
/// upward, it activates the assigned spikes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FallingSpikesLandingTrigger2D : MonoBehaviour
{
    [Tooltip("The FallingSpikes2D object above this platform.")]
    [SerializeField] private FallingSpikes2D spikes;

    [SerializeField] private string playerTag = "Player";

    [Tooltip("Allows for the small amount of contact offset produced by the physics solver.")]
    [SerializeField, Min(0f)] private float topTolerance = 0.08f;

    [Tooltip("Positive vertical speeds above this value count as rising, not landing.")]
    [SerializeField] private float maximumUpwardSpeed = 0.1f;

    private Collider2D platformCollider;
    private bool consumed;

    private void Awake()
    {
        platformCollider = GetComponent<Collider2D>();

        if (platformCollider.isTrigger)
        {
            Debug.LogWarning(
                $"{nameof(FallingSpikesLandingTrigger2D)} must be on the platform's solid, non-trigger collider.",
                this);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryActivate(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // This also handles a player who first touches the platform's side and
        // then settles onto its top without producing another enter callback.
        TryActivate(collision);
    }

    private void TryActivate(Collision2D collision)
    {
        if (consumed || spikes == null || platformCollider == null)
            return;

        if (!collision.gameObject.CompareTag(playerTag))
            return;

        Rigidbody2D playerBody = collision.rigidbody;

        if (playerBody == null)
            return;

        float platformTop = platformCollider.bounds.max.y;
        bool playerIsOnTop = false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).point.y >= platformTop - topTolerance)
            {
                playerIsOnTop = true;
                break;
            }
        }

        bool playerIsNotRising =
            playerBody.linearVelocity.y <= maximumUpwardSpeed;

        if (playerIsOnTop && playerIsNotRising && spikes.Activate())
            consumed = true;
    }
}
