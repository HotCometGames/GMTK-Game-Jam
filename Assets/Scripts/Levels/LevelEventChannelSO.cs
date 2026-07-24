using System;
using UnityEngine;

/// <summary>
/// Event channel that carries a LevelDataSO reference. Used for "a level just loaded" —
/// anything that needs to react to which level is now active (HUD level name, ability
/// unlocks, par display) subscribes here instead of referencing GameManager directly.
/// </summary>
[CreateAssetMenu(fileName = "LevelEventChannel", menuName = "Game/Events/Level Event Channel")]
public class LevelEventChannelSO : ScriptableObject
{
    public event Action<LevelDataSO> OnEventRaised;

    public void Raise(LevelDataSO level) => OnEventRaised?.Invoke(level);
}