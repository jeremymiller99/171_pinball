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
    [Tooltip("Anchor for the 'hand' line of balls (balls remaining).")]
    [SerializeField] private Transform handAnchor;
    [Tooltip("Spacing between balls in the hand line.")]
    [SerializeField] private float handSpacing = 0.35f;
    [Tooltip("World-space direction for the hand line. Default is left->right (+X).")]
    [SerializeField] private Vector3 handDirection = Vector3.right;
    [Tooltip("If true, balls in the hand will be rotated to match the handAnchor (if provided).")]
    [SerializeField] private bool alignHandRotationToAnchor = true;

    [Header("Turn transition")]
    [SerializeField] private float moveDuration = 0.35f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("If true, physics is disabled while moving a ball to the spawn point to prevent accidental collisions.")]
    [SerializeField] private bool disablePhysicsWhileMoving = true;

    [Header("Safety")]
    [SerializeField] private bool enforceSingleActiveBall = true;

    private readonly List<GameObject> _handBalls = new List<GameObject>();
    private readonly Dictionary<int, RigidbodyState> _rbStateById = new Dictionary<int, RigidbodyState>();

    private Coroutine _moveCoroutine;
    private GameObject _activeBall;

    public GameObject ActiveBall => _activeBall;
    public int HandCount => _handBalls.Count;

    /// <summary>
    /// Allows the gameplay core to rebind the spawn point when a new board scene is loaded.
    /// </summary>
    public void SetSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
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

        Destroy(ball);
    }

    private GameObject SpawnNewBallForHand()
    {
        if (ballPrefab == null)
        {
            Debug.LogWarning($"{nameof(BallSpawner)} missing ballPrefab.", this);
            return null;
        }

        Vector3 pos = GetHandBallWorldPos(_handBalls.Count);
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

        Vector3 pos = GetHandBallWorldPos(index);
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
        for (int i = 0; i < _handBalls.Count; i++)
        {
            var b = _handBalls[i];
            if (b == null) continue;

            b.transform.position = GetHandBallWorldPos(i);
            if (alignHandRotationToAnchor && handAnchor != null)
            {
                b.transform.rotation = handAnchor.rotation;
            }
        }
    }

    private Vector3 GetHandBallWorldPos(int index)
    {
        Vector3 anchorPos = handAnchor != null ? handAnchor.position : transform.position;
        Vector3 dir = handDirection.sqrMagnitude > 0.0001f ? handDirection.normalized : Vector3.right;
        return anchorPos + dir * (handSpacing * index);
    }

    private Quaternion GetHandBallWorldRot()
    {
        if (alignHandRotationToAnchor && handAnchor != null)
        {
            return handAnchor.rotation;
        }

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

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float eased = moveCurve != null ? moveCurve.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);

            ball.transform.position = Vector3.LerpUnclamped(startPos, endPos, eased);
            ball.transform.rotation = Quaternion.SlerpUnclamped(startRot, endRot, eased);

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
}

