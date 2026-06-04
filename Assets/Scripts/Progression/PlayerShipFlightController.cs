using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the spawned player ship's cinematic movement in the board scene.
///
/// Flow:
///  - On spawn: flies from the spawn point, through any mid-points, to a resting place.
///  - On shop open  (ShopTransitionController.OpenTransitionStarted):  rest -> shop position.
///  - On shop close (ShopTransitionController.CloseTransitionStarted): shop -> rest position.
///
/// The ship always points along its direction of travel. The idle hover
/// (<see cref="PlayerShipVisual"/>) is suspended while flying and re-anchored wherever
/// the ship parks, so it bobs around its new spot.
///
/// SETUP: put this on the same GameObject as <see cref="PlayerShipVisualSpawner"/>
/// (the "Player Ship Spawn" object) and assign the waypoint Transforms. The ship starts
/// at this object's transform. If no mid-points / rest point are assigned, the ship simply
/// hovers at the spawn point (legacy behavior).
/// </summary>
[RequireComponent(typeof(PlayerShipVisualSpawner))]
public class PlayerShipFlightController : MonoBehaviour
{
    [Header("Entry path (spawn -> mid-points -> resting place)")]
    [Tooltip("Optional mid-points the ship flies through after spawning, in order.")]
    [SerializeField] private List<Transform> midPoints = new List<Transform>();

    [Tooltip("Final resting place. If null, the last mid-point (or the spawn point) is used.")]
    [SerializeField] private Transform restPoint;

    [Tooltip("Total seconds for the whole entry flight (spawn -> rest). " +
             "Time is distributed across segments by length, so speed stays even.")]
    [SerializeField] private float entryDuration = 2.5f;

    [Tooltip("Eases the entry flight over its whole length (ease-in/out by default).")]
    [SerializeField] private AnimationCurve entryEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Shop flight (rest -> shop)")]
    [Tooltip("Where the ship parks while the shop is open.")]
    [SerializeField] private Transform shopPoint;

    [Tooltip("Optional mid-points flown through on the way OUT (rest -> shop), in order.")]
    [SerializeField] private List<Transform> shopMidPoints = new List<Transform>();

    [Tooltip("Seconds to fly from the resting place to the shop position.")]
    [SerializeField] private float shopDuration = 1f;

    [SerializeField] private AnimationCurve shopEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Return flight (shop -> rest)")]
    [Tooltip("Optional mid-points flown through on the way BACK (shop -> rest), in order. " +
             "Separate from the out-bound points for full control over each arc.")]
    [SerializeField] private List<Transform> returnMidPoints = new List<Transform>();

    [Tooltip("Seconds to fly from the shop position back to the resting place. " +
             "If <= 0, falls back to Shop Duration.")]
    [SerializeField] private float returnDuration = 0f;

    [SerializeField] private AnimationCurve returnEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Orientation")]
    [Tooltip("Extra rotation applied so the model's nose points along the flight direction. " +
             "Tune if the ship flies sideways or backwards.")]
    [SerializeField] private Vector3 modelForwardEuler = Vector3.zero;

    [Tooltip("How quickly the ship turns to face its travel direction. " +
             "Higher = snappier, lower = lazier/smoother. 0 = snap instantly.")]
    [SerializeField] private float rotationSharpness = 8f;

    [Header("Path smoothing")]
    [Tooltip("Subdivisions per segment used to round the path through mid-points (Catmull-Rom). " +
             "Higher = smoother curve; 1 = straight lines between points.")]
    [SerializeField, Min(1)] private int pathSmoothing = 12;

