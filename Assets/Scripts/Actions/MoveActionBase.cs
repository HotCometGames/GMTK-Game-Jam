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
    }

    /// <summary>Return false to silently block the action (e.g. dash only while airborne).</summary>
    protected abstract bool CanExecute();

    /// <summary>The actual gameplay effect — apply velocity, break a wall, etc.</summary>
    protected abstract void Execute();

    /// <summary>Called when the player tried this action with zero moves left. Default: does nothing extra,
    /// MoveBudgetSO.onMovesDepleted already fired for RunController/UI to react to (e.g. trigger death).</summary>
    protected virtual void OnDepleted() { }
}