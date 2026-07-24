using UnityEngine;

/// <summary>
/// Drop this on any trigger collider that should kill the player on contact — the void
/// below the level, spikes, etc. Same pattern as LevelExitTrigger: raises an event,
/// doesn't know or care who's listening.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerDeathTrigger : MonoBehaviour
{
    [SerializeField] private VoidEventChannelSO onPlayerDied;
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        onPlayerDied?.Raise();
    }
}