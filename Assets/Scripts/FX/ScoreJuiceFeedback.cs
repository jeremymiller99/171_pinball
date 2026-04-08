// Updated with Cursor (Composer) by assistant on 2026-03-31.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ScoreJuiceFeedback : MonoBehaviour
{
    [Header("Camera Shake on Score")]
    [Tooltip("Reference to CameraShake. Auto-resolved if not set.")]
    [SerializeField] private CameraShake cameraShake;

    [Header("Points Shake Settings")]
    [Tooltip("Base duration of camera shake when earning points.")]
    [SerializeField] private float shakeBaseDuration = 0.22f;

    [Tooltip("Exponent applied to points before converting to shake. Lower (< 1) ramps sooner.")]
    [Range(0.25f, 1.5f)]
    [SerializeField] private float shakePointsExponent = 0.75f;

    [Tooltip("How much to increase duration per shaped points. Shaped = points ^ exponent.")]
    [SerializeField] private float shakeDurationPerPoint = 0.01f;

    [Tooltip("Maximum shake duration cap for points.")]
    [SerializeField] private float shakeMaxDuration = 0.65f;

    [Tooltip("Base magnitude of camera shake when earning points.")]
    [SerializeField] private float shakeBaseMagnitude = 0.24f;

    [Tooltip("How much to scale shake magnitude per shaped points. Shaped = points ^ exponent.")]
    [SerializeField] private float shakeMagnitudePerPoint = 0.02f;

    [Tooltip("Maximum shake magnitude cap for points.")]
    [SerializeField] private float shakeMaxMagnitude = 1.0f;

    [Header("Multiplier Shake Settings")]
    [Tooltip("Base duration of camera shake when gaining multiplier.")]
    [SerializeField] private float multShakeBaseDuration = 0.28f;

    [Tooltip("Exponent applied to mult gain before converting to shake. Lower (< 1) ramps sooner.")]
    [Range(0.25f, 1.5f)]
    [SerializeField] private float multShakeExponent = 0.75f;

    [Tooltip("How much to increase duration per shaped mult gain. Shaped = multGain ^ exponent.")]
    [SerializeField] private float multShakeDurationPerMult = 0.22f;

    [Tooltip("Maximum shake duration cap for multiplier.")]
    [SerializeField] private float multShakeMaxDuration = 0.75f;

    [Tooltip("Base magnitude of camera shake when gaining multiplier.")]
    [SerializeField] private float multShakeBaseMagnitude = 0.26f;

    [Tooltip("How much to scale shake magnitude per shaped mult gain. Shaped = multGain ^ exponent.")]
    [SerializeField] private float multShakeMagnitudePerMult = 0.28f;

    [Tooltip("Maximum shake magnitude cap for multiplier.")]
    [SerializeField] private float multShakeMaxMagnitude = 0.85f;

    private int compTriggered = 35;
    private int framesSinceLastScore = 0;
    private const int FramesToResetAudio = 200;

    private void Awake()
    {
        ServiceLocator.Register<ScoreJuiceFeedback>(this);
        ResolveCameraShake();
    }

    private void OnEnable()
    {
        ScoreManager sm = ServiceLocator.Get<ScoreManager>();
        if (sm != null)
        {
            sm.PointsAdded += OnPointsAdded;
            sm.MultAdded += OnMultAdded;
            sm.BallBanked += OnBallBanked;
        }

        ScoreUIController ui = ServiceLocator.Get<ScoreUIController>();
        if (ui != null)
        {
            ui.ScoreUiPopped += OnScoreUiPopped;
        }
    }

    private void OnDisable()
    {
        ScoreManager sm = ServiceLocator.TryGet<ScoreManager>(out var manager) ? manager : null;
        if (sm != null)
        {
            sm.PointsAdded -= OnPointsAdded;
            sm.MultAdded -= OnMultAdded;
            sm.BallBanked -= OnBallBanked;
        }

        ScoreUIController ui = ServiceLocator.TryGet<ScoreUIController>(out var uiController) ? uiController : null;
        if (ui != null)
        {
            ui.ScoreUiPopped -= OnScoreUiPopped;
        }

        ServiceLocator.Unregister<ScoreJuiceFeedback>();
    }

    private void Update()
    {
        if (framesSinceLastScore < FramesToResetAudio)
        {
            framesSinceLastScore++;
            if (framesSinceLastScore == FramesToResetAudio)
            {
                ResetAudioPitch();
            }
        }
    }

    private void OnBallBanked()
    {
        ResetAudioPitch();
    }

    private void ResetAudioPitch()
    {
        compTriggered = 35;
    }

    private void OnPointsAdded(float applied, float newTotal)
    {
        if (applied > 0f)
        {
            TriggerScoreShake(applied);
        }
    }

    private void OnMultAdded(float applied, float newTotal)
    {
        if (applied > 0f)
        {
            TriggerMultShake(applied);
        }
    }

    private void OnScoreUiPopped(bool multChanged)
    {
        if (multChanged)
        {
            ServiceLocator.Get<AudioManager>()?.PlayMultAdd(compTriggered);
        }

        if (multChanged)
        {
            framesSinceLastScore = 0;
            if (compTriggered < 80)
            {
                compTriggered += 5;
            }
        }
    }

    private void ResolveCameraShake()
    {
        if (cameraShake != null && cameraShake.isActiveAndEnabled)
            return;

        cameraShake = ServiceLocator.Get<CameraShake>();
    }

    private void TriggerScoreShake(float pointsEarned)
    {
        if (cameraShake == null || !cameraShake.isActiveAndEnabled)
            ResolveCameraShake();

        if (cameraShake == null || !cameraShake.isActiveAndEnabled)
            return;

        float shaped = ShapeShakeInput(pointsEarned, shakePointsExponent);

        float duration = shakeBaseDuration + (shaped * shakeDurationPerPoint);
        duration = Mathf.Clamp(duration, shakeBaseDuration, shakeMaxDuration);

        float magnitude = shakeBaseMagnitude + (shaped * shakeMagnitudePerPoint);
        magnitude = Mathf.Clamp(magnitude, shakeBaseMagnitude, shakeMaxMagnitude);

        cameraShake.Shake(duration, magnitude);
    }

    private void TriggerMultShake(float multGained)
    {
        if (cameraShake == null || !cameraShake.isActiveAndEnabled)
            ResolveCameraShake();

        if (cameraShake == null || !cameraShake.isActiveAndEnabled)
            return;

        float shaped = ShapeShakeInput(multGained, multShakeExponent);

        float duration = multShakeBaseDuration + (shaped * multShakeDurationPerMult);
        duration = Mathf.Clamp(duration, multShakeBaseDuration, multShakeMaxDuration);

        float magnitude = multShakeBaseMagnitude + (shaped * multShakeMagnitudePerMult);
        magnitude = Mathf.Clamp(magnitude, multShakeBaseMagnitude, multShakeMaxMagnitude);

        cameraShake.Shake(duration, magnitude);
    }

    private static float ShapeShakeInput(float value, float exponent)
    {
        if (value <= 0f) return 0f;
        float safeExponent = Mathf.Max(0.0001f, exponent);
        return Mathf.Pow(value, safeExponent);
    }
}
