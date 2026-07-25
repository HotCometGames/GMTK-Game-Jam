using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Binds the Settings menu's Music / Effect sliders to the mixer. Lives on the Settings Menu
/// object, so it wakes up with the panel and re-reads the saved values every time it's opened.
///
/// The sliders themselves are authored in the scene (0..1, matching styling) — this script only
/// reads and writes them, so restyling a slider never means touching code.
/// </summary>
public class AudioSettingsUI : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider effectSlider;

    private void OnEnable()
    {
        // Seed the handles from the saved values WITHOUT notifying — a plain `.value = x` fires
        // onValueChanged, which would write the value straight back and, worse, re-save the
        // default over a real setting on the very first open.
        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(AudioVolume.GetMusic());
            musicSlider.onValueChanged.AddListener(HandleMusicChanged);
        }

        if (effectSlider != null)
        {
            effectSlider.SetValueWithoutNotify(AudioVolume.GetSfx());
            effectSlider.onValueChanged.AddListener(HandleEffectChanged);
        }

        // The panel may be opened before anything has pushed the saved values into the mixer
        // (e.g. entering the title scene directly from the Editor), so make sure they're live.
        AudioVolume.ApplySaved(mixer);
    }

    private void OnDisable()
    {
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(HandleMusicChanged);
        if (effectSlider != null) effectSlider.onValueChanged.RemoveListener(HandleEffectChanged);
    }

    private void HandleMusicChanged(float value) => AudioVolume.SetMusic(mixer, value);

    private void HandleEffectChanged(float value) => AudioVolume.SetSfx(mixer, value);
}
