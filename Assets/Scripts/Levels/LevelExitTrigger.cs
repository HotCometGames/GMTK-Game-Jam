using UnityEngine;

/// <summary>
/// Drop this on a trigger collider at the exit of each level. It doesn't know GameManager
/// exists — it just raises an event. GameManager (or anything else) can subscribe.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LevelExitTrigger : MonoBehaviour
{
    [SerializeField] private VoidEventChannelSO onRoundComplete;
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"LevelExitTrigger: OnTriggerEnter2D with {other.name}");
        if (!other.CompareTag(playerTag)) return;
        onRoundComplete?.Raise();
    }
}