using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns a visible "hand" of balls in a left-to-right line, and promotes the next ball
/// by lerping it to the actual spawn point when it's that ball's turn.
///
/// This is 3D-physics friendly (Rigidbody/Collider).
/// </summary>
public sealed class BallSpawner : MonoBehaviour
{
    [Header("Prefabs / Points")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Hand (balls remaining visual)")]
    [Tooltip("Spacing between balls in the hand line.")]
    [SerializeField] private float handSpacing = 0.35f;
    [Tooltip("World-space direction for the hand line. Default is left->right (+X).")]
    [SerializeField] private Vector3 handDirection = Vector3.right;

    [Header("Hand path (optional)")]
    [Tooltip("If set (or waypoints exist), hand balls will be placed along a path: start -> waypoints -> spawnPoint.")]
    [SerializeField] private Transform handPathStart;
    [Tooltip("Optional corner points for the hand path (in order).")]
    [SerializeField] private List<Transform> handPathWaypoints = new List<Transform>();
    [Tooltip("If true, activating the next ball will move it along the hand path to spawnPoint (instead of straight-line lerp).")]
    [SerializeField] private bool activateAlongHandPath = true;
    [Tooltip("If true, the hand balls will be evenly distributed along the path from end->start.\n" +
             "Index 0 (next ball) is closest to the end/spawnPoint, and the last ball is closest to the start.")]
    [SerializeField] private bool distributeHandEvenlyAlongPath = true;
    [Tooltip("Distance (world units) to keep the next ball away from the end/spawnPoint while it is still in the hand.\n" +
             "Useful if the spawnPoint area has triggers/colliders you don't want 'hand' balls to overlap.")]
    [Min(0f)]
    [SerializeField] private float handEndInset = 0f;

    [Header("Hand distribution tuning (queue lane)")]
    [Tooltip("Only used when there are at least 2 waypoints.\n" +
             "The queue lane is HandPathWaypoints[0] -> HandPathWaypoints[1].\n" +
             "Hand balls are distributed within that segment so they don't stack on the same point.")]
    [Min(0f)]
    [SerializeField] private float queueInsetFromStart = 0f;

    [Min(0f)]
    [SerializeField] private float queueInsetFromEnd = 0f;

    [Tooltip("Shifts ALL hand balls along the queue lane (+ moves toward the queue end, - toward the queue start).")]
    [SerializeField] private float queueGlobalOffset = 0f;

    [Tooltip("0 = even spacing (uses Queue Distribution Curve).\n" +
             "> 0 = fixed spacing in world units (measured from the queue end backwards).")]
    [Min(0f)]
    [SerializeField] private float queueFixedSpacing = 0f;

    [Tooltip("Controls spacing bias along the queue lane when Queue Fixed Spacing = 0.\n" +
             "X=ball index normalized (0..1), Y=position normalized (0..1).")]
    [SerializeField] private AnimationCurve queueDistributionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Turn transition")]
    [SerializeField] private float moveDuration = 0.35f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("If true, physics is disabled while moving a ball to the spawn point to prevent accidental collisions.")]
    [SerializeField] private bool disablePhysicsWhileMoving = true;

    [Header("Safety")]
    [SerializeField] private bool enforceSingleActiveBall = true;

    private readonly List<GameObject> _handBalls = new List<GameObject>();
    private readonly Dictionary<int, RigidbodyState> _rbStateById = new Dictionary<int, RigidbodyState>();
    private readonly Dictionary<int, float> _handDistanceById = new Dictionary<int, float>();

    private Coroutine _moveCoroutine;
    private GameObject _activeBall;

    public GameObject ActiveBall => _activeBall;
    public int HandCount => _handBalls.Count;
    public GameObject DefaultBallPrefab => ballPrefab;

