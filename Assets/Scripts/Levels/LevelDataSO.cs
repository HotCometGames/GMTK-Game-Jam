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

    [Header("Move Budget")]
    [Tooltip("How many moves the player gets when this level loads. The shared budget resets " +
             "to this value on every load of this level.")]
    public int startingMoves = 4;

    [Header("Ability Gating")]
    [Tooltip("Names of the MoveActionBase subclasses unlocked in this level, " +
             "e.g. 'JumpAction', 'DashAction'. This is re-applied on every level load, " +
             "so list every action that should be active here - leaving it empty locks everything.")]
    public List<string> unlockedActionNames = new List<string>();
}