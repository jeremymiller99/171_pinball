// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-17.
using UnityEngine;
using TMPro;
using System;

public class FloatingText : MonoBehaviour
{
    [Header("Float Up")]
    [SerializeField] private float moveSpeed = 100f;
    [SerializeField] private float fadeSpeed = 1f;
    [SerializeField] private float lifetime = 1f;

    [Header("Fly To")]
    [SerializeField] private float flyDuration = 0.45f;
    [SerializeField] private bool flyUseSpeed = true;
    [Tooltip("Anchored-units per second when FlyUseSpeed is enabled.")]
    [SerializeField] private float flySpeed = 1200f;
    [SerializeField] private float flyDurationMin = 0.18f;
    [SerializeField] private float flyDurationMax = 0.75f;

    [Header("Fly To - Easing")]
    [Tooltip("Higher values start slower and accelerate toward the destination (ease-in). 1 = linear.")]
    [SerializeField] private float flyEaseInExponent = 2.6f;

    [Header("Fly To - Readability Lead-In")]
    [Tooltip("Move up first so the popup is readable before it flies to the UI.")]
    [SerializeField] private float flyPreRiseDuration = 0.12f;
    [SerializeField] private float flyPreRiseDistance = 40f;

    [Header("Fly To - Random Kick")]
    [SerializeField] private bool flyUseRandomKick = true;
    [Tooltip("Max angle (degrees) to rotate the fly direction for a kick (randomized +/-).")]
    [SerializeField] private float flyKickAngleMaxDegrees = 35f;
    [SerializeField] private float flyKickDistanceMin = 0f;
    [SerializeField] private float flyKickDistanceMax = 120f;
    [SerializeField] private float flyArcHeight = 40f;
    [Range(0f, 1f)]
    [SerializeField] private float flyFadeStartNormalized = 0.75f;
    [SerializeField] private bool destroyOnFlyComplete = true;

    private TMP_Text text;
    private RectTransform rectTransform;
    private Color startColor;

    private enum AnimationMode
    {
        FloatUp,
        FlyTo,
    }

    private AnimationMode animationMode = AnimationMode.FloatUp;

    private float ageSeconds;

