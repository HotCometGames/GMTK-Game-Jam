using UnityEngine;

/// <summary>
/// Base class for every costed move action (Jump, Dash, WallBreak, Attack, ...).
///
/// TO ADD A NEW ACTION:
///   1. Create a new script that inherits MoveActionBase.
///   2. Override CanExecute() and Execute().
///   3. Drop it as a component on the Player alongside the others.
/// That's it — input handling, cost spending, and cooldown are all handled here,
/// so a new ability never needs to touch PlayerMovement2D or MoveBudgetSO directly.
/// </summary>
[RequireComponent(typeof(PlayerMovement2D))]
public abstract class MoveActionBase : MonoBehaviour
{
    [Header("Shared Setup")]
    [SerializeField] protected MoveBudgetSO moveBudget;
    [SerializeField] protected KeyCode inputKey = KeyCode.Space;
    [SerializeField] protected int cost = 1;
    [SerializeField] protected float cooldown = 0f;

    [Tooltip("Played when the action successfully fires. Leave empty for silence.")]
    [SerializeField] protected SoundEventSO performSound;

    protected PlayerMovement2D movement;
    private float cooldownTimer;

    /// <summary>Whether this action is currently unlocked/collected. Toggle this on pickup.</summary>
    public bool IsUnlocked { get; set; } = true;

    protected virtual void Awake()
    {
        movement = GetComponent<PlayerMovement2D>();
    }

    protected virtual void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (!movement.AcceptsInput) return;
        if (!IsUnlocked) return;
        if (cooldownTimer > 0f) return;
        if (!Input.GetKeyDown(inputKey)) return;

        TryTrigger();
    }

    private void TryTrigger()
    {
        if (!CanExecute()) return;

        // This is the ONE place a jump/dash/etc. touches the shared counter.
        if (!moveBudget.TrySpend(cost))
        {
            OnDepleted(); // e.g. play the "buzzer" / trigger death — override if needed
            return;
        }

        cooldownTimer = cooldown;
        Execute();

        // One hook here covers Jump, Dash and every future action — no action subclass needs audio code.
        AudioManager.Play(performSound);
    }

    /// <summary>Return false to silently block the action (e.g. dash only while airborne).</summary>
    protected abstract bool CanExecute();

    /// <summary>The actual gameplay effect — apply velocity, break a wall, etc.</summary>
    protected abstract void Execute();

    /// <summary>Called when the player tried an action without enough moves to pay its cost.</summary>
    protected virtual void OnDepleted()
    {
        movement.Die();
    }
}
