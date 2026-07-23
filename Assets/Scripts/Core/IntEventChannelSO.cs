using System;
using UnityEngine;

/// <summary>
/// Reusable event channel that carries an int payload (e.g. moves remaining).
/// UI/Audio subscribe via OnEventRaised without ever referencing gameplay scripts.
/// </summary>
[CreateAssetMenu(fileName = "IntEventChannel", menuName = "Game/Events/Int Event Channel")]
public class IntEventChannelSO : ScriptableObject
{
    public event Action<int> OnEventRaised;

    public void Raise(int value) => OnEventRaised?.Invoke(value);
}