    private bool flyConfigured;
    private Vector2 flyFromAnchored;
    private Vector2 flyToAnchored;
    private Vector2 flyControlAnchored;
    private float flyDistanceAnchored;
    private float flyElapsedSeconds;
    private Vector2 flyPreRiseEndAnchored;
    private Action onFlyComplete;
    private bool flyCompletionInvoked;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        rectTransform = GetComponent<RectTransform>();
        startColor = text.color;
        ageSeconds = 0f;
    }

    private void Update()
    {
        ageSeconds += Time.deltaTime;

        if (animationMode == AnimationMode.FlyTo && flyConfigured)
        {
            TickFlyTo();
            return;
        }

        TickFloatUp();
    }

    public void SetText(string value)
    {
        if (text == null) text = GetComponent<TMP_Text>();
        text.text = value;
    }

    public void SetFontAsset(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null) return;
        if (text == null) text = GetComponent<TMP_Text>();
        text.font = fontAsset;
        startColor = text.color;
    }

    public void SetScale(float scale)
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        rectTransform.localScale = Vector3.one * scale;
    }

    public void SetLifetime(float seconds)
    {
        lifetime = Mathf.Max(0.01f, seconds);
    }

    public void SetOnFlyComplete(Action callback)
    {
        onFlyComplete = callback;
    }

    public void PlayFlyTo(Vector2 destinationAnchored)
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

        flyFromAnchored = rectTransform.anchoredPosition;
        flyToAnchored = destinationAnchored;
        flyPreRiseEndAnchored = flyFromAnchored + (Vector2.up * flyPreRiseDistance);

        Vector2 mainFrom = flyPreRiseDuration > 0f ? flyPreRiseEndAnchored : flyFromAnchored;
        flyControlAnchored = ComputeFlyControlPoint(mainFrom, flyToAnchored);
        flyDistanceAnchored = Vector2.Distance(mainFrom, flyToAnchored);
        flyElapsedSeconds = 0f;
        flyConfigured = true;
        flyCompletionInvoked = false;
        animationMode = AnimationMode.FlyTo;
    }

    private void TickFloatUp()
    {
        if (rectTransform == null) return;

        rectTransform.anchoredPosition += Vector2.up * (moveSpeed * Time.deltaTime);

        float alpha = startColor.a - (fadeSpeed * ageSeconds);
        ApplyAlpha(alpha);

        if (ageSeconds >= lifetime || alpha <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void TickFlyTo()
    {
        if (rectTransform == null) return;

        flyElapsedSeconds += Time.deltaTime;

        float preDuration = Mathf.Max(0f, flyPreRiseDuration);
        if (preDuration > 0f && flyElapsedSeconds < preDuration)
        {
            float u0 = Mathf.Clamp01(flyElapsedSeconds / preDuration);
            float s0 = u0 * u0 * (3f - 2f * u0);
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(flyFromAnchored, flyPreRiseEndAnchored, s0);
            ApplyAlpha(startColor.a);
            return;
        }

        float duration = GetFlyDurationSeconds();
        float flyTime = preDuration > 0f ? (flyElapsedSeconds - preDuration) : flyElapsedSeconds;
        float u = Mathf.Clamp01(flyTime / duration);
        float s = EvaluateFlyEasing(u);

        Vector2 mainFrom = preDuration > 0f ? flyPreRiseEndAnchored : flyFromAnchored;
        Vector2 pos = QuadraticBezier(mainFrom, flyControlAnchored, flyToAnchored, s);
        float arc = 4f * u * (1f - u);
        pos += Vector2.up * (flyArcHeight * arc);
        rectTransform.anchoredPosition = pos;

        float alpha = startColor.a;
        if (flyFadeStartNormalized < 1f)
        {
            float denom = Mathf.Max(0.0001f, 1f - flyFadeStartNormalized);
            float fadeU = Mathf.Clamp01((u - flyFadeStartNormalized) / denom);
            alpha = Mathf.Lerp(startColor.a, 0f, fadeU);
        }

        ApplyAlpha(alpha);

        if (u >= 1f && !flyCompletionInvoked)
        {
            onFlyComplete?.Invoke();
            flyCompletionInvoked = true;
        }

        if (destroyOnFlyComplete && u >= 1f)
        {
            Destroy(gameObject);
        }

        if (!destroyOnFlyComplete && (ageSeconds >= lifetime || alpha <= 0f))
        {
            Destroy(gameObject);
        }
    }

    private void ApplyAlpha(float alpha)
    {
        if (text == null) return;

        Color c = text.color;
        c.a = Mathf.Clamp01(alpha);
        text.color = c;
    }

    private float GetFlyDurationSeconds()
    {
        if (!flyUseSpeed)
            return Mathf.Max(0.0001f, flyDuration);

        float speed = Mathf.Max(0.0001f, flySpeed);
        float d = Mathf.Max(0f, flyDistanceAnchored);
        float duration = d / speed;
        duration = Mathf.Clamp(duration, flyDurationMin, flyDurationMax);
        return Mathf.Max(0.0001f, duration);
    }

    private Vector2 ComputeFlyControlPoint(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float mag = delta.magnitude;
        if (mag <= 0.0001f)
            return from;

        Vector2 dir = delta / mag;

        if (!flyUseRandomKick)
            return from + (dir * (mag * 0.35f));

        float angle = UnityEngine.Random.Range(-flyKickAngleMaxDegrees, flyKickAngleMaxDegrees);
        float dist = UnityEngine.Random.Range(flyKickDistanceMin, flyKickDistanceMax);
        Vector2 kickDir = Rotate(dir, angle);
        Vector2 control = from + (kickDir * dist);
        return control;
    }

    private float EvaluateFlyEasing(float u)
    {
        float exp = Mathf.Clamp(flyEaseInExponent, 0.25f, 8f);
        if (Mathf.Approximately(exp, 1f))
            return u;

        return Mathf.Pow(u, exp);
    }

    private static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return (u * u * a) + (2f * u * t * b) + (t * t * c);
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2((v.x * cos) - (v.y * sin), (v.x * sin) + (v.y * cos));
    }
}

