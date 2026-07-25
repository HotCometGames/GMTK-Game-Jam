using System;
using UnityEngine;

/// <summary>
/// Turns event-channel raises into sounds. This exists so no gameplay script has to know audio
/// is installed at all — GameManager, LevelExitTrigger and PlayerDeathTrigger stay untouched and
/// just keep raising the channels they already raise.
///
/// TO ADD A NEW SOUND CUE:
///   Add a row to Void Bindings (or Level Bindings) and drop in the channel asset + sound event.
///   No code change needed.
///
/// Lives on the AudioManager GameObject in the boot scene, so it persists across scene loads.
/// </summary>
[RequireComponent(typeof(AudioManager))]
public class GameAudioBinder : MonoBehaviour
{
    [Serializable]
    private class VoidBinding
    {
        [Tooltip("e.g. OnRoundComplete, OnPlayerDied, OnStartRunRequested")]
        public VoidEventChannelSO channel;
        public SoundEventSO sound;

        [NonSerialized] public Action Handler;
    }

    [Serializable]
    private class LevelBinding
    {
        [Tooltip("e.g. OnLevelLoaded")]
        public LevelEventChannelSO channel;
        public SoundEventSO sound;

        [NonSerialized] public Action<LevelDataSO> Handler;
    }

    [SerializeField] private VoidBinding[] voidBindings;
    [SerializeField] private LevelBinding[] levelBindings;

    private void OnEnable()
    {
        if (voidBindings != null)
        {
            foreach (var binding in voidBindings)
            {
                if (binding?.channel == null) continue;

                // Cache the delegate so OnDisable unsubscribes the exact same instance —
                // a fresh lambda each time would silently leak subscriptions across scene loads.
                var sound = binding.sound;
                binding.Handler = () => AudioManager.Play(sound);
                binding.channel.OnEventRaised += binding.Handler;
            }
        }

        if (levelBindings == null) return;
        foreach (var binding in levelBindings)
        {
            if (binding?.channel == null) continue;

            var sound = binding.sound;
            binding.Handler = _ => AudioManager.Play(sound);
            binding.channel.OnEventRaised += binding.Handler;
        }
    }

    private void OnDisable()
    {
        if (voidBindings != null)
        {
            foreach (var binding in voidBindings)
            {
                if (binding?.channel == null || binding.Handler == null) continue;
                binding.channel.OnEventRaised -= binding.Handler;
                binding.Handler = null;
            }
        }

        if (levelBindings == null) return;
        foreach (var binding in levelBindings)
        {
            if (binding?.channel == null || binding.Handler == null) continue;
            binding.channel.OnEventRaised -= binding.Handler;
            binding.Handler = null;
        }
    }
}
