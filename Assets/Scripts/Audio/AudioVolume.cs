using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// The one place that knows how a 0..1 settings slider becomes a mixer attenuation, and where
/// that choice is saved. Both the settings UI and AudioManager go through here, so a volume can
/// never be applied without also being persisted (or vice versa).
///
/// TO ADD A NEW VOLUME CHANNEL:
///   1. Expose the group's volume on GameMixer.mixer as "<Name>Volume".
///   2. Add a key + param constant below and a Get/Set pair alongside the existing ones.
///   3. Add its slider to AudioSettingsUI.
/// </summary>
public static class AudioVolume
{
    // Exposed parameter names on Assets/Audio/GameMixer.mixer.
    public const string MusicParam = "MusicVolume";
    public const string SfxParam = "SFXVolume";

    private const string MusicPrefKey = "Audio.MusicVolume";
    private const string SfxPrefKey = "Audio.SFXVolume";

    // Music defaults lower than SFX so a fresh install already has the music sitting under the
    // jump/land/level-advance pops rather than competing with them.
    public const float MusicDefault = 0.6f;
    public const float SfxDefault = 1f;

    /// <summary>Below this a slider counts as "off" and the group is muted outright (-80dB).</summary>
    private const float SilenceThreshold = 0.0001f;

    public static float GetMusic() => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicPrefKey, MusicDefault));
    public static float GetSfx() => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxPrefKey, SfxDefault));

    public static void SetMusic(AudioMixer mixer, float value01)
    {
        value01 = Mathf.Clamp01(value01);
        PlayerPrefs.SetFloat(MusicPrefKey, value01);
        PlayerPrefs.Save();
        Apply(mixer, MusicParam, value01);
    }

    public static void SetSfx(AudioMixer mixer, float value01)
    {
        value01 = Mathf.Clamp01(value01);
        PlayerPrefs.SetFloat(SfxPrefKey, value01);
        PlayerPrefs.Save();
        Apply(mixer, SfxParam, value01);
    }

    /// <summary>Push whatever is saved into the mixer. Called once at boot by AudioManager.</summary>
    public static void ApplySaved(AudioMixer mixer)
    {
        Apply(mixer, MusicParam, GetMusic());
        Apply(mixer, SfxParam, GetSfx());
    }

    private static void Apply(AudioMixer mixer, string param, float value01)
    {
        if (mixer == null) return;
        mixer.SetFloat(param, ToDecibels(value01));
    }

    /// <summary>
    /// Linear slider position to mixer decibels. A slider is perceived logarithmically, so a plain
    /// linear-to-dB map would make the top of the travel do almost nothing and the bottom do
    /// everything — Log10 * 20 is what makes the handle feel evenly spread across its range.
    /// </summary>
    public static float ToDecibels(float value01)
    {
        if (value01 <= SilenceThreshold) return -80f;
        return Mathf.Log10(value01) * 20f;
    }
}
