using TMPro;
using UnityEngine;

/// <summary>
/// 3D meter that fills based on round-goal progress, using a tiered/stacking system:
/// tier = floor(LiveRoundTotal / Goal)
/// fill01 = (LiveRoundTotal / Goal) - tier
/// Mirrors the "3D Meter" behavior from ActiveBallSpeedHUD (scale + anchor shift).
/// </summary>
public sealed class RoundGoalProgressHUD : MonoBehaviour
{
    private enum MeterAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    [System.Serializable]
    private struct MeterPalette
    {
        public Color empty;
        public Color mid;
        public Color full;
        [Range(0f, 1f)] public float midPoint;

        public static MeterPalette From(Color empty, Color mid, Color full, float midPoint)
        {
            return new MeterPalette
            {
                empty = empty,
                mid = mid,
                full = full,
                midPoint = Mathf.Clamp01(midPoint)
            };
        }
    }

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int MainColorId = Shader.PropertyToID("_MainColor");
    private static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

    [Header("Source")]
    [SerializeField] private ScoreManager scoreManager;

    [Header("UI (optional)")]
    [Tooltip("Optional TMP label that displays the current (live) round total as a number.")]
    [SerializeField] private TMP_Text roundTotalText;
    [SerializeField] private bool autoFindTextInChildren = true;
    [SerializeField] private bool autoFindTextByName = false;
    [SerializeField] private string roundTotalTextObjectName = "RoundTotalText";
    [Tooltip("Number of decimal places to show in the round total text.")]
    [Min(0)]
    [SerializeField] private int roundTotalDecimals = 0;

    [Header("3D Meter")]
    [Tooltip("Assign a 3D box/cube transform that should 'fill' as progress increases.")]
    [SerializeField] private Transform meterFill;
    [SerializeField] private bool autoFindMeterFillInChildren = true;
    [SerializeField] private bool autoFindMeterFillByName = false;
    [SerializeField] private string meterFillObjectName = "MeterFill";
    [Tooltip("Local axis the meter extends along. Use Z if your box points forward.")]
    [SerializeField] private MeterAxis meterAxis = MeterAxis.Z;
    [Tooltip("If false, the meter will extend in the negative axis direction.")]
    [SerializeField] private bool meterPositiveDirection = true;
    [Tooltip("Maximum length of the meter (local-units along the chosen axis).")]
    [Min(0f)]
    [SerializeField] private float meterMaxUnits = 1.0f;
    [Tooltip("0 = no smoothing. Higher values smooth more (exponential).")]
    [Min(0f)]
    [SerializeField] private float meterSmoothing = 12f;

