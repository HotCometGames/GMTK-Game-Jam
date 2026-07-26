using UnityEngine;

/// <summary>
/// A one-shot ceiling trap. It waits for a FallingSpikesLandingTrigger2D to
/// activate it, falls once, plays a break burst, and destroys itself.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class FallingSpikes2D : MonoBehaviour
{
    [Header("Fall")]
    [Tooltip("How far downward the spikes fall, in world units.")]
    [SerializeField, Min(0f)] private float fallDistance = 3f;

    [Tooltip("How quickly the spikes are moving when they first release.")]
    [SerializeField, Min(0.01f)] private float fallSpeed = 6f;

    [Tooltip("How much downward speed is added every second. This creates the gravity-like curve.")]
    [SerializeField, Min(0f)] private float fallAcceleration = 5f;

    [Tooltip("The fastest speed the spikes can reach while falling.")]
    [SerializeField, Min(0.01f)] private float maximumFallSpeed = 8.5f;

    [Tooltip("Optional pause after the player lands before the spikes begin falling.")]
    [SerializeField, Min(0f)] private float activationDelay = 0.15f;

    [Tooltip("How long the spikes remain at the bottom before breaking. At least one physics step is always used.")]
    [SerializeField, Min(0f)] private float breakDelayAfterImpact = 0.1f;

    [Header("Break")]
    [Tooltip("Assign Assets/VFX/Events/VFX_Death.asset to reuse the player's death burst.")]
    [SerializeField] private VfxEventSO breakVfx;

    [Tooltip("Optional point where the break burst appears. Leave empty to use this object's position.")]
    [SerializeField] private Transform breakVfxOrigin;

    [SerializeField, Min(0f)] private float breakVfxIntensity = 1f;

    public bool HasActivated => state != TrapState.Idle;
    public bool HasFinished => state == TrapState.Finished;

    private enum TrapState
    {
        Idle,
        ActivationDelay,
        Falling,
        ImpactLinger,
        Finished
    }

    private Rigidbody2D body;
    private Vector2 topPosition;
    private Vector2 bottomPosition;
    private float delayRemaining;
    private float impactWaitRemaining;
    private float currentFallSpeed;
    private TrapState state;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;

        topPosition = body.position;
        bottomPosition = topPosition + Vector2.down * fallDistance;
        state = TrapState.Idle;
    }

    /// <summary>
    /// Arms the trap once. Returns false if it was already activated or finished.
    /// </summary>
    public bool Activate()
    {
        if (state != TrapState.Idle)
            return false;

        delayRemaining = activationDelay;
        currentFallSpeed = Mathf.Max(0.01f, fallSpeed);
        state = delayRemaining > 0f
            ? TrapState.ActivationDelay
            : TrapState.Falling;

        return true;
    }

    private void FixedUpdate()
    {
        if (state == TrapState.ActivationDelay)
        {
            delayRemaining = Mathf.Max(
                0f,
                delayRemaining - Time.fixedDeltaTime);

            if (delayRemaining <= 0f)
                state = TrapState.Falling;

            return;
        }

        if (state == TrapState.ImpactLinger)
        {
            impactWaitRemaining = Mathf.Max(
                0f,
                impactWaitRemaining - Time.fixedDeltaTime);

            if (impactWaitRemaining <= 0f)
                BreakApart();

            return;
        }

        if (state != TrapState.Falling)
            return;

        float terminalSpeed = Mathf.Max(
            currentFallSpeed,
            maximumFallSpeed);

        float previousFallSpeed = currentFallSpeed;

        currentFallSpeed = Mathf.MoveTowards(
            currentFallSpeed,
            terminalSpeed,
            Mathf.Max(0f, fallAcceleration) * Time.fixedDeltaTime);

        // Average speed gives the distance travelled by a steadily
        // accelerating object during this physics step.
        float stepSpeed =
            (previousFallSpeed + currentFallSpeed) * 0.5f;

        Vector2 newPosition = Vector2.MoveTowards(
            body.position,
            bottomPosition,
            stepSpeed * Time.fixedDeltaTime);

        body.MovePosition(newPosition);

        if ((newPosition - bottomPosition).sqrMagnitude <= 0.000001f)
        {
            // Keep the spikes alive at the bottom for at least one complete
            // physics step so their kill trigger cannot disappear before impact.
            impactWaitRemaining = Mathf.Max(
                breakDelayAfterImpact,
                Time.fixedDeltaTime);

            state = TrapState.ImpactLinger;
        }
    }

    private void BreakApart()
    {
        if (state == TrapState.Finished)
            return;

        state = TrapState.Finished;

        Vector3 effectPosition = breakVfxOrigin != null
            ? breakVfxOrigin.position
            : transform.position;

        // VfxManager owns world-space emitters, so the particles remain after
        // this falling-spike GameObject is destroyed.
        VfxManager.Play(
            breakVfx,
            effectPosition,
            Vector2.up,
            breakVfxIntensity);

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 top = Application.isPlaying
            ? topPosition
            : (Vector2)transform.position;

        Vector2 bottom = top + Vector2.down * fallDistance;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(top, bottom);
        Gizmos.DrawWireSphere(top, 0.12f);
        Gizmos.DrawWireSphere(bottom, 0.12f);
    }
}