    /// <summary>
    /// Allows the gameplay core to rebind the spawn point when a new board scene is loaded.
    /// </summary>
    public void SetSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
        if (_handBalls.Count > 0)
            LayoutHandImmediate();
    }

    /// <summary>
    /// Sets the polyline path for laying out the hand and (optionally) moving active balls to spawn.
    /// End point is always <see cref="spawnPoint"/>.
    /// </summary>
    public void SetHandPath(Transform start, IList<Transform> waypoints)
    {
        handPathStart = start;
        handPathWaypoints.Clear();
        if (waypoints != null)
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] != null)
                    handPathWaypoints.Add(waypoints[i]);
            }
        }
        if (_handBalls.Count > 0)
            LayoutHandImmediate();
    }

    private struct RigidbodyState
    {
        public bool hasRb;
        public bool isKinematic;
        public bool useGravity;
        public bool detectCollisions;
        public RigidbodyConstraints constraints;
        public RigidbodyInterpolation interpolation;
        public CollisionDetectionMode collisionDetectionMode;
    }

    /// <summary>
    /// Clears all spawned balls (hand + active).
    /// </summary>
    public void ClearAll()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }

        if (_activeBall != null)
        {
            Destroy(_activeBall);
            _activeBall = null;
        }

        for (int i = _handBalls.Count - 1; i >= 0; i--)
        {
            if (_handBalls[i] != null)
            {
                Destroy(_handBalls[i]);
            }
        }

        _handBalls.Clear();
        _rbStateById.Clear();
        _handDistanceById.Clear();
    }

    /// <summary>
    /// Spawns (or rebuilds) the visible hand line to exactly <paramref name="count"/> balls.
    /// Does not automatically activate a ball.
    /// </summary>
    public void BuildHand(int count)
    {
        count = Mathf.Max(0, count);

        // Shrink
        while (_handBalls.Count > count)
        {
            var b = _handBalls[_handBalls.Count - 1];
            _handBalls.RemoveAt(_handBalls.Count - 1);
            if (b != null)
            {
                _rbStateById.Remove(b.GetInstanceID());
                Destroy(b);
            }
        }

        // Grow
        while (_handBalls.Count < count)
        {
            var b = SpawnNewBallForHand();
            if (b == null) break;
            _handBalls.Add(b);
        }

        LayoutHandImmediate();
    }

    /// <summary>
    /// Builds the visible hand from an explicit list of prefabs (one prefab per hand slot).
    /// </summary>
    public void BuildHandFromPrefabs(IList<GameObject> prefabs)
    {
        if (prefabs == null)
        {
            BuildHand(0);
            return;
        }

        // Clear tracked hand objects first (but keep active ball unchanged; the caller typically ClearAll()s).
        for (int i = _handBalls.Count - 1; i >= 0; i--)
        {
            var b = _handBalls[i];
            if (b != null)
            {
                _rbStateById.Remove(b.GetInstanceID());
                Destroy(b);
            }
        }
        _handBalls.Clear();

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] == null) continue;
            var b = SpawnNewBallForHand(prefabs[i], i);
            if (b != null)
            {
                _handBalls.Add(b);
            }
        }

        LayoutHandImmediate();
    }

    /// <summary>
    /// Activates the next ball: removes it from the hand, then lerps it to the spawn point.
    /// Returns the ball GameObject (may still be moving).
    /// </summary>
    public GameObject ActivateNextBall()
    {
        if (spawnPoint == null)
        {
            Debug.LogWarning($"{nameof(BallSpawner)} missing spawnPoint.", this);
            return null;
        }

        if (_handBalls.Count == 0)
        {
            // If no hand has been built, fall back to spawning one directly at spawn.
            return SpawnBallAtSpawnPoint();
        }

        if (enforceSingleActiveBall && _activeBall != null)
        {
            DespawnBall(_activeBall);
        }

        GameObject next = _handBalls[0];
        _handBalls.RemoveAt(0);
        LayoutHandImmediate();

        _activeBall = next;

        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }

        _moveCoroutine = StartCoroutine(MoveBallToSpawnPointCoroutine(next));
        return next;
    }

    /// <summary>
    /// Destroys a ball and removes it from tracking (hand or active).
    /// </summary>
    public void DespawnBall(GameObject ball)
    {
        if (ball == null) return;

        if (ball == _activeBall)
        {
            _activeBall = null;
        }

        _handBalls.Remove(ball);
        _rbStateById.Remove(ball.GetInstanceID());
        _handDistanceById.Remove(ball.GetInstanceID());

        Destroy(ball);
    }

    private GameObject SpawnNewBallForHand()
    {
        if (ballPrefab == null)
        {
            Debug.LogWarning($"{nameof(BallSpawner)} missing ballPrefab.", this);
            return null;
        }

        int index = _handBalls.Count;
        float dist = handSpacing * index;
        Vector3 pos = GetHandBallWorldPos(index, dist);
        Quaternion rot = GetHandBallWorldRot();

        GameObject b = Instantiate(ballPrefab, pos, rot);
        b.name = $"{ballPrefab.name}_HandBall_{_handBalls.Count + 1}";

        CacheAndConfigureAsHandBall(b);
        return b;
    }

    private GameObject SpawnNewBallForHand(GameObject prefab, int index)
    {
        if (prefab == null)
        {
            return null;
        }

        float dist = handSpacing * index;
        Vector3 pos = GetHandBallWorldPos(index, dist);
        Quaternion rot = GetHandBallWorldRot();

        GameObject b = Instantiate(prefab, pos, rot);
        b.name = $"{prefab.name}_HandBall_{index + 1}";

        CacheAndConfigureAsHandBall(b);
        return b;
    }

    private GameObject SpawnBallAtSpawnPoint()
    {
        if (ballPrefab == null)
        {
            Debug.LogWarning($"{nameof(BallSpawner)} missing ballPrefab.", this);
            return null;
        }

        if (enforceSingleActiveBall && _activeBall != null)
        {
            DespawnBall(_activeBall);
        }

        _activeBall = Instantiate(ballPrefab, spawnPoint.position, spawnPoint.rotation);
        _activeBall.name = $"{ballPrefab.name}_ActiveBall";
        return _activeBall;
    }

    private void LayoutHandImmediate()
    {
        // Path layout: spread balls from end->start so "next ball" sits closest to the spawn point.
        if (TryGetHandPathPoints(out var pts))
        {
            float totalLen = GetPolylineLength(pts);
            float endDist = Mathf.Max(0f, totalLen - Mathf.Max(0f, handEndInset));
            int n = _handBalls.Count;

            // If the board provides at least TWO waypoints, treat the segment waypoint[0] -> waypoint[1]
            // as the "queue segment" and distribute ALL hand balls within that segment.
            // This prevents overlap with the active ball at the end/spawn and avoids multiple balls snapping to the same waypoint.
            bool hasQueueSegment = handPathWaypoints.Count >= 2 && pts.Count >= 4; // start + wp0 + wp1 + end
            float queueA = 0f;
            float queueB = 0f;
            if (hasQueueSegment)
            {
                // pts indices: 0=start, 1=waypoint[0], 2=waypoint[1], ..., last=end/spawn
                var cum = new float[pts.Count];
                for (int p = 1; p < pts.Count; p++)
                {
                    cum[p] = cum[p - 1] + Vector3.Distance(pts[p - 1], pts[p]);
                }

                queueA = cum[1]; // waypoint[0]
                queueB = cum[2]; // waypoint[1]

                // Clamp away from end/spawn just in case (should already be before the end).
                queueA = Mathf.Clamp(queueA, 0f, endDist);
                queueB = Mathf.Clamp(queueB, 0f, endDist);

                // Ensure A <= B so our lerp is stable even if points were authored "backwards".
                if (queueA > queueB)
                {
                    (queueA, queueB) = (queueB, queueA);
                }

                // Apply insets (padding) within the queue lane.
                queueA = Mathf.Clamp(queueA + Mathf.Max(0f, queueInsetFromStart), 0f, endDist);
                queueB = Mathf.Clamp(queueB - Mathf.Max(0f, queueInsetFromEnd), 0f, endDist);
                if (queueA > queueB)
                {
                    // Degenerate queue lane after insets; collapse to a single safe point.
                    float mid = (queueA + queueB) * 0.5f;
                    queueA = mid;
                    queueB = mid;
                }
            }

            for (int i = 0; i < n; i++)
            {
                var b = _handBalls[i];
                if (b == null) continue;

                float dist;
                if (distributeHandEvenlyAlongPath)
                {
                    if (hasQueueSegment)
                    {
                        // Spread within queue segment: i=0 closest to queueB (queue end), i=n-1 closest to queueA (queue start).
                        if (queueFixedSpacing > 0f)
                        {
                            dist = queueB - (queueFixedSpacing * i);
                        }
                        else
                        {
                            float t = n <= 1 ? 0f : (float)i / (n - 1);
                            float shaped = queueDistributionCurve != null ? queueDistributionCurve.Evaluate(Mathf.Clamp01(t)) : t;
                            dist = Mathf.Lerp(queueB, queueA, Mathf.Clamp01(shaped));
                        }

                        // Apply global offset along the queue lane, then clamp.
                        dist += queueGlobalOffset;
                        dist = Mathf.Clamp(dist, queueA, queueB);
                    }
                    else
                    {
                        // Fallback: even distribution along length (excluding the end).
                        // i=0 (next ball) => endDist, i=n-1 => 0. For n==1, place at the midpoint.
                        float t = n <= 1 ? 0.5f : (float)i / (n - 1);
                        dist = Mathf.Lerp(endDist, 0f, t);
                    }
                }
                else
                {
                    // Legacy spacing measured from start.
                    dist = Mathf.Clamp(handSpacing * i, 0f, totalLen);
                }

                // Never allow a hand ball to be placed at/inside the end/spawn area.
                dist = Mathf.Min(dist, endDist);
                b.transform.position = SamplePolyline(pts, dist);

                _handDistanceById[b.GetInstanceID()] = dist;
            }

            return;
        }

        for (int i = 0; i < _handBalls.Count; i++)
        {
            var b = _handBalls[i];
            if (b == null) continue;

            float dist = handSpacing * i;
            b.transform.position = GetHandBallWorldPos(i, dist);

            _handDistanceById[b.GetInstanceID()] = dist;
        }
    }

    private Vector3 GetHandBallWorldPos(int index, float distance)
    {
        if (TryGetHandPathPoints(out var pts))
        {
            return SamplePolyline(pts, distance);
        }

        Vector3 anchorPos = transform.position;
        Vector3 dir = handDirection.sqrMagnitude > 0.0001f ? handDirection.normalized : Vector3.right;
        return anchorPos + dir * (handSpacing * index);
    }

    private Quaternion GetHandBallWorldRot()
    {
        return Quaternion.identity;
    }

    private void CacheAndConfigureAsHandBall(GameObject ball)
    {
        if (ball == null) return;

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        int id = ball.GetInstanceID();

        if (rb != null)
        {
            _rbStateById[id] = new RigidbodyState
            {
                hasRb = true,
                isKinematic = rb.isKinematic,
                useGravity = rb.useGravity,
                detectCollisions = rb.detectCollisions,
                constraints = rb.constraints,
                interpolation = rb.interpolation,
                collisionDetectionMode = rb.collisionDetectionMode
            };

            // Park in hand: no physics interactions.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        else
        {
            _rbStateById[id] = new RigidbodyState { hasRb = false };
        }
    }

    private void RestoreRigidbodyForActive(GameObject ball)
    {
        if (ball == null) return;

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb == null) return;

        int id = ball.GetInstanceID();
        if (_rbStateById.TryGetValue(id, out var state) && state.hasRb)
        {
            rb.isKinematic = state.isKinematic;
            rb.useGravity = state.useGravity;
            rb.detectCollisions = state.detectCollisions;
            rb.constraints = state.constraints;
            rb.interpolation = state.interpolation;
            rb.collisionDetectionMode = state.collisionDetectionMode;
        }
        else
        {
            // Reasonable defaults if we don't have cached state.
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }

        // Ensure the physics body matches the final transform we lerped to.
        rb.position = ball.transform.position;
        rb.rotation = ball.transform.rotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.WakeUp();
    }

    private IEnumerator MoveBallToSpawnPointCoroutine(GameObject ball)
    {
        if (ball == null || spawnPoint == null)
        {
            yield break;
        }

        List<Vector3> pts = null;
        bool usePath = false;
        if (activateAlongHandPath && TryGetHandPathPoints(out var tmpPts) && tmpPts.Count >= 2)
        {
            pts = tmpPts;
            usePath = true;
        }

        float startDist = 0f;
        if (usePath)
        {
            // If we tracked this ball as part of the hand, start from its assigned distance.
            // Otherwise start from the beginning of the path.
            _handDistanceById.TryGetValue(ball.GetInstanceID(), out startDist);
        }

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        bool hadRb = rb != null;

        // Temporarily disable interactions so the travel doesn't bump into table geometry
        // AND so we can reliably trigger OnTriggerEnter at the end (e.g. PinballLauncher).
        Collider[] cols = null;
        bool[] prevColEnabled = null;

        if (disablePhysicsWhileMoving)
        {
            cols = ball.GetComponentsInChildren<Collider>(includeInactive: false);
            if (cols != null && cols.Length > 0)
            {
                prevColEnabled = new bool[cols.Length];
                for (int i = 0; i < cols.Length; i++)
                {
                    prevColEnabled[i] = cols[i] != null && cols[i].enabled;
                    if (cols[i] != null)
                    {
                        cols[i].enabled = false;
                    }
                }
            }

            if (hadRb)
            {
                // Hand balls often start as kinematic; setting velocities on kinematic RBs can warn.
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
            }
        }

        Vector3 startPos = ball.transform.position;
        Quaternion startRot = ball.transform.rotation;
        Vector3 endPos = spawnPoint.position;
        Quaternion endRot = spawnPoint.rotation;

        float dur = Mathf.Max(0.01f, moveDuration);
        float t = 0f;

        // Cache polyline total length (used for path movement).
        float totalLen = 0f;
        if (usePath)
        {
            totalLen = GetPolylineLength(pts);
        }

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float eased = moveCurve != null ? moveCurve.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);

            if (usePath)
            {
                float dist = Mathf.LerpUnclamped(startDist, totalLen, eased);
                ball.transform.position = SamplePolyline(pts, dist);
                ball.transform.rotation = endRot;
            }
            else
            {
                ball.transform.position = Vector3.LerpUnclamped(startPos, endPos, eased);
                ball.transform.rotation = Quaternion.SlerpUnclamped(startRot, endRot, eased);
            }

            yield return null;
        }

        ball.transform.position = endPos;
        ball.transform.rotation = endRot;

        // Restore original prefab RB settings (gravity/constraints/etc) for active play.
        RestoreRigidbodyForActive(ball);

        // Restore colliders after RB settings so trigger/collision callbacks fire cleanly.
        if (disablePhysicsWhileMoving && cols != null && prevColEnabled != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                {
                    cols[i].enabled = prevColEnabled[i];
                }
            }
            Physics.SyncTransforms();
        }
        _moveCoroutine = null;
    }

    private void OnDisable()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
    }

    private bool TryGetHandPathPoints(out List<Vector3> points)
    {
        points = null;
        if (spawnPoint == null)
            return false;

        // Enable path mode if either start is set or there are any waypoints set.
        bool hasAnyWaypoint = false;
        for (int i = 0; i < handPathWaypoints.Count; i++)
        {
            if (handPathWaypoints[i] != null)
            {
                hasAnyWaypoint = true;
                break;
            }
        }

        Transform startT = handPathStart != null ? handPathStart : transform;

        points = new List<Vector3>(2 + handPathWaypoints.Count)
        {
            startT.position
        };

        for (int i = 0; i < handPathWaypoints.Count; i++)
        {
            if (handPathWaypoints[i] != null)
                points.Add(handPathWaypoints[i].position);
        }

        points.Add(spawnPoint.position);

        // Remove consecutive duplicates (prevents zero-length segments).
        for (int i = points.Count - 2; i >= 0; i--)
        {
            if ((points[i + 1] - points[i]).sqrMagnitude < 0.0000001f)
                points.RemoveAt(i + 1);
        }

        return points.Count >= 2;
    }

    private static float GetPolylineLength(IList<Vector3> pts)
    {
        if (pts == null || pts.Count < 2) return 0f;
        float len = 0f;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            len += Vector3.Distance(pts[i], pts[i + 1]);
        }
        return len;
    }

    private static Vector3 SamplePolyline(IList<Vector3> pts, float distance)
    {
        if (pts == null || pts.Count == 0) return Vector3.zero;
        if (pts.Count == 1) return pts[0];

        distance = Mathf.Max(0f, distance);

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 a = pts[i];
            Vector3 b = pts[i + 1];
            float segLen = Vector3.Distance(a, b);
            if (segLen <= 0.000001f)
                continue;

            if (distance <= segLen)
            {
                float t = distance / segLen;
                return Vector3.LerpUnclamped(a, b, t);
            }

            distance -= segLen;
        }

        // If we ran past the end, extrapolate along the last valid segment.
        Vector3 last = pts[pts.Count - 1];
        Vector3 prev = pts[pts.Count - 2];
        Vector3 dir = (last - prev);
        if (dir.sqrMagnitude > 0.000001f)
        {
            dir.Normalize();
            return last + dir * distance;
        }
        return last;
    }
}

