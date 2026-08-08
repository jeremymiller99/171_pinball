// Modified with Claude Code (Opus 5) by JJ on 2026-07-26: hold the PowerSurgeManager
// reference so teardown can unsubscribe without re-resolving it.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toggles pre-placed red/blue fire VFX around the board and scales their
/// intensity with the current score multiplier. Red set is the default;
/// blue set takes over while drop-target Power Surge is active.
/// </summary>
public class BoardFireFXController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DropTargetsScoringMode scoringMode;
    [SerializeField] private ScoreManager scoreManager;

    [Header("Fire Sets (pre-placed in scene)")]
    [Tooltip("Red fire GameObjects — active when not in Power Surge.")]
    [SerializeField] private List<GameObject> redFires = new();
    [Tooltip("Blue fire GameObjects — active while Power Surge is running.")]
    [SerializeField] private List<GameObject> blueFires = new();

    [Header("Activation")]
    [Tooltip("Minimum EffectiveMult required before any fire turns on.")]
    [SerializeField] private float multToIgnite = 2f;

    [Header("Intensity Curve")]
    [Tooltip("Mult value that maps to max intensity (1.0 on the scalar).")]
    [SerializeField] private float multForMaxIntensity = 20f;
    [Tooltip("Intensity scalar at ignition (mult == multToIgnite).")]
    [SerializeField, Range(0f, 1f)] private float minIntensity = 0.25f;
    [Tooltip("Emission rate multiplier applied at max intensity.")]
    [SerializeField] private float maxEmissionMultiplier = 3f;
    [Tooltip("Start size multiplier applied at max intensity.")]
    [SerializeField] private float maxSizeMultiplier = 1.5f;

    private readonly List<ParticleSystem> _redParticles = new();
    private readonly List<ParticleSystem> _blueParticles = new();
    private PowerSurgeManager _powerSurgeManager;
    private bool _powerSurgeActive;
    private float _lastAppliedScalar = -1f;

    private void Awake()
    {
        CacheParticles(redFires, _redParticles);
        CacheParticles(blueFires, _blueParticles);
    }

    private void OnEnable()
    {
        _powerSurgeManager = ServiceLocator.Get<PowerSurgeManager>();

        if (_powerSurgeManager != null)
        {
            _powerSurgeManager.OnPowerSurgeActivated += HandlePowerSurgeActivated;
            _powerSurgeManager.OnPowerSurgeDeactivated += HandlePowerSurgeDeactivated;
        }

        ApplySetActive(false);
    }

    // PowerSurgeManager is never registered with the ServiceLocator, so a lookup here falls
    // back to a scene search that returns null once teardown has started. Unsubscribe
    // from the instance resolved in OnEnable instead of resolving it a second time.
    private void OnDisable()
    {
        if (_powerSurgeManager == null)
        {
            return;
        }

        _powerSurgeManager.OnPowerSurgeActivated -= HandlePowerSurgeActivated;
        _powerSurgeManager.OnPowerSurgeDeactivated -= HandlePowerSurgeDeactivated;
        _powerSurgeManager = null;
    }

    private void Start()
    {
        if (scoreManager == null)
            ServiceLocator.TryGet(out scoreManager);
    }

    private void Update()
    {
        if (scoreManager == null) return;

        float mult = scoreManager.EffectiveMult;

        if (mult < multToIgnite)
        {
            if (_lastAppliedScalar != 0f)
            {
                ApplySetActive(false);
                _lastAppliedScalar = 0f;
            }
            return;
        }

        float t = Mathf.InverseLerp(multToIgnite, multForMaxIntensity, mult);
        float scalar = Mathf.Lerp(minIntensity, 1f, t);

        if (_lastAppliedScalar <= 0f)
        {
            ApplySetActive(true);
        }

        ApplyIntensity(scalar);
        _lastAppliedScalar = scalar;
    }

    private void HandlePowerSurgeActivated()
    {
        _powerSurgeActive = true;
        if (_lastAppliedScalar > 0f)
            ApplySetActive(true);
    }

    private void HandlePowerSurgeDeactivated()
    {
        _powerSurgeActive = false;
        if (_lastAppliedScalar > 0f)
            ApplySetActive(true);
    }

    private void ApplySetActive(bool lit)
    {
        SetListActive(redFires, lit && !_powerSurgeActive);
        SetListActive(blueFires, lit && _powerSurgeActive);
    }

    /// <summary>
    /// Toggles a fire set and keeps each object's burning loop in sync with it, so every
    /// lit fire is heard from its own position on the board instead of one flat loop.
    /// </summary>
    private static void SetListActive(List<GameObject> list, bool active)
    {
        AudioManager audio = ServiceLocator.Get<AudioManager>();

        foreach (GameObject go in list)
        {
            if (go == null) continue;
            if (go.activeSelf != active) go.SetActive(active);

            if (active)
            {
                audio?.StartBurningSound(go);
            }
            else
            {
                audio?.StopBurningSound(go);
            }
        }
    }

    private void ApplyIntensity(float scalar)
    {
        List<ParticleSystem> active = _powerSurgeActive ? _blueParticles : _redParticles;
        float emissionMul = Mathf.Lerp(1f, maxEmissionMultiplier, scalar);
        float sizeMul = Mathf.Lerp(1f, maxSizeMultiplier, scalar);

        foreach (ParticleSystem ps in active)
        {
            if (ps == null) continue;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTimeMultiplier = emissionMul;

            ParticleSystem.MainModule main = ps.main;
            main.startSizeMultiplier = sizeMul;
        }
    }

    private static void CacheParticles(List<GameObject> sources, List<ParticleSystem> cache)
    {
        cache.Clear();
        foreach (GameObject go in sources)
        {
            if (go == null) continue;
            cache.AddRange(go.GetComponentsInChildren<ParticleSystem>(true));
        }
    }
}
