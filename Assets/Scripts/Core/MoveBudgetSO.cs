using UnityEngine;

/// <summary>
/// Single source of truth for the shared move counter.
/// Actions call TrySpend() — nobody mutates CurrentMoves directly.
/// </summary>
[CreateAssetMenu(fileName = "MoveBudget", menuName = "Game/Move Budget")]
public class MoveBudgetSO : ScriptableObject
{
    [SerializeField] private int startingMoves = 4;

    public int CurrentMoves { get; private set; }
    public int StartingMoves => startingMoves;

    [Header("Broadcast on change")]
    public IntEventChannelSO onMovesChanged;   // passes remaining moves
    public IntEventChannelSO onMovesDepleted;  // passes the cost that failed
    public VoidEventChannelSO onBudgetReset;

    private void OnEnable()
    {
        CurrentMoves = startingMoves;
    }

    /// <summary>Call this from an action before executing it.</summary>
    public bool TrySpend(int amount)
    {
        if (amount > CurrentMoves)
        {
            onMovesDepleted?.Raise(amount);
            return false;
        }

        CurrentMoves -= amount;
        onMovesChanged?.Raise(CurrentMoves);
        return true;
    }

    /// <summary>Call from RunController on death / new run. Resets to this asset's own default.</summary>
    public void ResetBudget()
    {
        ResetBudget(startingMoves);
    }

    /// <summary>Resets to a specific amount, e.g. a level's own starting move count.</summary>
    public void ResetBudget(int amount)
    {
        CurrentMoves = amount;
        onMovesChanged?.Raise(CurrentMoves);
        onBudgetReset?.Raise();
    }

    /// <summary>Optional: refund moves (e.g. a "cheap route" pickup).</summary>
    public void Refund(int amount)
    {
        CurrentMoves = Mathf.Min(CurrentMoves + amount, startingMoves);
        onMovesChanged?.Raise(CurrentMoves);
    }
}