    [Header("3D Meter Color (optional)")]
    [Tooltip("If enabled, the meter fill mesh color will change as it fills (white → yellow → orange).")]
    // Color lerp is core to this meter now; keep serialized for existing scenes but don't expose as a toggle.
    [SerializeField, HideInInspector] private bool meterColorEnabled = true;
    [Tooltip("Color when the meter is empty (0%).")]
    [SerializeField] private Color meterColorEmpty = Color.white;
    [Tooltip("Color at the mid point (defaults to ~50%).")]
    [SerializeField] private Color meterColorMid = Color.yellow;
    [Tooltip("Color when the meter is full (100%).")]
    [SerializeField] private Color meterColorFull = new Color(1f, 0.5f, 0f, 1f);
    [Tooltip("Where the mid color occurs along the fill amount (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] private float meterColorMidPoint = 0.5f;

    [Header("Tiered / Stacking Meter (new)")]
    // Tiered behavior is the default/only mode now. Keep this hidden field only so the value
    // can exist in serialized scenes without confusing the inspector.
    [SerializeField, HideInInspector] private bool tieredMeterEnabled = true;

    [Tooltip("Optional background mesh behind the fill. When tiered, its color becomes the previous tier's 'full' color.")]
    [SerializeField] private Transform meterBackground;
    [SerializeField] private bool autoFindMeterBackgroundInChildren = true;
    [SerializeField] private bool autoFindMeterBackgroundByName = false;
    [SerializeField] private string meterBackgroundObjectName = "MeterBackground";

    [Tooltip("If no background is assigned, this does nothing.")]
    [SerializeField] private bool meterBackgroundColorEnabled = true;

    [Tooltip("Background color to use before the first goal is completed (tier 0).")]
    [SerializeField] private Color meterBackgroundDefaultColor = new Color(0.05f, 0.05f, 0.05f, 1f);

    [Header("Palette integration (optional)")]
    [Tooltip("If assigned, meter background tier-0 default color will be pulled from this palette switcher (so resets match board palette).")]
    [SerializeField] private BoardAlphaPaletteSwitcher paletteSwitcher;

    [Tooltip("Which target index in the palette switcher should drive the meter background default color (usually your BG target).")]
    [Min(0)]
    [SerializeField] private int paletteTargetIndexForMeterBackgroundDefault = 0;

    [Tooltip("If true, overwrite meterBackgroundDefaultColor from the palette switcher on Awake/Enable.")]
    [SerializeField] private bool pullMeterBackgroundDefaultFromPalette = true;

    [Tooltip("If true, uses Palettes By Tier below. Otherwise auto-generates palettes by hue shifting your base colors.")]
    [SerializeField] private bool usePalettesByTier = false;

    [Tooltip("Optional explicit palettes per tier (tier 0 = first). If tier exceeds count, it will clamp or loop based on 'Loop Palettes'.")]
    [SerializeField] private MeterPalette[] palettesByTier;

    [Tooltip("If true, tiers beyond the palette list loop back to the start.")]
    [SerializeField] private bool loopPalettes = true;

    [Tooltip("When auto-generating palettes, shift hue by this amount per tier (0..1). Example: 0.12 ~= 43 degrees.")]
    [Range(0f, 1f)]
    [SerializeField] private float autoHueShiftPerTier = 0.12f;

    // Meter baseline state (so we can anchor one end while scaling).
    private Vector3 _meterBaseLocalScale;
    private Vector3 _meterBaseLocalPos;
    private float _meterMeshUnitSize = 1f; // mesh length along axis at scale = 1
    private bool _meterInit;
    private float _meterUnitsSmoothed;
    private Renderer _meterRenderer;
    private MaterialPropertyBlock _meterMPB;
    private float _meterBaseAlpha = 1f;
    private Material _meterMaterialInstance;

    private Renderer _bgRenderer;
    private MaterialPropertyBlock _bgMPB;
    private float _bgBaseAlpha = 1f;
    private Material _bgMaterialInstance;

    [Header("Color Application (debug / compatibility)")]
    [Tooltip("If enabled, also writes colors directly to Renderer.material (creates a material instance at runtime). " +
             "Use this if your shader ignores MaterialPropertyBlock updates.")]
    [SerializeField] private bool forceMaterialColorUpdates = true;

    private int _displayTier;

    private void Awake()
    {
        ResolveRefs();
        PullMeterBackgroundDefaultFromPalette();
        InitMeterIfNeeded();
    }

    private void OnEnable()
    {
        ResolveRefs();
        PullMeterBackgroundDefaultFromPalette();
        if (scoreManager != null)
        {
            scoreManager.ScoreChanged += OnScoreChanged;
            scoreManager.GoalTierChanged += OnGoalTierChanged;
        }

        // Initialize meter to correct state immediately.
        UpdateMeterFromScore();
    }

    private void OnDisable()
    {
        if (scoreManager != null)
        {
            scoreManager.ScoreChanged -= OnScoreChanged;
            scoreManager.GoalTierChanged -= OnGoalTierChanged;
        }
    }

    private void ResolveRefs()
    {
        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();

        if (paletteSwitcher == null)
            paletteSwitcher = FindFirstObjectByType<BoardAlphaPaletteSwitcher>();

        if (!roundTotalText)
        {
            // Prefer local lookup (HUD prefab), then optional global name lookup.
            if (autoFindTextInChildren)
                roundTotalText = GetComponentInChildren<TMP_Text>(includeInactive: true);

            if (!roundTotalText && autoFindTextByName && !string.IsNullOrWhiteSpace(roundTotalTextObjectName))
            {
                var go = GameObject.Find(roundTotalTextObjectName);
                if (go) roundTotalText = go.GetComponent<TMP_Text>();
            }
        }

        if (!meterFill)
        {
            // Prefer local lookup (HUD prefab), then optional global name lookup.
            if (autoFindMeterFillInChildren)
                meterFill = FindLikelyMeterFillInChildren();

            if (!meterFill && autoFindMeterFillByName && !string.IsNullOrWhiteSpace(meterFillObjectName))
            {
                var go = GameObject.Find(meterFillObjectName);
                if (go) meterFill = go.transform;
            }
        }

        if (!meterBackground)
        {
            if (autoFindMeterBackgroundInChildren)
                meterBackground = FindLikelyMeterBackgroundInChildren();

            if (!meterBackground && autoFindMeterBackgroundByName && !string.IsNullOrWhiteSpace(meterBackgroundObjectName))
            {
                var go = GameObject.Find(meterBackgroundObjectName);
                if (go) meterBackground = go.transform;
            }

            // Fallback: if the fill is assigned somewhere else in the scene,
            // try to find a sibling "background" object near it.
            if (!meterBackground && meterFill)
                meterBackground = FindLikelyMeterBackgroundNearFill(meterFill);
        }
    }

    private void OnScoreChanged()
    {
        UpdateMeterFromScore();
    }

    private void OnGoalTierChanged(int newTier)
    {
        // Prefer event-driven tier transitions to avoid per-frame flicker at thresholds.
        HandleTierTransitionIfNeeded(newTier);
    }

    private void Update()
    {
        // Safety: if we weren't wired at enable-time (or ScoreManager got created later),
        // keep trying to resolve refs.
        if (scoreManager == null)
        {
            ResolveRefs();
            if (scoreManager != null)
            {
                scoreManager.ScoreChanged += OnScoreChanged;
                scoreManager.GoalTierChanged += OnGoalTierChanged;
            }
        }

        // If no events fire (e.g., custom scripts mutate fields directly),
        // keep the meter reasonably up-to-date.
        UpdateMeterFromScore();
    }

    private void UpdateMeterFromScore()
    {
        if (!meterFill)
            return;

        if (!_meterInit)
            InitMeterIfNeeded();

        float goal = scoreManager != null ? scoreManager.Goal : 0f;
        float live = scoreManager != null ? scoreManager.LiveRoundTotal : 0f;

        if (roundTotalText)
        {
            int d = Mathf.Clamp(roundTotalDecimals, 0, 6);
            roundTotalText.text = live.ToString("F" + d);
        }

        int tier = 0;
        float t01 = 0f;
        if (goal > 0f && live > 0f)
        {
            float raw = live / goal;
            // Compute tier independently of ScoreManager's optional tier scaling feature.
            // Add a tiny epsilon to reduce float edge cases at exact boundaries.
            tier = Mathf.Max(0, Mathf.FloorToInt(raw + 0.0001f));
            t01 = Mathf.Clamp01(raw - tier);
        }

        HandleTierTransitionIfNeeded(tier);

        float targetUnits = Mathf.Max(0f, meterMaxUnits) * t01;

        UpdateMeterUnits(targetUnits, t01, _displayTier);
    }

    private void InitMeterIfNeeded()
    {
        if (_meterInit || !meterFill)
            return;

        _meterInit = true;
        _meterBaseLocalScale = meterFill.localScale;
        _meterBaseLocalPos = meterFill.localPosition;
        _meterMeshUnitSize = Mathf.Max(0.0001f, GetMeshUnitSizeAlongAxis(meterFill, meterAxis));
        _meterUnitsSmoothed = 0f;
        _displayTier = 0;

        // Cache renderer + baseline alpha so we can tint via MaterialPropertyBlock (no material instancing).
        _meterRenderer = meterFill.GetComponent<Renderer>();
        if (!_meterRenderer)
            _meterRenderer = meterFill.GetComponentInChildren<Renderer>();

        _meterMPB ??= new MaterialPropertyBlock();
        _meterBaseAlpha = 1f;
        if (_meterRenderer && _meterRenderer.sharedMaterial)
        {
            var mat = _meterRenderer.sharedMaterial;
            if (mat.HasProperty(BaseColorId))
                _meterBaseAlpha = mat.GetColor(BaseColorId).a;
            else if (mat.HasProperty(ColorId))
                _meterBaseAlpha = mat.GetColor(ColorId).a;
        }
        _meterMaterialInstance = null;
        if (forceMaterialColorUpdates && _meterRenderer)
        {
            // Grab (and thus create) the renderer material instance once.
            _meterMaterialInstance = _meterRenderer.material;
        }

        InitBackgroundIfNeeded();
        UpdateBackgroundForTier(_displayTier);
    }

    private void UpdateMeterUnits(float targetUnits, float fill01, int tier)
    {
        if (!_meterInit)
            return;

        if (meterSmoothing <= 0f)
        {
            _meterUnitsSmoothed = targetUnits;
        }
        else
        {
            // Exponential smoothing (stable across frame rates).
            float t = 1f - Mathf.Exp(-meterSmoothing * Time.deltaTime);
            _meterUnitsSmoothed = Mathf.Lerp(_meterUnitsSmoothed, targetUnits, t);
        }

        // Convert desired length (units) into a scale along the axis, based on mesh size at scale=1.
        float targetScaleAxis = _meterUnitsSmoothed / _meterMeshUnitSize;

        // Anchor the "bottom" (negative end) by shifting position by half the length delta.
        Vector3 axisDir = GetLocalAxisDir(meterAxis) * (meterPositiveDirection ? 1f : -1f);

        float baseScaleAxis = GetAxisValue(_meterBaseLocalScale, meterAxis);
        float baseLen = _meterMeshUnitSize * baseScaleAxis;
        float newLen = _meterMeshUnitSize * targetScaleAxis;
        float deltaLen = newLen - baseLen;

        Vector3 newScale = _meterBaseLocalScale;
        newScale = SetAxisValue(newScale, meterAxis, targetScaleAxis);

        meterFill.localScale = newScale;
        meterFill.localPosition = _meterBaseLocalPos + axisDir * (deltaLen * 0.5f);

        // Drive fill color based on current fill percent (use smoothed units to match visuals).
        float smoothedFill01 = meterMaxUnits > 0.0001f ? Mathf.Clamp01(_meterUnitsSmoothed / meterMaxUnits) : 0f;
        UpdateMeterColor(smoothedFill01, tier);
    }

    private void UpdateMeterColor(float fill01, int tier)
    {
        if (!meterFill)
            return;

        if (!_meterRenderer)
        {
            _meterRenderer = meterFill.GetComponent<Renderer>();
            if (!_meterRenderer)
                _meterRenderer = meterFill.GetComponentInChildren<Renderer>();
        }

        if (!_meterRenderer)
            return;

        _meterMPB ??= new MaterialPropertyBlock();

        Color c = EvaluateMeterColor(fill01, tier);
        c.a = _meterBaseAlpha;

        ApplyColorToRenderer(_meterRenderer, _meterMPB, c, ref _meterMaterialInstance);
    }

    private Color EvaluateMeterColor(float fill01, int tier)
    {
        MeterPalette p = GetPaletteForTier(tier);
        float mid = Mathf.Clamp01(p.midPoint);
        if (fill01 <= mid)
        {
            float t = mid <= 0.0001f ? 1f : Mathf.Clamp01(fill01 / mid);
            return Color.Lerp(p.empty, p.mid, t);
        }
        else
        {
            float denom = Mathf.Max(0.0001f, 1f - mid);
            float t = Mathf.Clamp01((fill01 - mid) / denom);
            return Color.Lerp(p.mid, p.full, t);
        }
    }

    private void HandleTierTransitionIfNeeded(int newTier)
    {
        if (!_meterInit)
            return;

        newTier = Mathf.Max(0, newTier);
        if (newTier == _displayTier)
            return;

        // When wrapping tiers, reset smoothing so the bar doesn't "lerp backward" from full to empty.
        _displayTier = newTier;
        _meterUnitsSmoothed = 0f;

        // Keep this very simple: just update colors on tier change.
        UpdateBackgroundForTier(_displayTier);
    }

    private void InitBackgroundIfNeeded()
    {
        if (_bgRenderer || !meterBackground)
            return;

        _bgRenderer = meterBackground.GetComponent<Renderer>();
        if (!_bgRenderer)
            _bgRenderer = meterBackground.GetComponentInChildren<Renderer>();

        _bgMPB ??= new MaterialPropertyBlock();
        _bgBaseAlpha = 1f;
        if (_bgRenderer && _bgRenderer.sharedMaterial)
        {
            var mat = _bgRenderer.sharedMaterial;
            if (mat.HasProperty(BaseColorId))
                _bgBaseAlpha = mat.GetColor(BaseColorId).a;
            else if (mat.HasProperty(ColorId))
                _bgBaseAlpha = mat.GetColor(ColorId).a;
        }

        _bgMaterialInstance = null;
        if (forceMaterialColorUpdates && _bgRenderer)
        {
            _bgMaterialInstance = _bgRenderer.material;
        }
    }

    private void PullMeterBackgroundDefaultFromPalette()
    {
        if (!pullMeterBackgroundDefaultFromPalette)
            return;

        if (paletteSwitcher == null)
            return;

        if (paletteSwitcher.TryGetCurrentColor(paletteTargetIndexForMeterBackgroundDefault, out Color c))
        {
            // Alpha is driven by the background material/base alpha anyway.
            meterBackgroundDefaultColor = c;
        }
    }

    private void UpdateBackgroundForTier(int tier)
    {
        if (!meterBackgroundColorEnabled)
            return;

        InitBackgroundIfNeeded();
        if (!_bgRenderer)
            return;

        // Background becomes the previous tier's FULL color to create a stacked look.
        // Tier 0 uses the configured default background color.
        Color c = tier <= 0 ? meterBackgroundDefaultColor : GetPaletteForTier(tier - 1).full;
        c.a = _bgBaseAlpha;

        ApplyColorToRenderer(_bgRenderer, _bgMPB, c, ref _bgMaterialInstance);
    }

    private void ApplyColorToRenderer(Renderer r, MaterialPropertyBlock mpb, Color c, ref Material materialInstance)
    {
        if (!r)
            return;

        r.GetPropertyBlock(mpb);
        // Support common shader color properties (varies by pipeline/shader).
        mpb.SetColor(ColorId, c);           // Built-in/Standard
        mpb.SetColor(BaseColorId, c);       // URP Lit/Unlit, HDRP Lit
        mpb.SetColor(TintColorId, c);       // Many unlit/custom shaders
        mpb.SetColor(MainColorId, c);       // Some custom shaders
        mpb.SetColor(UnlitColorId, c);      // Some unlit shaders
        mpb.SetColor(EmissionColorId, c);   // If emission enabled in material
        mpb.SetColor(EmissiveColorId, c);   // HDRP
        r.SetPropertyBlock(mpb);

        if (!forceMaterialColorUpdates)
            return;

        if (!materialInstance && r)
        {
            // Ensure we have (and thus create) an instance once, not every frame.
            materialInstance = r.material;
        }

        if (!materialInstance)
            return;

        SetColorIfPresent(materialInstance, ColorId, c);
        SetColorIfPresent(materialInstance, BaseColorId, c);
        SetColorIfPresent(materialInstance, TintColorId, c);
        SetColorIfPresent(materialInstance, MainColorId, c);
        SetColorIfPresent(materialInstance, UnlitColorId, c);
        // Emission only shows if enabled on the material; still safe to set.
        SetColorIfPresent(materialInstance, EmissionColorId, c);
        SetColorIfPresent(materialInstance, EmissiveColorId, c);
    }

    private static void SetColorIfPresent(Material m, int propertyId, Color c)
    {
        if (m != null && m.HasProperty(propertyId))
            m.SetColor(propertyId, c);
    }

    private MeterPalette GetPaletteForTier(int tier)
    {
        tier = Mathf.Max(0, tier);

        // Tier 0 base palette from existing fields (keeps current behavior).
        var basePalette = MeterPalette.From(meterColorEmpty, meterColorMid, meterColorFull, meterColorMidPoint);

        // If palettes are provided, prefer them (this is the "color sets" workflow).
        // The toggle remains, but palettes win if present to avoid "it still uses old colors" confusion.
        bool hasPalettes = palettesByTier != null && palettesByTier.Length > 0;
        if ((usePalettesByTier || hasPalettes) && hasPalettes)
        {
            int idx;
            if (loopPalettes)
                idx = tier % palettesByTier.Length;
            else
                idx = Mathf.Clamp(tier, 0, palettesByTier.Length - 1);

            // If the inspector palette has default (0,0,0,0) values, fall back to base.
            MeterPalette p = palettesByTier[idx];
            if (p.midPoint <= 0f && p.empty.a == 0f && p.mid.a == 0f && p.full.a == 0f)
                return basePalette;

            if (p.midPoint <= 0f)
                p.midPoint = basePalette.midPoint;

            return p;
        }

        // Auto-generate by hue shifting the base palette.
        if (tier <= 0)
            return basePalette;

        // NOTE: existing scenes may serialize this new field as 0; ensure we still cycle by default.
        float perTier = autoHueShiftPerTier > 0.0001f ? autoHueShiftPerTier : 0.12f;
        float shift = Mathf.Repeat(perTier * tier, 1f);
        return MeterPalette.From(
            ShiftHue(basePalette.empty, shift),
            ShiftHue(basePalette.mid, shift),
            ShiftHue(basePalette.full, shift),
            basePalette.midPoint
        );
    }

    private static Color ShiftHue(Color c, float hueShift01)
    {
        // Preserve alpha while shifting hue.
        Color.RGBToHSV(c, out float h, out float s, out float v);
        h = Mathf.Repeat(h + hueShift01, 1f);
        Color outC = Color.HSVToRGB(h, s, v);
        outC.a = c.a;
        return outC;
    }

    private static Vector3 GetLocalAxisDir(MeterAxis axis)
    {
        switch (axis)
        {
            case MeterAxis.X: return Vector3.right;
            case MeterAxis.Y: return Vector3.up;
            default:
            case MeterAxis.Z: return Vector3.forward;
        }
    }

    private static float GetAxisValue(Vector3 v, MeterAxis axis)
    {
        switch (axis)
        {
            case MeterAxis.X: return v.x;
            case MeterAxis.Y: return v.y;
            default:
            case MeterAxis.Z: return v.z;
        }
    }

    private static Vector3 SetAxisValue(Vector3 v, MeterAxis axis, float value)
    {
        switch (axis)
        {
            case MeterAxis.X: v.x = value; break;
            case MeterAxis.Y: v.y = value; break;
            default:
            case MeterAxis.Z: v.z = value; break;
        }
        return v;
    }

    private static float GetMeshUnitSizeAlongAxis(Transform t, MeterAxis axis)
    {
        if (!t)
            return 1f;

        // Prefer mesh bounds (local space, scale=1). Works great for Unity Cube and most mesh-based bars.
        var mf = t.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 size = mf.sharedMesh.bounds.size;
            return axis == MeterAxis.X ? size.x : axis == MeterAxis.Y ? size.y : size.z;
        }

        // Fallback: collider size (still local-ish and usually matches the mesh for primitives).
        var bc = t.GetComponent<BoxCollider>();
        if (bc != null)
        {
            Vector3 size = bc.size;
            return axis == MeterAxis.X ? size.x : axis == MeterAxis.Y ? size.y : size.z;
        }

        return 1f;
    }

    private Transform FindLikelyMeterFillInChildren()
    {
        // Heuristic: first child transform with a Renderer (and not a TMP object).
        // This keeps the prefab self-contained when moved between scenes.
        var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (var r in renderers)
        {
            if (!r) continue;
            if (r.GetComponent<TMP_Text>()) continue;
            return r.transform;
        }

        return null;
    }

    private Transform FindLikelyMeterBackgroundInChildren()
    {
        // Heuristic: first renderer transform that is NOT the fill (and not TMP).
        // If your HUD has multiple renderers, consider using name-based lookup.
        var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (var r in renderers)
        {
            if (!r) continue;
            if (r.GetComponent<TMP_Text>()) continue;
            if (meterFill != null && (r.transform == meterFill || r.transform.IsChildOf(meterFill))) continue;
            return r.transform;
        }

        return null;
    }

    private static Transform FindLikelyMeterBackgroundNearFill(Transform fill)
    {
        if (!fill)
            return null;

        Transform parent = fill.parent;
        if (!parent)
            return null;

        // Prefer name hints among siblings.
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform t = parent.GetChild(i);
            if (!t || t == fill) continue;

            string n = t.name ?? string.Empty;
            bool nameHint =
                n.IndexOf("background", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("back", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("bg", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (!nameHint) continue;

            var r = t.GetComponent<Renderer>();
            if (r) return t;
        }

        // Fallback: first renderer sibling that isn't the fill.
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform t = parent.GetChild(i);
            if (!t || t == fill) continue;
            var r = t.GetComponent<Renderer>();
            if (r) return t;
        }

        return null;
    }
}

