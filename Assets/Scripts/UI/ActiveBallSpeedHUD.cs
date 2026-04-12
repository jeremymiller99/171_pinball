using TMPro;
using UnityEngine;

/// <summary>
/// Put this on a HUD/UI object (not the ball). It finds the active ball and displays its speed on a TMP label.
/// This avoids "hand" balls overwriting the UI when multiple ball prefabs exist simultaneously.
/// </summary>
public sealed class ActiveBallSpeedHUD : MonoBehaviour
{
    private enum MeterAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int MainColorId = Shader.PropertyToID("_MainColor");
    private static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

    [Header("UI")]
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private bool autoFindTextByName = true;
    [SerializeField] private string speedTextObjectName = "BallSpeedText";
    [SerializeField] private bool autoFindTextInChildren = true;

    [Header("Source")]
    [SerializeField] private GameRulesManager gameRules;
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private DropTargetsScoringMode dropTargetsScoringMode;

    [Header("Behavior")]
    [Tooltip("If true, shows 0 until the active ball is non-kinematic.")]
    [SerializeField] private bool freezeWhileKinematic = true;
    [SerializeField] private int decimals = 2;
    [Tooltip("If true, appends ' m/s' to the numeric speed readout.")]
    [SerializeField] private bool showUnits = false;

    [Header("3D Meter (optional)")]
    [Tooltip("Assign a 3D box/cube transform that should 'fill' as speed increases.")]
    [SerializeField] private Transform meterFill;
    [SerializeField] private bool autoFindMeterFillInChildren = true;
    [SerializeField] private bool autoFindMeterFillByName = false;
    [SerializeField] private string meterFillObjectName = "MeterFill";
    [Tooltip("Local axis the meter extends along. Use Z if your box points forward.")]
    [SerializeField] private MeterAxis meterAxis = MeterAxis.Z;
    [Tooltip("If false, the meter will extend in the negative axis direction.")]
    [SerializeField] private bool meterPositiveDirection = true;
    [Tooltip("How many local-units of meter length per 1 m/s of speed.")]
    [Min(0f)]
    [SerializeField] private float meterUnitsPerMps = 0.02f;
    [Tooltip("Speed (m/s) that corresponds to a full meter. Example: 50 means 0..50 m/s maps to 0..100% fill.")]
    [Min(0.0001f)]
    [SerializeField] private float meterMaxSpeedMps = 50f;
    [Tooltip("Maximum length of the meter (local-units along the chosen axis).")]
    [Min(0f)]
    [SerializeField] private float meterMaxUnits = 1.0f;
    [Tooltip("If true, kinematic balls won't move the meter (useful while the ball is 'held').")]
    [SerializeField] private bool meterFreezeWhileKinematic = true;
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

    [Header("Frenzy Color")]
    [Tooltip("Color of the meter when frenzy (drop-target 2x) mode is active. Should match frenzy lights in the scene.")]
    [SerializeField] private Color frenzyMeterColor = new Color(0f, 0.85f, 1f, 1f);

    [Header("Color Application (debug / compatibility)")]
    [Tooltip("If enabled, also writes colors directly to Renderer.material. Use if your shader ignores MaterialPropertyBlock.")]
    [SerializeField] private bool forceMaterialColorUpdates = true;

    [Header("Debug")]
    [SerializeField] private bool debugInText = true;

    private Rigidbody _activeRb;
    private GameObject _activeBall;

    // Meter baseline state (so we can anchor one end while scaling).
    private Vector3 _meterBaseLocalScale;
    private float _meterMeshUnitSize = 1f; // mesh length along axis at scale = 1
    private bool _meterInit;
    private Transform _meterAnchor; // pivot parent at bar's "bottom" - prevents drift
    private float _meterUnitsSmoothed;
    private Renderer _meterRenderer;
    private MaterialPropertyBlock _meterMPB;
    private float _meterBaseAlpha = 1f;
    private Material _meterMaterialInstance;

    private void Awake()
    {
        ResolveRefs();
        InitMeterIfNeeded();
    }

    private void Start()
    {
        ServiceLocator.Get<AudioManager>()?.StartRollingSound();
    }

