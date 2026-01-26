using TMPro;
using UnityEngine;

/// <summary>
/// 3D meter that fills based on round-goal progress:
/// progress01 = (ScoreManager.LiveRoundTotal / ScoreManager.Goal).
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

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

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
    [SerializeField] private bool meterColorEnabled = true;
    [Tooltip("Color when the meter is empty (0%).")]
    [SerializeField] private Color meterColorEmpty = Color.white;
    [Tooltip("Color at the mid point (defaults to ~50%).")]
    [SerializeField] private Color meterColorMid = Color.yellow;
    [Tooltip("Color when the meter is full (100%).")]
    [SerializeField] private Color meterColorFull = new Color(1f, 0.5f, 0f, 1f);
    [Tooltip("Where the mid color occurs along the fill amount (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] private float meterColorMidPoint = 0.5f;

    // Meter baseline state (so we can anchor one end while scaling).
    private Vector3 _meterBaseLocalScale;
    private Vector3 _meterBaseLocalPos;
    private float _meterMeshUnitSize = 1f; // mesh length along axis at scale = 1
    private bool _meterInit;
    private float _meterUnitsSmoothed;
    private Renderer _meterRenderer;
    private MaterialPropertyBlock _meterMPB;
    private float _meterBaseAlpha = 1f;

    private void Awake()
    {
        ResolveRefs();
        InitMeterIfNeeded();
    }

    private void OnEnable()
    {
        ResolveRefs();
        if (scoreManager != null)
            scoreManager.ScoreChanged += OnScoreChanged;

        // Initialize meter to correct state immediately.
        UpdateMeterFromScore();
    }

    private void OnDisable()
    {
        if (scoreManager != null)
            scoreManager.ScoreChanged -= OnScoreChanged;
    }

    private void ResolveRefs()
    {
        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();

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
    }

    private void OnScoreChanged()
    {
        UpdateMeterFromScore();
    }

    private void Update()
    {
        // Safety: if we weren't wired at enable-time (or ScoreManager got created later),
        // keep trying to resolve refs.
        if (scoreManager == null)
        {
            ResolveRefs();
            if (scoreManager != null)
                scoreManager.ScoreChanged += OnScoreChanged;
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

        float t01 = (goal > 0f) ? Mathf.Clamp01(live / goal) : 0f;
        float targetUnits = Mathf.Max(0f, meterMaxUnits) * t01;

        UpdateMeterUnits(targetUnits);
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
    }

    private void UpdateMeterUnits(float targetUnits)
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
        float fill01 = meterMaxUnits > 0.0001f ? Mathf.Clamp01(_meterUnitsSmoothed / meterMaxUnits) : 0f;
        UpdateMeterColor(fill01);
    }

    private void UpdateMeterColor(float fill01)
    {
        if (!meterColorEnabled || !meterFill)
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

        Color c = EvaluateMeterColor(fill01);
        c.a = _meterBaseAlpha;

        _meterRenderer.GetPropertyBlock(_meterMPB);
        // Support both built-in/Standard (_Color) and URP/Lit (_BaseColor).
        _meterMPB.SetColor(ColorId, c);
        _meterMPB.SetColor(BaseColorId, c);
        _meterRenderer.SetPropertyBlock(_meterMPB);
    }

    private Color EvaluateMeterColor(float fill01)
    {
        float mid = Mathf.Clamp01(meterColorMidPoint);
        if (fill01 <= mid)
        {
            float t = mid <= 0.0001f ? 1f : Mathf.Clamp01(fill01 / mid);
            return Color.Lerp(meterColorEmpty, meterColorMid, t);
        }
        else
        {
            float denom = Mathf.Max(0.0001f, 1f - mid);
            float t = Mathf.Clamp01((fill01 - mid) / denom);
            return Color.Lerp(meterColorMid, meterColorFull, t);
        }
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
}

