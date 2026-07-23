using System;
using UnityEngine;

/// <summary>
/// Reusable event channel with no payload (e.g. OnPlayerDied, OnRoundComplete).
/// </summary>
[CreateAssetMenu(fileName = "VoidEventChannel", menuName = "Game/Events/Void Event Channel")]
public class VoidEventChannelSO : ScriptableObject
{
    public event Action OnEventRaised;

    public void Raise() => OnEventRaised?.Invoke();
}