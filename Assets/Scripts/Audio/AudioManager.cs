using UnityEngine;

/// <summary>
/// The only thing in the project that owns an AudioSource. Builds three pools of 2D voices in
/// Awake() and hands them out round-robin, so nothing else ever has to think about voice limits.
///
/// The Crisp and Cushion pools carry an AudioHighPassFilter / AudioLowPassFilter respectively —
/// that opposite-direction EQ split (see AudioBus) is what makes the stock Kenney clips read as a
/// deliberate pop-vs-cushion pair rather than "different sounds happened to get picked".
///
/// Lives on the same GameObject as GameAudioBinder in the boot scene (TitleScreen) and persists
/// across scene loads, exactly like GameManager.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Voices")]
    [Tooltip("AudioSources per bus. More = more sounds can overlap before the oldest gets stolen.")]
    [SerializeField] private int voicesPerBus = 6;

    [Header("EQ Split")]
    [Tooltip("Crisp bus: roll off everything BELOW this so pops feel light and bright.")]
    [SerializeField] private float crispHighPassHz = 300f;
    [Tooltip("Cushion bus: roll off everything ABOVE this so impacts feel soft and weighted.")]
    [SerializeField] private float cushionLowPassHz = 4500f;

    [Header("Makeup Gain")]
    // Filtering out a band removes that band's energy, so a filtered bus is quieter than an
    // unfiltered one by however much spectrum was cut. These put the loudness back WITHOUT
    // softening the cutoffs — the EQ character stays exactly as designed, it just isn't buried.
    // The crisp bus needs a lot: the Kenney clicks carry most of their energy in the low-mids.
    [Range(1f, 6f)][SerializeField] private float crispMakeupGain = 2.5f;
    [Range(1f, 6f)][SerializeField] private float cushionMakeupGain = 4f;

    [Header("Master")]
    [Range(0f, 1f)][SerializeField] private float masterVolume = 1f;

    private AudioSource[] crispVoices;
    private AudioSource[] cushionVoices;
    private AudioSource[] rawVoices;

    private int crispCursor;
    private int cushionCursor;
    private int rawCursor;

    private void Awake()
    {
        // Same persistent-singleton shape as GameManager: re-entering the boot scene must not duplicate this.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        crispVoices = BuildPool("Crisp Voices", AudioBus.Crisp);
        cushionVoices = BuildPool("Cushion Voices", AudioBus.Cushion);
        rawVoices = BuildPool("Raw Voices", AudioBus.Raw);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private AudioSource[] BuildPool(string poolName, AudioBus bus)
    {
        var host = new GameObject(poolName);
        host.transform.SetParent(transform, false);

        int count = Mathf.Max(1, voicesPerBus);
        var voices = new AudioSource[count];

        for (int i = 0; i < count; i++)
        {
            // One filter per AudioSource: Unity's filter components process whatever the
            // AudioSource on the SAME GameObject outputs, so each voice needs its own object.
            var voiceObject = new GameObject($"Voice {i}");
            voiceObject.transform.SetParent(host.transform, false);

            var source = voiceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // fully 2D — this is a flat platformer, no positional falloff
            source.bypassReverbZones = true;
            voices[i] = source;

            switch (bus)
            {
                case AudioBus.Crisp:
                    voiceObject.AddComponent<AudioHighPassFilter>().cutoffFrequency = crispHighPassHz;
                    break;
                case AudioBus.Cushion:
                    voiceObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = cushionLowPassHz;
                    break;
            }
        }

        return voices;
    }

    /// <summary>Fire a sound event. Safe to call with null (an unassigned Inspector slot is simply silent).</summary>
    public static void Play(SoundEventSO soundEvent)
    {
        if (soundEvent == null) return;
        if (Instance == null)
        {
            // Playing a level scene directly from the Editor skips TitleScreen, so there's no manager.
            // Stay silent rather than throwing — gameplay must never depend on audio existing.
            return;
        }

        Instance.PlayInternal(soundEvent);
    }

    private void PlayInternal(SoundEventSO soundEvent)
    {
        AudioClip clip = soundEvent.PickVariant();
        if (clip == null) return;
        if (!soundEvent.TryConsumeCooldown()) return;

        AudioSource source = NextVoice(soundEvent.Bus);
        source.pitch = soundEvent.PickPitch();

        float gain = MakeupGainFor(soundEvent.Bus) * masterVolume;
        source.PlayOneShot(clip, soundEvent.Volume * gain);

        var layers = soundEvent.Layers;
        if (layers == null) return;

        // Layers ride the same voice so they share its pitch and filter — that's what keeps a
        // dash whoosh glued underneath the click instead of sounding like a second event.
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null)
                source.PlayOneShot(layers[i], soundEvent.LayerVolume * gain);
        }
    }

    private float MakeupGainFor(AudioBus bus)
    {
        switch (bus)
        {
            case AudioBus.Crisp: return crispMakeupGain;
            case AudioBus.Cushion: return cushionMakeupGain;
            default: return 1f;
        }
    }

    /// <summary>Prefer an idle voice; otherwise steal the next one in rotation.</summary>
    private AudioSource NextVoice(AudioBus bus)
    {
        AudioSource[] pool;
        int cursor;

        switch (bus)
        {
            case AudioBus.Cushion: pool = cushionVoices; cursor = cushionCursor; break;
            case AudioBus.Raw: pool = rawVoices; cursor = rawCursor; break;
            default: pool = crispVoices; cursor = crispCursor; break;
        }

        int chosen = cursor;
        for (int i = 0; i < pool.Length; i++)
        {
            int index = (cursor + i) % pool.Length;
            if (!pool[index].isPlaying)
            {
                chosen = index;
                break;
            }
        }

        int nextCursor = (chosen + 1) % pool.Length;
        switch (bus)
        {
            case AudioBus.Cushion: cushionCursor = nextCursor; break;
            case AudioBus.Raw: rawCursor = nextCursor; break;
            default: crispCursor = nextCursor; break;
        }

        return pool[chosen];
    }
}
