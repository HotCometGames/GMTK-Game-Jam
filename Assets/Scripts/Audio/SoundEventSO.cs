using UnityEngine;

/// <summary>
/// One authored sound "event" (SFX_Jump, SFX_Land, ...). Gameplay code never references an
/// AudioClip directly — it references one of these and calls AudioManager.Play(evt), so the
/// clip choice, pitch, volume and EQ routing are all tuned in the Inspector without recompiling.
///
/// TO ADD A NEW SOUND:
///   1. Assets > Create > Game > Audio > Sound Event.
///   2. Drop one or more clips into Variants (they're picked at random, never twice in a row).
///   3. Pick the bus — Crisp for pops, Cushion for impacts.
///   4. Drag the asset into whichever component/binder slot should fire it.
/// </summary>
[CreateAssetMenu(fileName = "SFX_New", menuName = "Game/Audio/Sound Event")]
public class SoundEventSO : ScriptableObject
{
    [Header("Clips")]
    [Tooltip("One is picked at random each play. Drop in a whole Kenney _000.._004 set to avoid repeat fatigue.")]
    [SerializeField] private AudioClip[] variants;

    [Tooltip("Optional clips fired at the same time as the variant — e.g. a whoosh layered under a dash click.")]
    [SerializeField] private AudioClip[] layers;

    [Header("Mix")]
    [SerializeField] private AudioBus bus = AudioBus.Crisp;
    [Tooltip("Per-event trim. Above 1 is allowed — quiet source clips need it, especially on the Cushion bus.")]
    [Range(0f, 2f)][SerializeField] private float volume = 1f;
    [Range(0f, 2f)][SerializeField] private float layerVolume = 0.7f;

    [Header("Pitch")]
    [Tooltip("Randomised per play. Pitch UP (>1) makes a pop feel lighter; DOWN (<1) makes it feel heavier.")]
    [Range(0.25f, 3f)][SerializeField] private float pitchMin = 1f;
    [Range(0.25f, 3f)][SerializeField] private float pitchMax = 1f;

    [Header("Retrigger Guard")]
    [Tooltip("Plays within this many seconds of the last one are swallowed, so a bumpy landing can't machine-gun.")]
    [SerializeField] private float retriggerCooldown = 0.05f;

    public AudioBus Bus => bus;
    public AudioClip[] Layers => layers;
    public float Volume => volume;
    public float LayerVolume => layerVolume;

    private int lastVariantIndex = -1;
    private float lastPlayTime = -999f;

    /// <summary>False if this event fired too recently to play again. Also arms the guard.</summary>
    public bool TryConsumeCooldown()
    {
        // Unscaled: sounds should still be rate-limited correctly if the game is ever slowed/paused.
        float now = Time.unscaledTime;
        if (now - lastPlayTime < retriggerCooldown) return false;
        lastPlayTime = now;
        return true;
    }

    /// <summary>Random clip, never the same one twice in a row when there's more than one to choose from.</summary>
    public AudioClip PickVariant()
    {
        if (variants == null || variants.Length == 0) return null;
        if (variants.Length == 1) return variants[0];

        int index = Random.Range(0, variants.Length);
        if (index == lastVariantIndex)
            index = (index + 1) % variants.Length;

        lastVariantIndex = index;
        return variants[index];
    }

    public float PickPitch() => Random.Range(pitchMin, pitchMax);

    private void OnValidate()
    {
        if (pitchMax < pitchMin) pitchMax = pitchMin;
    }

    // ScriptableObject state survives play-mode exits in the Editor; clear the guards so the
    // first sound of a fresh session is never swallowed by a stale timestamp.
    private void OnEnable()
    {
        lastVariantIndex = -1;
        lastPlayTime = -999f;
    }
}
