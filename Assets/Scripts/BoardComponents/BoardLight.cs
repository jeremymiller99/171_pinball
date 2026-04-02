// Generated with Cursor (Composer) by assistant, for jjmil, on 2026-04-01.
// Updated with Cursor (Composer) by assistant, for jjmil, on 2026-04-01 (alternatives + flash).
// Updated with Cursor (Composer) by assistant, for jjmil, on 2026-04-01 (FlashLitVersusOffThenOff).
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Board decoration light: off/on colors in the Inspector, optional <see cref="alternativeLitColors"/>
/// for extra lit looks, optional one-shot <see cref="SetLitAppearanceOverride"/> (e.g. drop-target
/// all-down), and simple flash animations. Child <see cref="Light"/> follows lit phases when flashing.
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class BoardLight : MonoBehaviour
{
    private static readonly int baseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int colorId = Shader.PropertyToID("_Color");
    private static readonly int emissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Bulb")]
    [Tooltip("Mesh renderer for the bulb glass / housing. Uses a MaterialPropertyBlock so shared " +
             "materials are not duplicated.")]
    [SerializeField] private Renderer bulbRenderer;

    [Tooltip("Sub-material index on the renderer (0 if the mesh uses one material).")]
    [Min(0)]
    [SerializeField] private int materialIndex;

    [Tooltip("If true, also writes _EmissionColor (URP Lit and similar). Enable if your shader glows " +
             "from emission.")]
    [SerializeField] private bool driveEmission;

    [Header("Colors — Off / On")]
    [SerializeField] private Color offColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color onColor = Color.white;

    [Tooltip("Extra lit-only colors. Select with SetLitAlternativeIndex (0 = first element).")]
    [SerializeField] private Color[] alternativeLitColors;

    [SerializeField] private Color offEmissionColor = Color.black;
    [SerializeField] private Color onEmissionColor = new Color(1.5f, 1.5f, 1.2f, 1f);

    [Header("Light source")]
    [Tooltip("Point/spot light on a child. If empty, the first Light in children is used.")]
    [SerializeField] private Light targetLight;

    [Header("Initial state")]
    [SerializeField] private bool startLit = true;

    [Header("Flash")]
    [Tooltip("If true, flash coroutines use unscaled time (ignores pause / slow-mo).")]
    [SerializeField] private bool flashUsesUnscaledTime;

    [Header("Events (optional)")]
    [Tooltip("Invoked when logical lit state changes (SetLit only), not each flash frame.")]
    [SerializeField] private UnityEvent<bool> onLitChanged;

    private bool _isLit;
    private MaterialPropertyBlock _propertyBlock;
    private bool _runtimePaletteActive;
    private Color _runtimeOff;
    private Color _runtimeOn;
    private bool _litAppearanceOverrideActive;
    private Color _litAppearanceOverride;
    private int _litAlternativeIndex = -1;

    private bool _flashing;
    private Coroutine _flashCoroutine;


    /// <summary>Inspector / UnityEvent hook: pass true = on, false = off.</summary>
    public void SetLitFromEvent(bool lit)
    {
        SetLit(lit);
    }

    /// <summary>Runtime API for scripts (drop targets, portals, goals, etc.). Stops any flash.</summary>
    public void SetLit(bool lit)
    {
        StopFlashingInternal();
        _isLit = lit;
        if (!lit)
        {
            _litAppearanceOverrideActive = false;
        }

        ApplyNow(true);
    }

    public void SetOn()
    {
        SetLit(true);
    }

    public void SetOff()
    {
        SetLit(false);
    }

    public void ToggleLit()
    {
        SetLit(!_isLit);
    }

    public bool IsLit => _isLit;

    public bool IsFlashing => _flashing;

    /// <summary>Re-applies colors and light from current state (stops flash first).</summary>
    public void ReapplyVisuals()
    {
        StopFlashingInternal();
        ApplyNow(false);
    }

    /// <summary>Overrides inspector off/on base colors until <see cref="ClearRuntimePalette"/>.</summary>
    public void SetRuntimeOffOnColors(Color off, Color on)
    {
        _runtimePaletteActive = true;
        _runtimeOff = off;
        _runtimeOn = on;
        if (!_flashing)
        {
            ApplyNow(false);
        }
    }

    public void ClearRuntimePalette()
    {
        _runtimePaletteActive = false;
        if (!_flashing)
        {
            ApplyNow(false);
        }
    }

    /// <summary>When lit, use this color instead of on / alternatives (e.g. all drop targets down).</summary>
    public void SetLitAppearanceOverride(Color whenLitColor)
    {
        _litAppearanceOverrideActive = true;
        _litAppearanceOverride = whenLitColor;
        if (_isLit && !_flashing)
        {
            ApplyNow(false);
        }
    }

    public void ClearLitAppearanceOverride()
    {
        _litAppearanceOverrideActive = false;
        if (_isLit && !_flashing)
        {
            ApplyNow(false);
        }
    }

    /// <summary>
    /// When lit (and no appearance override), use <see cref="alternativeLitColors"/> [index].
    /// Pass -1 to use default <see cref="onColor"/> (or runtime on).
    /// </summary>
    public void SetLitAlternativeIndex(int index)
    {
        _litAlternativeIndex = index;
        if (_isLit && !_flashing)
        {
            ApplyNow(false);
        }
    }

    public void ClearLitAlternativeIndex()
    {
        SetLitAlternativeIndex(-1);
    }

    /// <summary>Alternate full off look vs steady lit look (override / alt / on). Requires IsLit.</summary>
    public void StartFlashLitVersusOff(float fullCycleSeconds)
    {
        if (fullCycleSeconds <= 0f || !_isLit)
        {
            return;
        }

        StopFlashingInternal();
        _flashing = true;
        _flashCoroutine = StartCoroutine(FlashLitVersusOffRoutine(fullCycleSeconds));
    }

    /// <summary>
    /// Same off/on timing as <see cref="StartFlashLitVersusOff"/>, for a fixed number of full cycles,
    /// then <see cref="SetLit"/> (false) so the light always ends off.
    /// </summary>
    public void FlashLitVersusOffThenOff(int fullCycles, float fullCycleSeconds)
    {
        if (fullCycles <= 0 || fullCycleSeconds <= 0f)
        {
            return;
        }

        StopFlashingInternal();
        _isLit = true;
        _flashing = true;
        _flashCoroutine = StartCoroutine(FlashLitVersusOffThenOffRoutine(fullCycles, fullCycleSeconds));
    }

    /// <summary>Alternate default on color vs one alternative (both phases keep the light on).</summary>
    public void StartFlashBetweenOnAndAlternative(int alternativeIndex, float fullCycleSeconds)
    {
        if (fullCycleSeconds <= 0f || !_isLit || alternativeLitColors == null ||
            alternativeLitColors.Length == 0)
        {
            return;
        }

        int i = Mathf.Clamp(alternativeIndex, 0, alternativeLitColors.Length - 1);
        StopFlashingInternal();
        _flashing = true;
        _flashCoroutine = StartCoroutine(FlashTwoLitColorsRoutine(
            EffectiveOnColorIgnoringOverride(),
            alternativeLitColors[i],
            fullCycleSeconds,
            true));
    }

    /// <summary>
    /// Cycle default on color then each alternative. Requires IsLit and at least one alternative.
    /// </summary>
    public void StartFlashCycleOnAndAlternatives(float secondsPerStep)
    {
        if (secondsPerStep <= 0f || !_isLit || alternativeLitColors == null ||
            alternativeLitColors.Length == 0)
        {
            return;
        }

        StopFlashingInternal();
        _flashing = true;
        _flashCoroutine = StartCoroutine(FlashCycleRoutine(secondsPerStep));
    }

    /// <summary>Alternate two arbitrary lit colors (light stays on both phases).</summary>
    public void StartFlashBetweenTwoColors(Color a, Color b, float fullCycleSeconds)
    {
        if (fullCycleSeconds <= 0f || !_isLit)
        {
            return;
        }

        StopFlashingInternal();
        _flashing = true;
        _flashCoroutine = StartCoroutine(FlashTwoLitColorsRoutine(a, b, fullCycleSeconds, true));
    }

    public void StopFlashing()
    {
        StopFlashingInternal();
        ApplyNow(false);
    }

    private void Awake()
    {
        ResolveLightIfNeeded();
        _isLit = startLit;
    }

    private void OnEnable()
    {
        ResolveLightIfNeeded();
        ApplyNow(false);
    }

    private void OnDisable()
    {
        StopFlashingInternal();
    }

    private void StopFlashingInternal()
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }

        _flashing = false;
    }

    private void ResolveLightIfNeeded()
    {
        if (targetLight == null)
        {
            targetLight = GetComponentInChildren<Light>(true);
        }

        if (bulbRenderer == null)
        {
            bulbRenderer = GetComponentInChildren<Renderer>(true);
        }
    }

    private void ApplyNow(bool invokeLitChanged)
    {
        if (_flashing)
        {
            return;
        }

        Color baseCol = ResolveBaseColor();
        PushVisuals(baseCol, _isLit);

        if (invokeLitChanged)
        {
            onLitChanged?.Invoke(_isLit);
        }
    }

    private void PushVisuals(Color baseCol, bool lightEnabled)
    {
        if (bulbRenderer != null)
        {
            int max = bulbRenderer.sharedMaterials != null ? bulbRenderer.sharedMaterials.Length : 0;
            if (max > 0)
            {
                _propertyBlock ??= new MaterialPropertyBlock();
                int idx = Mathf.Clamp(materialIndex, 0, max - 1);

                bulbRenderer.GetPropertyBlock(_propertyBlock, idx);
                _propertyBlock.SetColor(baseColorId, baseCol);
                _propertyBlock.SetColor(colorId, baseCol);

                if (driveEmission)
                {
                    Color em = ResolveEmissionForBase(baseCol, lightEnabled);
                    _propertyBlock.SetColor(emissionColorId, em);
                }

                bulbRenderer.SetPropertyBlock(_propertyBlock, idx);
            }
        }

        if (targetLight != null)
        {
            targetLight.enabled = lightEnabled;
        }
    }

    private Color ResolveBaseColor()
    {
        if (!_isLit)
        {
            return _runtimePaletteActive ? _runtimeOff : offColor;
        }

        if (_litAppearanceOverrideActive)
        {
            return _litAppearanceOverride;
        }

        return ResolveSteadyLitColorNoOverride();
    }

    private Color ResolveSteadyLitColorNoOverride()
    {
        if (_litAlternativeIndex >= 0 && alternativeLitColors != null &&
            _litAlternativeIndex < alternativeLitColors.Length)
        {
            return alternativeLitColors[_litAlternativeIndex];
        }

        return _runtimePaletteActive ? _runtimeOn : onColor;
    }

    private Color EffectiveOnColorIgnoringOverride()
    {
        if (_litAlternativeIndex >= 0 && alternativeLitColors != null &&
            _litAlternativeIndex < alternativeLitColors.Length)
        {
            return alternativeLitColors[_litAlternativeIndex];
        }

        return _runtimePaletteActive ? _runtimeOn : onColor;
    }

    private Color ResolveEmissionForBase(Color baseCol, bool lightEnabled)
    {
        if (!lightEnabled)
        {
            return offEmissionColor;
        }

        if (_flashing)
        {
            return ScaledEmissionFromBase(baseCol);
        }

        if (_litAppearanceOverrideActive && _isLit)
        {
            return ScaledEmissionFromBase(baseCol);
        }

        return onEmissionColor;
    }

    private static Color ScaledEmissionFromBase(Color baseCol)
    {
        return new Color(
            Mathf.Min(4f, baseCol.r * 1.5f),
            Mathf.Min(4f, baseCol.g * 1.5f),
            Mathf.Min(4f, baseCol.b * 1.5f),
            baseCol.a);
    }

    private IEnumerator FlashLitVersusOffRoutine(float fullCycle)
    {
        float half = fullCycle * 0.5f;
        Color litCol = Color.black;
        WaitForSecondsRealtime waitRt = new WaitForSecondsRealtime(half);
        WaitForSeconds waitSc = new WaitForSeconds(half);

        while (_flashing && _isLit)
        {
            litCol = ResolveSteadyLitColorFullStack();
            PushVisuals(offColor, false);
            if (flashUsesUnscaledTime)
            {
                yield return waitRt;
            }
            else
            {
                yield return waitSc;
            }

            if (!_flashing || !_isLit)
            {
                break;
            }

            PushVisuals(litCol, true);
            if (flashUsesUnscaledTime)
            {
                yield return waitRt;
            }
            else
            {
                yield return waitSc;
            }
        }

        StopFlashingInternal();
        ApplyNow(false);
    }

    private IEnumerator FlashLitVersusOffThenOffRoutine(int fullCycles, float fullCycle)
    {
        float half = fullCycle * 0.5f;
        WaitForSecondsRealtime waitRt = new WaitForSecondsRealtime(half);
        WaitForSeconds waitSc = new WaitForSeconds(half);

        for (int c = 0; c < fullCycles; c++)
        {
            if (!_flashing)
            {
                yield break;
            }

            Color litCol = ResolveSteadyLitColorFullStack();
            PushVisuals(offColor, false);
            if (flashUsesUnscaledTime)
            {
                yield return waitRt;
            }
            else
            {
                yield return waitSc;
            }

            if (!_flashing)
            {
                yield break;
            }

            litCol = ResolveSteadyLitColorFullStack();
            PushVisuals(litCol, true);
            if (flashUsesUnscaledTime)
            {
                yield return waitRt;
            }
            else
            {
                yield return waitSc;
            }
        }

        StopFlashingInternal();
        SetLit(false);
    }

    private Color ResolveSteadyLitColorFullStack()
    {
        if (_litAppearanceOverrideActive)
        {
            return _litAppearanceOverride;
        }

        return ResolveSteadyLitColorNoOverride();
    }

    private IEnumerator FlashTwoLitColorsRoutine(Color colorA, Color colorB, float fullCycle, bool lightOn)
    {
        float half = fullCycle * 0.5f;
        bool useA = true;
        WaitForSecondsRealtime waitRt = new WaitForSecondsRealtime(half);
        WaitForSeconds waitSc = new WaitForSeconds(half);

        while (_flashing && _isLit)
        {
            PushVisuals(useA ? colorA : colorB, lightOn);
            useA = !useA;
            if (flashUsesUnscaledTime)
            {
                yield return waitRt;
            }
            else
            {
                yield return waitSc;
            }
        }

        StopFlashingInternal();
        ApplyNow(false);
    }

    private IEnumerator FlashCycleRoutine(float secondsPerStep)
    {
        WaitForSecondsRealtime waitRt = new WaitForSecondsRealtime(secondsPerStep);
        WaitForSeconds waitSc = new WaitForSeconds(secondsPerStep);

        while (_flashing && _isLit)
        {
            Color onC = _runtimePaletteActive ? _runtimeOn : onColor;
            PushVisuals(onC, true);
            if (flashUsesUnscaledTime)
            {
                yield return waitRt;
            }
            else
            {
                yield return waitSc;
            }

            if (!_flashing || !_isLit)
            {
                break;
            }

            for (int i = 0; i < alternativeLitColors.Length; i++)
            {
                if (!_flashing || !_isLit)
                {
                    yield break;
                }

                PushVisuals(alternativeLitColors[i], true);
                if (flashUsesUnscaledTime)
                {
                    yield return waitRt;
                }
                else
                {
                    yield return waitSc;
                }
            }
        }

        StopFlashingInternal();
        ApplyNow(false);
    }

#if UNITY_EDITOR
    [ContextMenu("BoardLight/Toggle Lit")]
    private void ContextToggleLit()
    {
        ToggleLit();
    }

    [ContextMenu("BoardLight/Stop Flash")]
    private void ContextStopFlash()
    {
        StopFlashing();
    }
#endif
}
