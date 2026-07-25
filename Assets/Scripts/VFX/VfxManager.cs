using UnityEngine;

/// <summary>
/// The only thing in the project that owns a ParticleSystem for one-shot effects. Builds a pool of
/// emitters in Awake() and hands them out round-robin, exactly like AudioManager does with voices,
/// so nothing else ever has to think about instantiating or cleaning up particles.
///
/// Emitters are built in code rather than authored as prefabs: every per-effect difference
/// (count, speed, size, colour, spread) is carried on the VfxEventSO and applied per-particle via
/// EmitParams, so one generic emitter covers every effect and there is no prefab to keep in sync.
///
/// Pooled emitters sit at the world root and are positioned per shot. They are deliberately NEVER
/// parented to the player: PlayerMovement2D writes transform.localScale.x = FacingDirection every
/// frame, which would mirror any child particle system the instant the player turns around.
///
/// Lives on the same GameObject as AudioManager in the boot scene (TitleScreen) and persists
/// across scene loads, exactly like GameManager.
/// </summary>
public class VfxManager : MonoBehaviour
{
    public static VfxManager Instance { get; private set; }

    [Header("Look")]
    [Tooltip("Assets/Materials/SketchParticle.mat — Sprites/Default, alpha blended. " +
             "Additive is invisible against the near-white grid paper, so don't use it here.")]
    [SerializeField] private Material particleMaterial;

    [Tooltip("Columns/rows in the stroke atlas. Each particle picks one cell at random so repeated " +
             "bursts don't look stamped from the same sprite.")]
    [SerializeField] private int atlasColumns = 4;
    [SerializeField] private int atlasRows = 4;

    [Header("Sorting")]
    // The project has exactly one sorting layer (Default) and the player's SpriteRenderer is order 0,
    // so anything at or below 0 renders behind the player.
    [SerializeField] private string sortingLayer = "Default";
    [SerializeField] private int sortingOrder = 5;

    [Header("Pool")]
    [Tooltip("Emitters in rotation. More = more overlapping bursts before the oldest is reused.")]
    [SerializeField] private int emitterCount = 8;
    [Tooltip("Particle ceiling per emitter.")]
    [SerializeField] private int maxParticlesPerEmitter = 128;

    private ParticleSystem[] emitters;
    private int cursor;

    private void Awake()
    {
        // Same persistent-singleton shape as AudioManager: re-entering the boot scene must not duplicate this.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildPool();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildPool()
    {
        var host = new GameObject("Vfx Emitters");
        host.transform.SetParent(transform, false);

        int count = Mathf.Max(1, emitterCount);
        emitters = new ParticleSystem[count];

        for (int i = 0; i < count; i++)
        {
            var emitterObject = new GameObject($"Emitter {i}");
            emitterObject.transform.SetParent(host.transform, false);
            emitters[i] = ConfigureEmitter(emitterObject.AddComponent<ParticleSystem>());
        }
    }

    /// <summary>
    /// Sets up an emitter that never emits on its own — everything arrives through Emit(EmitParams).
    /// It stays permanently "playing" with a zero emission rate, which is what lets manually emitted
    /// particles simulate; a stopped system would freeze them mid-flight.
    /// </summary>
    private ParticleSystem ConfigureEmitter(ParticleSystem system)
    {
        var main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.maxParticles = Mathf.Max(16, maxParticlesPerEmitter);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startSpeed = 0f;      // per-particle velocity comes from EmitParams
        main.startSize = 0.1f;
        main.startLifetime = 1f;
        main.gravityModifier = 0f; // overridden per shot in Play()

        var emission = system.emission;
        emission.enabled = false;  // nothing is emitted automatically

        var shape = system.shape;
        shape.enabled = false;     // EmitParams carries the position, so no shape needed

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        if (particleMaterial != null) renderer.sharedMaterial = particleMaterial;

        // Each particle grabs one random cell of the stroke atlas and holds it for its whole life,
        // so a burst reads as a scatter of different pen marks rather than one sprite repeated.
        int tilesX = Mathf.Max(1, atlasColumns);
        int tilesY = Mathf.Max(1, atlasRows);
        if (tilesX * tilesY > 1)
        {
            var sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = tilesX;
            sheet.numTilesY = tilesY;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, tilesX * tilesY - 0.001f);
        }

        system.Play();
        return system;
    }

