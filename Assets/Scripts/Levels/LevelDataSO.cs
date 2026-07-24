using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One asset per level. The Level Designer builds and rebalances levels entirely through
/// these assets — no code changes needed to add a level, change its par, or change which
/// abilities are available in it.
/// </summary>
[CreateAssetMenu(fileName = "Level_", menuName = "Game/Level Data")]
public class LevelDataSO : ScriptableObject
{
    [Header("Identity")]
    public string levelName;

    [Tooltip("Must exactly match the scene name in File > Build Settings.")]
    public string sceneName;

    [Header("Scoring")]
    [Tooltip("The 'optimal' move count this level is tuned around, for par/replay scoring.")]
    public int parMoveCount;

    [Header("Ability Gating")]
    [Tooltip("Names of the MoveActionBase subclasses unlocked in this level, " +
             "e.g. 'JumpAction', 'DashAction'. Leave empty to allow whatever is already unlocked.")]
    public List<string> unlockedActionNames = new List<string>();
}