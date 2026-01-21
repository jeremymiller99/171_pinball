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

    [Header("UI")]
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private bool autoFindTextByName = true;
    [SerializeField] private string speedTextObjectName = "BallSpeedText";

    [Header("Source")]
    [SerializeField] private GameRulesManager gameRules;
    [SerializeField] private BallSpawner ballSpawner;

    [Header("Behavior")]
    [Tooltip("If true, shows 0 until the active ball is non-kinematic.")]
    [SerializeField] private bool freezeWhileKinematic = true;
    [SerializeField] private int decimals = 2;
    [Tooltip("If true, appends ' m/s' to the numeric speed readout.")]
    [SerializeField] private bool showUnits = false;

    [Header("3D Meter (optional)")]
    [Tooltip("Assign a 3D box/cube transform that should 'fill' as speed increases.")]
    [SerializeField] private Transform meterFill;
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

    [Header("Debug")]
    [SerializeField] private bool debugInText = true;

    private Rigidbody _activeRb;
    private GameObject _activeBall;

    // Meter baseline state (so we can anchor one end while scaling).
    private Vector3 _meterBaseLocalScale;
    private Vector3 _meterBaseLocalPos;
    private float _meterMeshUnitSize = 1f; // mesh length along axis at scale = 1
    private bool _meterInit;
    private float _meterUnitsSmoothed;

    private void Awake()
    {
        ResolveRefs();
        InitMeterIfNeeded();
    }

    private void ResolveRefs()
    {
        if (!speedText && autoFindTextByName && !string.IsNullOrWhiteSpace(speedTextObjectName))
        {
            var go = GameObject.Find(speedTextObjectName);
            if (go) speedText = go.GetComponent<TMP_Text>();
        }

        if (!ballSpawner)
            ballSpawner = FindFirstObjectByType<BallSpawner>();

        if (!gameRules)
            gameRules = FindFirstObjectByType<GameRulesManager>();
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
            UpdateMeter(0f, kinematic: true);
            return;
        }

        bool kinematic = _activeRb.isKinematic;
        float speed = GetSpeedMps(_activeRb);
        if (freezeWhileKinematic && kinematic)
            speed = 0f;

        UpdateMeter(speed, kinematic);

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

    private void UpdateMeter(float speedMps, bool kinematic)
    {
        if (!meterFill)
            return;

        if (!_meterInit)
            InitMeterIfNeeded();

        if (!_meterInit)
            return;

        if (meterFreezeWhileKinematic && kinematic)
            speedMps = 0f;

        float targetUnits;
        if (meterMaxSpeedMps > 0f)
        {
            float t01 = Mathf.Clamp01(speedMps / meterMaxSpeedMps);
            targetUnits = Mathf.Max(0f, meterMaxUnits) * t01;
        }
        else
        {
            // Legacy fallback if someone sets meterMaxSpeedMps to 0 in the inspector.
            targetUnits = Mathf.Clamp(speedMps * Mathf.Max(0f, meterUnitsPerMps), 0f, Mathf.Max(0f, meterMaxUnits));
        }

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
    }

    private GameObject GetActiveBall()
    {
        if (ballSpawner && ballSpawner.ActiveBall)
            return ballSpawner.ActiveBall;

        if (gameRules && gameRules.ActiveBall)
            return gameRules.ActiveBall;

        return null;
    }

    private static float GetSpeedMps(Rigidbody rb)
    {
        return GetVelocity(rb).magnitude;
    }

    private static Vector3 GetVelocity(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb ? rb.linearVelocity : Vector3.zero;
#else
        return rb ? rb.velocity : Vector3.zero;
#endif
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
}