    private void ResolveRefs()
    {
        if (!speedText && autoFindTextInChildren)
            speedText = GetComponentInChildren<TMP_Text>(includeInactive: true);

        if (!speedText && autoFindTextByName && !string.IsNullOrWhiteSpace(speedTextObjectName))
        {
            var go = GameObject.Find(speedTextObjectName);
            if (go) speedText = go.GetComponent<TMP_Text>();
        }

        if (!ballSpawner)
            ballSpawner = ServiceLocator.Get<BallSpawner>();

        if (!gameRules)
            gameRules = ServiceLocator.Get<GameRulesManager>();

        if (!scoreManager)
            scoreManager = ServiceLocator.Get<ScoreManager>();

        if (!dropTargetsScoringMode)
            dropTargetsScoringMode = FindAnyObjectByType<DropTargetsScoringMode>();

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

    private void InitMeterIfNeeded()
    {
        if (_meterInit || !meterFill)
            return;

        _meterInit = true;
        _meterBaseLocalScale = meterFill.localScale;
        _meterMeshUnitSize = Mathf.Max(0.0001f, GetMeshUnitSizeAlongAxis(meterFill, meterAxis));
        _meterUnitsSmoothed = 0f;

        // Pivot-wrapper: create anchor at bar's "bottom", reparent meter fill so it scales from that end without drift.
        Vector3 axisDir = GetLocalAxisDir(meterAxis) * (meterPositiveDirection ? 1f : -1f);
        float baseScaleAxis = GetAxisValue(_meterBaseLocalScale, meterAxis);
        float baseLen = _meterMeshUnitSize * baseScaleAxis;
        Vector3 worldAxisDir = meterFill.TransformDirection(axisDir);
        Vector3 anchorWorldPos = meterFill.position - worldAxisDir * (baseLen * 0.5f);

        var anchorGo = new GameObject(meterFill.name + "_Anchor");
        anchorGo.transform.SetParent(meterFill.parent, worldPositionStays: false);
        anchorGo.transform.position = anchorWorldPos;
        anchorGo.transform.rotation = meterFill.rotation;
        _meterAnchor = anchorGo.transform;

        meterFill.SetParent(_meterAnchor, worldPositionStays: true);

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

    private void Update()
    {
        if (!speedText)
        {
            ResolveRefs();
            if (!speedText) return;
        }

        if (meterFill && !_meterInit)
            InitMeterIfNeeded();

        GameObject ball = GetActiveBall();
        if (ball != _activeBall)
        {
            _activeBall = ball;
            _activeRb = _activeBall ? _activeBall.GetComponent<Rigidbody>() : null;
        }

        if (!_activeBall || !_activeRb)
        {
            speedText.text = debugInText ? "Speed: (no active ball)" : "0.00 m/s";
            // Still drive meter from mult even when ball is absent.
            float idleMult = scoreManager ? scoreManager.Mult : 1f;
            float idleCap = (scoreManager && scoreManager.MultCap < float.MaxValue) ? scoreManager.MultCap : 10f;
            UpdateMeter(Mathf.Clamp01((idleMult - 1f) / Mathf.Max(0.0001f, idleCap - 1f)), kinematic: true);
            return;
        }

        bool kinematic = _activeRb.isKinematic;
        float speed = GetSpeedMps(_activeRb);
        if (freezeWhileKinematic && kinematic)
            speed = 0f;

        // Meter fill is driven by multiplier, not speed.
        float mult = scoreManager ? scoreManager.Mult : 1f;
        float multCap = (scoreManager && scoreManager.MultCap < float.MaxValue) ? scoreManager.MultCap : 10f;
        float multFill01 = Mathf.Clamp01((mult - 1f) / Mathf.Max(0.0001f, multCap - 1f));
        UpdateMeter(multFill01, kinematic);

        string fmt = "F" + Mathf.Clamp(decimals, 0, 6);
        string units = showUnits ? " m/s" : "";

        if (!debugInText)
        {
            speedText.text = $"{speed.ToString(fmt)}{units}";
            return;
        }

        Vector3 v = GetVelocity(_activeRb);
        speedText.text =
            $"{speed.ToString(fmt)}{units}\n" +
            $"ball={_activeBall.name}\n" +
            $"kinematic={kinematic}, sleeping={_activeRb.IsSleeping()}\n" +
            $"v=({v.x.ToString(fmt)},{v.y.ToString(fmt)},{v.z.ToString(fmt)})";
    }

    private void UpdateMeter(float fill01, bool kinematic)
    {
        if (!meterFill)
            return;

        if (!_meterInit)
            InitMeterIfNeeded();

        if (!_meterInit)
            return;

        if (meterFreezeWhileKinematic && kinematic)
            fill01 = 0f;

        float targetUnits = Mathf.Max(0f, meterMaxUnits) * Mathf.Clamp01(fill01);

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

        // Pivot-wrapper: anchor is at bar's bottom; meter fill's local position keeps bottom fixed.
        Vector3 axisDir = GetLocalAxisDir(meterAxis) * (meterPositiveDirection ? 1f : -1f);
        float newLen = _meterMeshUnitSize * targetScaleAxis;

        Vector3 newScale = _meterBaseLocalScale;
        newScale = SetAxisValue(newScale, meterAxis, targetScaleAxis);

        meterFill.localScale = newScale;
        meterFill.localPosition = axisDir * (newLen * 0.5f);

        // Drive fill color based on current fill percent (use smoothed units to match visuals).
        float smoothedFill01 = meterMaxUnits > 0.0001f ? Mathf.Clamp01(_meterUnitsSmoothed / meterMaxUnits) : 0f;
        UpdateMeterColor(smoothedFill01);

        ServiceLocator.Get<AudioManager>()?.UpdateRollingSound(smoothedFill01);
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

        bool frenzyActive = scoreManager != null ? scoreManager.IsFrenzyActive :
                            (dropTargetsScoringMode != null && dropTargetsScoringMode.IsFrenzyActive);
        Color c = frenzyActive ? frenzyMeterColor : EvaluateMeterColor(fill01);
        c.a = _meterBaseAlpha;

        _meterRenderer.GetPropertyBlock(_meterMPB);
        _meterMPB.SetColor(ColorId, c);
        _meterMPB.SetColor(BaseColorId, c);
        _meterMPB.SetColor(TintColorId, c);
        _meterMPB.SetColor(MainColorId, c);
        _meterMPB.SetColor(UnlitColorId, c);
        _meterMPB.SetColor(EmissionColorId, c);
        _meterMPB.SetColor(EmissiveColorId, c);
        _meterRenderer.SetPropertyBlock(_meterMPB);

        if (forceMaterialColorUpdates)
        {
            if (!_meterMaterialInstance && _meterRenderer)
                _meterMaterialInstance = _meterRenderer.material;
            if (_meterMaterialInstance)
            {
                SetColorIfPresent(_meterMaterialInstance, ColorId, c);
                SetColorIfPresent(_meterMaterialInstance, BaseColorId, c);
                SetColorIfPresent(_meterMaterialInstance, TintColorId, c);
                SetColorIfPresent(_meterMaterialInstance, MainColorId, c);
                SetColorIfPresent(_meterMaterialInstance, UnlitColorId, c);
                SetColorIfPresent(_meterMaterialInstance, EmissionColorId, c);
                SetColorIfPresent(_meterMaterialInstance, EmissiveColorId, c);
            }
        }
    }

    private static void SetColorIfPresent(Material m, int propertyId, Color c)
    {
        if (m != null && m.HasProperty(propertyId))
            m.SetColor(propertyId, c);
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

    private GameObject GetActiveBall()
    {
        if (ballSpawner)
        {
            var balls = ballSpawner.ActiveBalls;
            if (balls != null && balls.Count != 0)
            {
                for (int i = 0; i < balls.Count; i++)
                {
                    if (balls[i])
                        return balls[i];
                }
            }
        }
            
        if (gameRules)
        {
            var balls = gameRules.ActiveBalls;
            if (balls != null && balls.Count != 0)
            {
                for (int i = 0; i < balls.Count; i++)
                {
                    if (balls[i])
                        return balls[i];
                }
            }
        }
            

        return null;
    }

    private static float GetSpeedMps(Rigidbody rb)
    {
        return GetVelocity(rb).magnitude;
    }

    private static Vector3 GetVelocity(Rigidbody rb)
    {
        return rb ? rb.linearVelocity : Vector3.zero;
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

    private void OnDestroy()
    {
        ServiceLocator.Get<AudioManager>()?.StopRollingSound();
    }
}