    /// <summary>
    /// Fire a visual event at a world position. Safe to call with null (an unassigned Inspector slot
    /// simply shows nothing) and safe to call when no manager exists.
    /// </summary>
    /// <param name="direction">Which way the burst sprays. Zero means straight up.</param>
    /// <param name="intensity">Scales the particle count — e.g. a harder landing throws more dust.</param>
    public static void Play(VfxEventSO vfxEvent, Vector3 position, Vector2 direction = default, float intensity = 1f)
    {
        if (vfxEvent == null) return;
        if (Instance == null)
        {
            // Playing a level scene directly from the Editor skips TitleScreen, so there's no manager.
            // Show nothing rather than throwing — gameplay must never depend on VFX existing.
            return;
        }

        Instance.PlayInternal(vfxEvent, position, direction, intensity);
    }

    private void PlayInternal(VfxEventSO vfxEvent, Vector3 position, Vector2 direction, float intensity)
    {
        if (!vfxEvent.TryConsumeCooldown()) return;

        ParticleSystem system = NextEmitter(vfxEvent);
        if (system == null) return;

        // If we had to steal a still-playing emitter, clear it so existing particles don't get their
        // module-level settings (e.g. gravityModifier) changed mid-flight.
        if (system.particleCount > 0) system.Clear();

        // gravityModifier is the one property EmitParams can't carry, so it lives on the module.
        var main = system.main;
        main.gravityModifier = vfxEvent.Gravity;

        Vector2 axis = vfxEvent.AlignToDirection && direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.up;

        int count = vfxEvent.PickCount(intensity);
        for (int i = 0; i < count; i++)
        {
            float spread = Random.Range(-vfxEvent.ConeAngle, vfxEvent.ConeAngle);
            Vector2 heading = Rotate(axis, spread);

            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = heading * Random.Range(vfxEvent.SpeedMin, vfxEvent.SpeedMax),
                startLifetime = vfxEvent.Lifetime,
                startSize = Random.Range(vfxEvent.SizeMin, vfxEvent.SizeMax),
                startColor = vfxEvent.Tint,
                rotation = Random.Range(0f, 360f),
                angularVelocity = Random.Range(-vfxEvent.RotationSpeed, vfxEvent.RotationSpeed),
                applyShapeToPosition = false
            };

            system.Emit(emitParams, 1);
        }
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(rad);
        float cos = Mathf.Cos(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    /// <summary>
    /// A VfxEventSO with its own prefab gets a dedicated instance; everything else shares the
    /// generic pool. Prefer an idle emitter, otherwise steal the next one in rotation.
    /// </summary>
    private ParticleSystem NextEmitter(VfxEventSO vfxEvent)
    {
        if (vfxEvent.Prefab != null) return GetOverrideEmitter(vfxEvent);
        if (emitters == null || emitters.Length == 0) return null;

        int chosen = cursor;
        for (int i = 0; i < emitters.Length; i++)
        {
            int index = (cursor + i) % emitters.Length;
            if (emitters[index].particleCount == 0)
            {
                chosen = index;
                break;
            }
        }

        cursor = (chosen + 1) % emitters.Length;
        return emitters[chosen];
    }

    private System.Collections.Generic.Dictionary<VfxEventSO, ParticleSystem> overrideEmitters;

    private ParticleSystem GetOverrideEmitter(VfxEventSO vfxEvent)
    {
        overrideEmitters ??= new System.Collections.Generic.Dictionary<VfxEventSO, ParticleSystem>();

        if (overrideEmitters.TryGetValue(vfxEvent, out var existing) && existing != null)
            return existing;

        var instance = Instantiate(vfxEvent.Prefab, transform);
        instance.name = $"{vfxEvent.name} Emitter";

        // The prefab supplies the look; the manager still owns the "never self-emits" contract.
        var emission = instance.emission;
        emission.enabled = false;
        instance.Play();

        overrideEmitters[vfxEvent] = instance;
        return instance;
    }
}
