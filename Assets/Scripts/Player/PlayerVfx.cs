using System;
using UnityEngine;

/// <summary>
/// The three player effects that can't be fired from a single call site the way jump and dash can,
/// because they depend on per-frame movement state (landing, running) or on an event channel (death).
///
/// Everything here routes through VfxManager.Play rather than owning a ParticleSystem, so nothing on
/// the player is parented to a particle emitter. That matters: PlayerMovement2D writes
/// transform.localScale.x = FacingDirection every frame, and a child emitter would mirror itself —
/// and its already-emitted particles — every time the player turns around.
///
/// Drop it on the Player alongside PlayerMovement2D and fill whichever slots you want; each one is
/// independently optional, so a half-wired component is simply a quieter one, never a broken one.
/// </summary>
[RequireComponent(typeof(PlayerMovement2D))]
public class PlayerVfx : MonoBehaviour
{
    [Header("Landing")]
    [Tooltip("Burst on touchdown. Scales with fall speed, so a drop kicks up more than a step-off.")]
    [SerializeField] private VfxEventSO landingVfx;
    [Tooltip("Minimum downward speed on touchdown to show anything. Mirrors PlayerMovement2D's " +
             "landingSpeedThreshold — JustLanded itself fires on every touchdown, including tiny " +
             "step-offs, so without this the player trails dust down a staircase.")]
    [SerializeField] private float landingSpeedThreshold = 5f;
    [Tooltip("Fall speed at which the landing burst reaches full size.")]
    [SerializeField] private float landingFullImpactSpeed = 16f;

    [Header("Running")]
    [Tooltip("Small puffs kicked up while running on the ground.")]
    [SerializeField] private VfxEventSO runDustVfx;
    [Tooltip("Minimum horizontal speed before dust appears — keeps a nudge against a wall clean.")]
    [SerializeField] private float runDustSpeedThreshold = 1.5f;
    [Tooltip("Seconds between puffs while running.")]
    [SerializeField] private float runDustInterval = 0.12f;

    [Header("Death")]
    [Tooltip("Burst when the player dies.")]
    [SerializeField] private VfxEventSO deathVfx;
    [Tooltip("Assets/Events/OnPlayerDied.asset — the same channel PlayerDeathTrigger and the " +
             "fall-out-of-world check already raise, and the same one GameAudioBinder listens to " +
             "for SFX_Death. Listening here rather than at each raise site means both death routes " +
             "are covered, and the burst lands at the player's position instead of the trigger's.")]
    [SerializeField] private VoidEventChannelSO onPlayerDied;

    private PlayerMovement2D movement;
    private Rigidbody2D body;
    private float runDustTimer;
    private Action deathHandler;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement2D>();
        body = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        if (onPlayerDied == null) return;

        // Cache the delegate so OnDisable unsubscribes the exact same instance — a fresh lambda
        // each time would silently leak subscriptions across scene loads (see GameAudioBinder).
        deathHandler = HandlePlayerDied;
        onPlayerDied.OnEventRaised += deathHandler;
    }

    private void OnDisable()
    {
        if (onPlayerDied == null || deathHandler == null) return;

        onPlayerDied.OnEventRaised -= deathHandler;
        deathHandler = null;
    }

    private void Update()
    {
        HandleLanding();
        HandleRunDust();
    }

    private void HandleLanding()
    {
        if (!movement.JustLanded) return;

        float impact = -movement.LastImpactSpeed; // stored negative; flip so bigger = harder
        if (impact < landingSpeedThreshold) return;

        // Ramp from a modest puff at the threshold up to a full burst at terminal-ish speed.
        float intensity = Mathf.Lerp(0.6f, 2f,
            Mathf.InverseLerp(landingSpeedThreshold, landingFullImpactSpeed, impact));

        VfxManager.Play(landingVfx, FeetPosition, Vector2.up, intensity);
    }

    private void HandleRunDust()
    {
        bool running = movement.IsGrounded
            && body != null
            && Mathf.Abs(body.linearVelocity.x) > runDustSpeedThreshold;

        if (!running)
        {
            // Reset rather than pause, so the first step after stopping puffs immediately
            // instead of waiting out whatever was left on the clock.
            runDustTimer = 0f;
            return;
        }

        runDustTimer -= Time.deltaTime;
        if (runDustTimer > 0f) return;

        runDustTimer = runDustInterval;

        // Kick the dust backwards, away from the direction of travel.
        VfxManager.Play(runDustVfx, FeetPosition, new Vector2(-movement.FacingDirection, 0.4f));
    }

    private void HandlePlayerDied()
    {
        VfxManager.Play(deathVfx, transform.position, Vector2.up);
    }

    /// <summary>Ground-check marker if there is one, otherwise the transform origin.</summary>
    private Vector3 FeetPosition =>
        movement.GroundCheck != null ? movement.GroundCheck.position : transform.position;
}
