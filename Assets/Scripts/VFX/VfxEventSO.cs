using UnityEngine;

/// <summary>
/// One authored visual "event" (VFX_Jump, VFX_Land, ...). This is the visual twin of SoundEventSO:
/// gameplay code never touches a ParticleSystem directly — it references one of these and calls
/// VfxManager.Play(evt, position, direction), so burst size, colour, speed and spread are all tuned
/// in the Inspector without recompiling.
///
/// TO ADD A NEW EFFECT:
///   1. Assets > Create > Game > VFX > Vfx Event.
///   2. Leave Prefab empty to use the manager's default burst prefab, or drop in your own.
///   3. Tune the burst — count, speed, size, cone angle, colour.
///   4. Drag the asset into whichever component slot should fire it (e.g. JumpAction.performVfx).
/// </summary>
[CreateAssetMenu(fileName = "VFX_New", menuName = "Game/VFX/Vfx Event")]
public class VfxEventSO : ScriptableObject
{
    [Header("Source")]
    [Tooltip("Leave empty to use VfxManager's default burst prefab. Only override for a genuinely " +
             "different system (e.g. one with a custom texture sheet or noise module).")]
    [SerializeField] private ParticleSystem prefab;

    [Header("Burst")]
    [Tooltip("Particles emitted per play, picked at random in this range.")]
    [SerializeField] private int countMin = 6;
    [SerializeField] private int countMax = 10;

    [Header("Motion")]
    [SerializeField] private float speedMin = 1.5f;
    [SerializeField] private float speedMax = 3f;
    [Tooltip("Seconds each particle lives. Short (~0.3s) reads as a snappy pen tick; long reads as smoke.")]
    [SerializeField] private float lifetime = 0.35f;
    [Tooltip("Half-angle of the emission cone in degrees. 180 = full circle, 25 = a tight directional spray.")]
    [Range(0f, 180f)][SerializeField] private float coneAngle = 45f;
    [Tooltip("Rotate the cone to point along the direction passed to Play(). Off = always emit upward.")]
    [SerializeField] private bool alignToDirection = true;
    [Tooltip("Gravity pull on the particles. Slight positive makes dust settle; 0 makes sparks float.")]
    [SerializeField] private float gravity = 0.4f;

    [Header("Look")]
    // Default is the project's sketch orange (#EF722E) — the same value as GrappleLine.mat.
    [SerializeField] private Color tint = new Color(0.937255f, 0.447059f, 0.180392f, 1f);
    [Tooltip("World units. The player is 0.39 x 0.61 and one pen stroke is ~0.04, so keep these small.")]
    [SerializeField] private float sizeMin = 0.06f;
    [SerializeField] private float sizeMax = 0.14f;
    [Tooltip("Degrees per second of spin. A little rotation stops repeated strokes looking stamped.")]
    [SerializeField] private float rotationSpeed = 90f;

    [Header("Retrigger Guard")]
    [Tooltip("Plays within this many seconds of the last one are swallowed, so a bumpy landing can't machine-gun.")]
    [SerializeField] private float retriggerCooldown = 0.05f;

    public ParticleSystem Prefab => prefab;
    public float Lifetime => lifetime;
    public float ConeAngle => coneAngle;
    public bool AlignToDirection => alignToDirection;
    public float Gravity => gravity;
    public Color Tint => tint;
    public float SizeMin => sizeMin;
    public float SizeMax => sizeMax;
    public float RotationSpeed => rotationSpeed;
    public float SpeedMin => speedMin;
    public float SpeedMax => speedMax;

    private float lastPlayTime = -999f;

    /// <summary>False if this event fired too recently to play again. Also arms the guard.</summary>
    public bool TryConsumeCooldown()
    {
        // Unscaled: effects should still be rate-limited correctly if the game is ever slowed/paused.
        float now = Time.unscaledTime;
        if (now - lastPlayTime < retriggerCooldown) return false;
        lastPlayTime = now;
        return true;
    }

    /// <summary>Particles to emit this play. <paramref name="intensity"/> scales the count (1 = as authored).</summary>
    public int PickCount(float intensity = 1f)
    {
        int count = Random.Range(countMin, countMax + 1);
        return Mathf.Max(1, Mathf.RoundToInt(count * Mathf.Max(0f, intensity)));
    }

    private void OnValidate()
    {
        if (countMax < countMin) countMax = countMin;
        if (speedMax < speedMin) speedMax = speedMin;
        if (sizeMax < sizeMin) sizeMax = sizeMin;
    }

    // ScriptableObject state survives play-mode exits in the Editor; clear the guard so the
    // first effect of a fresh session is never swallowed by a stale timestamp.
    private void OnEnable()
    {
        lastPlayTime = -999f;
    }
}
