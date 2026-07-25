using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toggles pre-placed red/blue fire VFX around the board and scales their
/// intensity with the current score multiplier. Red set is the default;
/// blue set takes over while drop-target frenzy is active.
/// </summary>
public class BoardFireFXController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DropTargetsScoringMode scoringMode;
    [SerializeField] private ScoreManager scoreManager;

    [Header("Fire Sets (pre-placed in scene)")]
    [Tooltip("Red fire GameObjects — active when not in frenzy.")]
    [SerializeField] private List<GameObject> redFires = new();
    [Tooltip("Blue fire GameObjects — active while frenzy is running.")]
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
    private bool _frenzyActive;
    private float _lastAppliedScalar = -1f;

    private void Awake()
    {
        CacheParticles(redFires, _redParticles);
        CacheParticles(blueFires, _blueParticles);
    }

    private void OnEnable()
    {
        ServiceLocator.Get<FrenzyManager>().OnFrenzyActivated += HandleFrenzyActivated;
        ServiceLocator.Get<FrenzyManager>().OnFrenzyDeactivated += HandleFrenzyDeactivated;

        ApplySetActive(false);
    }

    private void OnDisable()
    {
        if (scoringMode != null)
        {
            ServiceLocator.Get<FrenzyManager>().OnFrenzyActivated -= HandleFrenzyActivated;
            ServiceLocator.Get<FrenzyManager>().OnFrenzyDeactivated -= HandleFrenzyDeactivated;
        }
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

    private void HandleFrenzyActivated()
    {
        _frenzyActive = true;
        if (_lastAppliedScalar > 0f)
            ApplySetActive(true);
    }

    private void HandleFrenzyDeactivated()
    {
        _frenzyActive = false;
        if (_lastAppliedScalar > 0f)
            ApplySetActive(true);
    }

    private void ApplySetActive(bool lit)
    {
        SetListActive(redFires, lit && !_frenzyActive);
        SetListActive(blueFires, lit && _frenzyActive);
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
        List<ParticleSystem> active = _frenzyActive ? _blueParticles : _redParticles;
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
