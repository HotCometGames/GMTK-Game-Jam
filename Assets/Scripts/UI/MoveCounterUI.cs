using TMPro;
using UnityEngine;

/// <summary>
/// Updates a text field to show moves remaining. Only listens to the IntEventChannelSO —
/// never references MoveBudgetSO, JumpAction, or DashAction directly. That means the
/// UI Programmer can build and test this before gameplay code even exists, by manually
/// calling onMovesChanged.Raise(3) from a debug button.
/// </summary>
public class MoveCounterUI : MonoBehaviour
{
    [Header("Event Channels")]
    [SerializeField] private IntEventChannelSO onMovesChanged;

    [Header("Initial Value")]
    [Tooltip("Only read once on Start() to show the correct count before the first event fires. " +
             "Not required — the counter still works from events alone if left empty.")]
    [SerializeField] private MoveBudgetSO moveBudget;

    [Header("UI")]
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private string format = "Moves: {0}";

    [Header("Feel the Squeeze (optional)")]
    [SerializeField] private int lowMovesThreshold = 1;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowMovesColor = Color.red;

    private void Start()
    {
        if (moveBudget != null)
            UpdateCounter(moveBudget.CurrentMoves);
    }

    private void OnEnable()
    {
        if (onMovesChanged != null)
            onMovesChanged.OnEventRaised += UpdateCounter;
    }

    private void OnDisable()
    {
        if (onMovesChanged != null)
            onMovesChanged.OnEventRaised -= UpdateCounter;
    }

    private void UpdateCounter(int movesRemaining)
    {
        if (counterText == null) return;

        counterText.text = string.Format(format, movesRemaining);
        counterText.color = movesRemaining <= lowMovesThreshold ? lowMovesColor : normalColor;
    }
}