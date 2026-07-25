using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// The only thing in the project that owns an AudioSource. Builds three pools of 2D voices in
/// Awake() and hands them out round-robin, so nothing else ever has to think about voice limits.
///
/// The Crisp and Cushion pools carry an AudioHighPassFilter / AudioLowPassFilter respectively —
/// that opposite-direction EQ split (see AudioBus) is what makes the stock Kenney clips read as a
/// deliberate pop-vs-cushion pair rather than "different sounds happened to get picked".
///
/// It also owns the single music voice, which cycles a shuffled playlist so a long play session
/// never hears the same track twice in a row.
///
/// All SFX voices route to the mixer's SFX group and the music voice to its Music group, so the
/// two settings sliders can attenuate them independently (see AudioVolume).
///
/// Lives on the same GameObject as GameAudioBinder in the boot scene (TitleScreen) and persists
/// across scene loads, exactly like GameManager.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer Routing")]
    [Tooltip("Assets/Audio/GameMixer.mixer — carries the MusicVolume / SFXVolume exposed params.")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioMixerGroup musicGroup;

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

    [Header("Music")]
    [Tooltip("Cycled in shuffled order — every track plays once before any repeats, and a track never follows itself.")]
    [SerializeField] private AudioClip[] musicPlaylist;
    [Tooltip("Music sits UNDER the SFX by design. This is the ceiling the Music slider scales from, so " +
             "even at 100% music the jump/land/level-advance pops stay clearly on top. Lower it if music still crowds them.")]
    [Range(0f, 1f)][SerializeField] private float musicTrim = 0.35f;
    [Tooltip("Seconds of silence between tracks.")]
    [SerializeField] private float gapBetweenTracks = 1.5f;
    [Tooltip("Fade applied at the start and end of each track so tracks don't cut in/out abruptly.")]
    [SerializeField] private float musicFadeDuration = 1.5f;

    [Header("Sound Catalog")]
    // Optional convenience layer. The PRIMARY wiring is still per-component (JumpAction.performSound,
    // PlayerMovement2D.landingSound, GameAudioBinder rows) — that's what actually fires in game today.
    // These slots only back the named PlayJump()/PlayLand()/... helpers for code that would rather
    // call a method than hold a SoundEventSO reference.
    [SerializeField] private SoundEventSO jumpSound;
    [SerializeField] private SoundEventSO landSound;
    [SerializeField] private SoundEventSO dashSound;
    [SerializeField] private SoundEventSO levelAdvanceSound;
    [SerializeField] private SoundEventSO levelIntroSound;

    private AudioSource[] crispVoices;
    private AudioSource[] cushionVoices;
    private AudioSource[] rawVoices;

    private int crispCursor;
    private int cushionCursor;
    private int rawCursor;

    private AudioSource musicSource;
    private int[] musicOrder;      // shuffled index list into musicPlaylist
    private int musicOrderCursor;
    private Coroutine musicRoutine;

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

        BuildMusicSource();

        // Push saved slider values into the mixer before the first frame, so the game never
        // audibly starts at full volume and then snaps down once a menu is opened.
        AudioVolume.ApplySaved(mixer);
    }

    private void Start()
    {
        StartMusic();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---- Music ----

    private void BuildMusicSource()
    {
        var host = new GameObject("Music Voice");
        host.transform.SetParent(transform, false);

        musicSource = host.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = false;          // the playlist does the looping, not the clip
        musicSource.spatialBlend = 0f;
        musicSource.bypassReverbZones = true;
        musicSource.outputAudioMixerGroup = musicGroup;
        musicSource.volume = musicTrim;
    }

    /// <summary>Begin (or restart) the shuffled playlist. Safe to call with an empty playlist.</summary>
    public void StartMusic()
    {
        if (musicSource == null || musicPlaylist == null || musicPlaylist.Length == 0) return;

        if (musicRoutine != null) StopCoroutine(musicRoutine);
        ReshuffleMusicOrder();
        musicRoutine = StartCoroutine(MusicRoutine());
    }

    public void StopMusic()
    {
        if (musicRoutine != null) { StopCoroutine(musicRoutine); musicRoutine = null; }
        if (musicSource != null) musicSource.Stop();
    }

    private System.Collections.IEnumerator MusicRoutine()
    {
        while (true)
        {
            AudioClip clip = NextMusicClip();
            if (clip == null) yield break;

            musicSource.clip = clip;
            musicSource.volume = 0f; // silence BEFORE Play, or the first frame leaks out at full volume
            musicSource.Play();

            // Fade in, hold, then fade out just before the clip ends.
            float fade = Mathf.Min(musicFadeDuration, clip.length * 0.25f);
            yield return StartCoroutine(FadeMusic(0f, musicTrim, fade));

            float holdUntil = Mathf.Max(0f, clip.length - fade * 2f);
            while (musicSource.isPlaying && musicSource.time < holdUntil)
                yield return null;

            yield return StartCoroutine(FadeMusic(musicTrim, 0f, fade));

            musicSource.Stop();
            yield return new WaitForSeconds(gapBetweenTracks);
        }
    }

    private System.Collections.IEnumerator FadeMusic(float from, float to, float duration)
    {
        if (duration <= 0f) { musicSource.volume = to; yield break; }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Unscaled: music must keep fading correctly while the game is paused or slowed.
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        musicSource.volume = to;
    }

    /// <summary>Walks the shuffled order, reshuffling once every track has had a turn.</summary>
    private AudioClip NextMusicClip()
    {
        if (musicPlaylist.Length == 0) return null;
        if (musicOrder == null || musicOrderCursor >= musicOrder.Length) ReshuffleMusicOrder();

        return musicPlaylist[musicOrder[musicOrderCursor++]];
    }

    private void ReshuffleMusicOrder()
    {
        int lastPlayed = (musicOrder != null && musicOrder.Length > 0) ? musicOrder[musicOrder.Length - 1] : -1;

        musicOrder = new int[musicPlaylist.Length];
        for (int i = 0; i < musicOrder.Length; i++) musicOrder[i] = i;

        for (int i = musicOrder.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (musicOrder[i], musicOrder[j]) = (musicOrder[j], musicOrder[i]);
        }

        // A fresh shuffle can start with the track the previous pass ended on, which is the one
        // repeat the shuffle was supposed to prevent. Swap it away when there's another choice.
        if (musicOrder.Length > 1 && musicOrder[0] == lastPlayed)
            (musicOrder[0], musicOrder[musicOrder.Length - 1]) = (musicOrder[musicOrder.Length - 1], musicOrder[0]);

        musicOrderCursor = 0;
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
            source.outputAudioMixerGroup = sfxGroup;
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

    // ---- Named helpers ----
    // Thin sugar over Play(). Everything already firing in game goes through the Inspector-assigned
    // references on JumpAction / PlayerMovement2D / GameAudioBinder; these exist for code that would
    // rather not hold a SoundEventSO. Fill the matching Sound Catalog slot for one to make noise.

    public static void PlayJump() => Play(Instance != null ? Instance.jumpSound : null);
    public static void PlayLand() => Play(Instance != null ? Instance.landSound : null);
    public static void PlayDash() => Play(Instance != null ? Instance.dashSound : null);
    public static void PlayLevelAdvance() => Play(Instance != null ? Instance.levelAdvanceSound : null);
    public static void PlayLevelIntro() => Play(Instance != null ? Instance.levelIntroSound : null);

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
