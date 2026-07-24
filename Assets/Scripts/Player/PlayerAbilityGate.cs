using UnityEngine;

/// <summary>
/// Lives on the Player alongside JumpAction, DashAction, etc. Listens for onLevelLoaded and
/// sets each MoveActionBase's IsUnlocked flag based on LevelDataSO.unlockedActionNames.
///
/// This keeps GameManager from ever needing a direct reference to the player or its action
/// components — it only broadcasts "this level just loaded," and the player decides what to
/// do with that information.
/// </summary>
public class PlayerAbilityGate : MonoBehaviour
{
    [SerializeField] private LevelEventChannelSO onLevelLoaded;

    private MoveActionBase[] actions;

    private void Awake()
    {
        actions = GetComponents<MoveActionBase>();
    }

    private void OnEnable()
    {
        if (onLevelLoaded != null) onLevelLoaded.OnEventRaised += HandleLevelLoaded;
    }

    private void OnDisable()
    {
        if (onLevelLoaded != null) onLevelLoaded.OnEventRaised -= HandleLevelLoaded;
    }

    private void HandleLevelLoaded(LevelDataSO level)
    {
        foreach (var action in actions)
        {
            action.IsUnlocked = level.unlockedActionNames.Contains(action.GetType().Name);
        }
    }
}