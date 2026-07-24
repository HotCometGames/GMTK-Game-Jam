using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ordered sequence of levels for a full run. Reorder, add, or remove levels here —
/// GameManager just walks this list, it never hardcodes level order.
/// </summary>
[CreateAssetMenu(fileName = "LevelSequence", menuName = "Game/Level Sequence")]
public class LevelSequenceSO : ScriptableObject
{
    public List<LevelDataSO> levels = new List<LevelDataSO>();
}