    [Header("Gizmos")]
    [SerializeField] private bool drawPathGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.9f, 1f, 1f);

    private const float MinSegmentSqr = 1e-6f;

    private Transform _ship;
    private PlayerShipVisual _visual;
    private ShopTransitionController _transition;
    private Coroutine _flightRoutine;

    /// <summary>
    /// Called by <see cref="PlayerShipVisualSpawner"/> once the ship instance exists.
    /// Suspends hover and starts the entry flight.
    /// </summary>
    public void Bind(Transform ship, PlayerShipVisual visual)
    {
        _ship = ship;
        _visual = visual;

        if (_visual != null)
        {
            _visual.SetHoverEnabled(false);
        }

        ResolveTransition();

        if (_flightRoutine != null)
        {
            StopCoroutine(_flightRoutine);
        }
        _flightRoutine = StartCoroutine(EntryFlight());
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void ResolveTransition()
    {
        if (_transition != null)
        {
            return;
        }

        _transition = ServiceLocator.Get<ShopTransitionController>();
        if (_transition == null)
        {
            _transition = FindFirstObjectByType<ShopTransitionController>();
        }

        if (_transition != null)
        {
            _transition.OpenTransitionStarted += OnShopOpen;
            _transition.CloseTransitionStarted += OnShopClose;
        }
    }

    private void Unsubscribe()
    {
        if (_transition != null)
        {
            _transition.OpenTransitionStarted -= OnShopOpen;
            _transition.CloseTransitionStarted -= OnShopClose;
            _transition = null;
        }
    }

    /// <summary>The resting place: explicit restPoint, else last mid-point, else the spawn point.</summary>
    private Transform RestTarget
    {
        get
        {
            if (restPoint != null)
            {
                return restPoint;
            }

            for (int i = midPoints.Count - 1; i >= 0; i--)
            {
                if (midPoints[i] != null)
                {
                    return midPoints[i];
                }
            }

            return transform;
        }
    }

    private void OnShopOpen()
    {
        if (_ship == null || shopPoint == null)
        {
            return;
        }

        StartLeg(shopMidPoints, shopPoint.position, shopDuration, shopEase);
    }

    private void OnShopClose()
    {
        if (_ship == null)
        {
            return;
        }

        Transform rest = RestTarget;
        if (rest == null)
        {
            return;
        }

        float duration = returnDuration > Mathf.Epsilon ? returnDuration : shopDuration;
        StartLeg(returnMidPoints, rest.position, duration, returnEase);
    }

    /// <summary>
    /// Flies from the ship's current position, through <paramref name="mids"/>, to
    /// <paramref name="target"/>. Used for both the out-bound (shop) and return (rest) legs.
    /// </summary>
    private void StartLeg(List<Transform> mids, Vector3 target, float duration, AnimationCurve ease)
    {
        if (_visual != null)
        {
            _visual.SetHoverEnabled(false);
        }

        if (_flightRoutine != null)
        {
            StopCoroutine(_flightRoutine);
        }

        var points = new List<Vector3> { _ship.position };
        if (mids != null)
        {
            for (int i = 0; i < mids.Count; i++)
            {
                if (mids[i] != null)
                {
                    points.Add(mids[i].position);
                }
            }
        }
        points.Add(target);

        _flightRoutine = StartCoroutine(FlyPolyline(points, duration, ease));
    }

    private IEnumerator EntryFlight()
    {
        // Build the entry polyline: spawn (current ship pos) -> mid-points -> rest.
        var points = new List<Vector3> { _ship.position };

        for (int i = 0; i < midPoints.Count; i++)
        {
            if (midPoints[i] != null)
            {
                points.Add(midPoints[i].position);
            }
        }

        if (restPoint != null)
        {
            points.Add(restPoint.position);
        }

        // No path authored -> just hover where we spawned.
        if (points.Count < 2)
        {
            ParkHere();
            yield break;
        }

        yield return FlyPolyline(points, entryDuration, entryEase);
    }

    /// <summary>
    /// Moves the ship along the polyline over <paramref name="duration"/> seconds, eased over the
    /// whole length and distance-weighted so speed is even across uneven segments. Points the ship
    /// along the current segment's direction. Parks (re-anchors hover) on arrival.
    /// </summary>
    private IEnumerator FlyPolyline(List<Vector3> controlPoints, float duration, AnimationCurve ease)
    {
        // Round the corners: resample the control points into a Catmull-Rom curve so the ship
        // arcs smoothly through the mid-points instead of cutting hard angles at each one.
        List<Vector3> points = BuildSmoothPath(controlPoints, pathSmoothing);

        // Cumulative arc length along the (smoothed) path.
        int count = points.Count;
        var cumulative = new float[count];
        cumulative[0] = 0f;
        for (int i = 1; i < count; i++)
        {
            cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }

        float total = cumulative[count - 1];
        if (total <= Mathf.Epsilon || duration <= Mathf.Epsilon)
        {
            _ship.position = points[count - 1];
            ParkHere();
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float n = Mathf.Clamp01(t / duration);
            float eased = ease != null ? ease.Evaluate(n) : n;
            float targetDist = Mathf.Clamp(eased * total, 0f, total);

            SamplePolyline(points, cumulative, targetDist, out Vector3 pos, out int seg);
            _ship.position = pos;
            OrientSmooth(points[seg + 1] - points[seg], dt);

            yield return null;
        }

        _ship.position = points[count - 1];
        ParkHere();
    }

    /// <summary>
    /// Resamples the control points into a smooth Catmull-Rom curve. The curve passes through
    /// every control point but rounds the path between them. Returns the input unchanged for
    /// 2 points or when smoothing is disabled.
    /// </summary>
    private static List<Vector3> BuildSmoothPath(List<Vector3> control, int subdivisions)
    {
        int n = control.Count;
        if (n <= 2 || subdivisions <= 1)
        {
            return new List<Vector3>(control);
        }

        var result = new List<Vector3>((n - 1) * subdivisions + 1);
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = control[Mathf.Max(0, i - 1)];
            Vector3 p1 = control[i];
            Vector3 p2 = control[i + 1];
            Vector3 p3 = control[Mathf.Min(n - 1, i + 2)];

            for (int s = 0; s < subdivisions; s++)
            {
                float t = s / (float)subdivisions;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        result.Add(control[n - 1]);
        return result;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    /// <summary>Finds the world position at <paramref name="dist"/> along the polyline and the segment index it lies on.</summary>
    private static void SamplePolyline(List<Vector3> points, float[] cumulative, float dist, out Vector3 pos, out int seg)
    {
        int last = points.Count - 1;
        for (int i = 1; i <= last; i++)
        {
            if (dist <= cumulative[i] || i == last)
            {
                float segLen = cumulative[i] - cumulative[i - 1];
                float f = segLen > Mathf.Epsilon ? (dist - cumulative[i - 1]) / segLen : 0f;
                pos = Vector3.LerpUnclamped(points[i - 1], points[i], f);
                seg = i - 1;
                return;
            }
        }

        pos = points[last];
        seg = Mathf.Max(0, last - 1);
    }

    /// <summary>
    /// Eases the ship's heading toward its travel direction. Uses frame-rate-independent
    /// exponential smoothing so turns round off instead of snapping at each waypoint.
    /// </summary>
    private void OrientSmooth(Vector3 dir, float dt)
    {
        if (dir.sqrMagnitude <= MinSegmentSqr)
        {
            return;
        }

        Quaternion target = Quaternion.LookRotation(dir) * Quaternion.Euler(modelForwardEuler);

        if (rotationSharpness <= 0f)
        {
            _ship.rotation = target;
            return;
        }

        float blend = 1f - Mathf.Exp(-rotationSharpness * dt);
        _ship.rotation = Quaternion.Slerp(_ship.rotation, target, blend);
    }

    /// <summary>Re-anchors the idle hover to the ship's current spot and re-enables it.</summary>
    private void ParkHere()
    {
        _flightRoutine = null;
        if (_visual != null)
        {
            _visual.ReanchorHere();
            _visual.SetHoverEnabled(true);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawPathGizmos)
        {
            return;
        }

        Gizmos.color = gizmoColor;

        // Entry path.
        Vector3 prev = transform.position;
        Gizmos.DrawWireSphere(prev, 0.4f);
        for (int i = 0; i < midPoints.Count; i++)
        {
            if (midPoints[i] == null)
            {
                continue;
            }
            Vector3 p = midPoints[i].position;
            Gizmos.DrawLine(prev, p);
            Gizmos.DrawWireSphere(p, 0.4f);
            prev = p;
        }

        if (restPoint != null)
        {
            Gizmos.DrawLine(prev, restPoint.position);
            Gizmos.DrawWireSphere(restPoint.position, 0.6f);
            prev = restPoint.position;
        }

        Vector3 rest = prev;

        // Out-bound shop leg (rest -> shopMidPoints -> shop), drawn in warm color.
        if (shopPoint != null)
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 1f);
            DrawLeg(rest, shopMidPoints, shopPoint.position);
        }

        // Return leg (shop -> returnMidPoints -> rest), drawn in green.
        if (shopPoint != null)
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 1f);
            DrawLeg(shopPoint.position, returnMidPoints, rest);
        }
    }

    private static void DrawLeg(Vector3 start, List<Transform> mids, Vector3 end)
    {
        Vector3 prev = start;
        if (mids != null)
        {
            for (int i = 0; i < mids.Count; i++)
            {
                if (mids[i] == null)
                {
                    continue;
                }
                Vector3 p = mids[i].position;
                Gizmos.DrawLine(prev, p);
                Gizmos.DrawWireSphere(p, 0.35f);
                prev = p;
            }
        }
        Gizmos.DrawLine(prev, end);
    }
}
