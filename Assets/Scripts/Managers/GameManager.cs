using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns run flow: which level is active, what happens on round-complete, and what happens
/// on player death. This is the only script allowed to call MoveBudgetSO.ResetBudget() and
/// the only one that loads scenes. It never references the player, HUD, or any action script
/// directly — it only listens to and raises event channels.
///
/// Put this on a GameObject in your first/boot scene. It persists itself across scene loads.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Debug Mode")]
    [SerializeField] private bool debugMode = false;
    [Header("Run Data")]
    [SerializeField] private LevelSequenceSO levelSequence;
    [SerializeField] private MoveBudgetSO moveBudget;
    [SerializeField] private SceneFader sceneFader;

    [Header("Listens To")]
    [SerializeField] private VoidEventChannelSO onRoundComplete;   // raised by LevelExitTrigger
    [SerializeField] private VoidEventChannelSO onPlayerDied;      // raised by PlayerDeathTrigger
    [SerializeField] private VoidEventChannelSO onStartRunRequested; // raised by the title screen's Play button

    [Header("Broadcasts")]
    [SerializeField] private LevelEventChannelSO onLevelLoaded;    // level just finished loading
    [SerializeField] private VoidEventChannelSO onRunComplete;     // last level's exit was reached

    private int currentLevelIndex;
    private bool isLoading;

    private void Awake()
    {
        // Simple persistent-singleton guard so re-entering the boot scene doesn't duplicate this.
        var existing = FindObjectsByType<GameManager>(FindObjectsSortMode.None);
        if (existing.Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // Debug hotkeys for testing
        if (Input.GetKeyDown(KeyCode.R))
            LoadLevel(currentLevelIndex);
        if (Input.GetKeyDown(KeyCode.N) && debugMode)
            LoadLevel(currentLevelIndex + 1);
    }

    private void OnEnable()
    {
        if (onRoundComplete != null) onRoundComplete.OnEventRaised += HandleRoundComplete;
        if (onPlayerDied != null) onPlayerDied.OnEventRaised += HandlePlayerDied;
        if (onStartRunRequested != null) onStartRunRequested.OnEventRaised += HandleStartRun;
    }

    private void OnDisable()
    {
        if (onRoundComplete != null) onRoundComplete.OnEventRaised -= HandleRoundComplete;
        if (onPlayerDied != null) onPlayerDied.OnEventRaised -= HandlePlayerDied;
        if (onStartRunRequested != null) onStartRunRequested.OnEventRaised -= HandleStartRun;
    }

    private void HandleStartRun()
    {
        LoadLevel(0);
    }

    // Waits at the title screen — LoadLevel(0) only runs once HandleStartRun() fires.

    private void HandleRoundComplete()
    {
        int nextIndex = currentLevelIndex + 1;

        if (nextIndex >= levelSequence.levels.Count)
        {
            onRunComplete?.Raise();
            return;
        }

        LoadLevel(nextIndex);
    }

    private void HandlePlayerDied()
    {
        LoadLevel(currentLevelIndex);
    }

    private void LoadLevel(int index)
    {
        if (isLoading) return;
        if (index < 0 || index >= levelSequence.levels.Count)
        {
            Debug.LogError($"GameManager: level index {index} out of range.");
            return;
        }

        currentLevelIndex = index;
        StartCoroutine(LoadLevelRoutine(levelSequence.levels[index]));
    }

    private IEnumerator LoadLevelRoutine(LevelDataSO levelData)
    {
        isLoading = true;

        if (sceneFader != null)
            yield return StartCoroutine(sceneFader.FadeOut());

        AsyncOperation op = SceneManager.LoadSceneAsync(levelData.sceneName, LoadSceneMode.Single);
        while (op != null && !op.isDone)
            yield return null;

        moveBudget.ResetBudget(levelData.startingMoves);
        onLevelLoaded?.Raise(levelData);

        if (sceneFader != null)
            yield return StartCoroutine(sceneFader.FadeIn());

        isLoading = false;
    }
}