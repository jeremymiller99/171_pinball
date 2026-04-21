using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns a visible "hand" of balls at fixed slot transforms, and promotes the next ball
/// by lerping it from its slot to the launch/spawn point when it's that ball's turn.
///
/// Each slot is a scene GameObject with a <see cref="BallHandSlot"/> + BoxCollider trigger.
/// Slots are drop targets for shop drag-and-drop.
/// </summary>
public sealed class BallSpawner : MonoBehaviour
{
    [Header("Prefabs / Points")]
    [SerializeField] private GameObject ballPrefab;
    [Tooltip("Launch point: where the active ball is fired from.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Hand slots")]
    [Tooltip("Ordered slot GameObjects for the player's hand. Index 0 is the ball next to launch. " +
             "Each slot should have a BallHandSlot + BoxCollider (trigger) for drop targeting.")]
    [SerializeField] private List<BallHandSlot> handSlots = new List<BallHandSlot>();

    [Header("Turn transition")]
    [SerializeField] private float moveDuration = 0.35f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("If true, physics is disabled while moving a ball to the spawn point to prevent accidental collisions.")]
    [SerializeField] private bool disablePhysicsWhileMoving = true;

    [Header("Safety")]
    [SerializeField] private bool enforceSingleActiveBall = false;

    [Header("Shop animations")]
    [SerializeField] private float layoutAnimDuration = 0.25f;

    private readonly List<GameObject> _handBalls = new List<GameObject>();
    private readonly Dictionary<int, RigidbodyState> _rbStateById = new Dictionary<int, RigidbodyState>();
    private readonly Dictionary<int, ColliderState[]> _colStateById = new Dictionary<int, ColliderState[]>();

    private Coroutine _moveCoroutine;
    private Coroutine _layoutCoroutine;
    private int _previewGapIndex = -1;
    private bool _isGapPreviewActive;
    private List<GameObject> _activeBalls = new List<GameObject>();

    public List<GameObject> ActiveBalls => _activeBalls;
    public IReadOnlyList<GameObject> HandBalls => _handBalls;
    public int HandCount => _handBalls.Count;
    public int SlotCount => handSlots != null ? handSlots.Count : 0;
    public IReadOnlyList<BallHandSlot> HandSlots => handSlots;
    public GameObject DefaultBallPrefab => ballPrefab;
    public Transform SpawnPoint => spawnPoint;

    private void Awake()
    {
        ServiceLocator.Register<BallSpawner>(this);
        EnsureHandSlotsResolved();
    }

    private void OnDisable()
    {
        ServiceLocator.Unregister<BallSpawner>();
        CancelLayoutAnimation();

        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
    }

    private void AssignSlotIndices()
    {
        if (handSlots == null) return;
        for (int i = 0; i < handSlots.Count; i++)
        {
            if (handSlots[i] != null) handSlots[i].AssignSlotIndex(i);
        }
    }

    /// <summary>
    /// Ensures the slot list is populated and valid. Drops null/destroyed entries,
    /// auto-discovers BallHandSlot children if the list is empty, then falls back
    /// to a scene-wide search. Safe to call multiple times.
    /// </summary>
    private void EnsureHandSlotsResolved()
    {
        if (handSlots == null) handSlots = new List<BallHandSlot>();

        for (int i = handSlots.Count - 1; i >= 0; i--)
        {
            if (handSlots[i] == null) handSlots.RemoveAt(i);
        }

        if (handSlots.Count == 0)
        {
            var children = GetComponentsInChildren<BallHandSlot>(includeInactive: true);
            if (children != null && children.Length > 0)
            {
                handSlots.AddRange(children);
            }
        }

        if (handSlots.Count == 0)
        {
            var scanned = FindObjectsByType<BallHandSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (scanned != null && scanned.Length > 0)
            {
                handSlots.AddRange(scanned);
                handSlots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            }
        }

        if (handSlots.Count == 0)
        {
            Debug.LogWarning(
                $"{nameof(BallSpawner)}: no {nameof(BallHandSlot)} assigned or found in scene. " +
                "Hand balls will fall back to the spawner's transform position.", this);
        }

        AssignSlotIndices();
    }

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
    /// Rebinds the slot list (e.g. when a board scene is loaded). Assigns slot indices.
    /// </summary>
    public void SetHandSlots(IList<BallHandSlot> slots)
    {
        handSlots.Clear();
        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null) handSlots.Add(slots[i]);
            }
        }
        AssignSlotIndices();
        if (_handBalls.Count > 0)
            LayoutHandImmediate();
    }

    public int GetSlotIndexForHandBall(GameObject ball)
    {
        if (ball == null) return -1;
        for (int i = 0; i < _handBalls.Count; i++)
        {
            if (_handBalls[i] == ball) return i;
        }
        return -1;
    }

    public GameObject GetHandBallAtSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _handBalls.Count) return null;
        return _handBalls[slotIndex];
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

    private struct ColliderState
    {
        public Collider col;
        public bool enabled;
    }

    /// <summary>
    /// Clears only active (in-play) balls, leaving the hand intact.
    /// </summary>
    public void ClearActiveBalls()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }

        for (int i = _activeBalls.Count - 1; i >= 0; i--)
        {
            if (_activeBalls[i] != null)
            {
                int id = _activeBalls[i].GetInstanceID();
                _rbStateById.Remove(id);
                _colStateById.Remove(id);
                Destroy(_activeBalls[i]);
            }
        }
        _activeBalls.Clear();
    }

    /// <summary>
    /// Clears all spawned balls (hand + active).
    /// </summary>
    public void ClearAll()
    {
        CancelLayoutAnimation();
        _isGapPreviewActive = false;
        _previewGapIndex = -1;

        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }

        if (_activeBalls.Count != 0)
        {
            _activeBalls.Clear();
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
        _colStateById.Clear();
    }

    /// <summary>
    /// Spawns (or rebuilds) the visible hand line to exactly <paramref name="count"/> balls.
    /// </summary>
    public void BuildHand(int count)
    {
        EnsureHandSlotsResolved();
        count = Mathf.Max(0, count);

        while (_handBalls.Count > count)
        {
            var b = _handBalls[_handBalls.Count - 1];
            _handBalls.RemoveAt(_handBalls.Count - 1);
            if (b != null)
            {
                int id = b.GetInstanceID();
                _rbStateById.Remove(id);
                _colStateById.Remove(id);
                Destroy(b);
            }
        }

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
        EnsureHandSlotsResolved();

        if (prefabs == null)
        {
            BuildHand(0);
            return;
        }

        for (int i = _handBalls.Count - 1; i >= 0; i--)
        {
            var b = _handBalls[i];
            if (b != null)
            {
                int id = b.GetInstanceID();
                _rbStateById.Remove(id);
                _colStateById.Remove(id);
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
        SyncAmpedUpStateFromLoadout();
    }

    private void SyncAmpedUpStateFromLoadout()
    {
        var loadout = ServiceLocator.Get<BallLoadoutController>();
        if (loadout == null) return;

        for (int i = 0; i < _handBalls.Count; i++)
        {
            if (_handBalls[i] == null) continue;
            Ball ball = _handBalls[i].GetComponent<Ball>();
            if (ball == null) continue;
            ball.SetAmpedUp(loadout.GetAmpedUpForSlot(i));
        }
    }

    /// <summary>
    /// Activates the next ball: removes it from the hand, then lerps it to the spawn point.
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
            return SpawnBallAtSpawnPoint();
        }

        GameObject next = _handBalls[0];
        _handBalls.RemoveAt(0);
        LayoutHandImmediate();

        _activeBalls.Add(next);

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

        if (_activeBalls.Contains(ball))
        {
            _activeBalls.Remove(ball);
        }

        _handBalls.Remove(ball);
        int id = ball.GetInstanceID();
        _rbStateById.Remove(id);
        _colStateById.Remove(id);

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
        Vector3 pos = GetHandBallWorldPos(index);
        Quaternion rot = GetHandBallWorldRot();

        GameObject b = Instantiate(ballPrefab, pos, rot);
        b.name = $"{ballPrefab.name}_HandBall_{index + 1}";

        TrySetHandSlotIndex(b, index);
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

        TrySetHandSlotIndex(b, index);
        CacheAndConfigureAsHandBall(b);
        return b;
    }

    private static void TrySetHandSlotIndex(GameObject ball, int slotIndex)
    {
        if (ball == null) return;

        var marker = ball.GetComponent<BallHandSlotMarker>();
        if (marker == null)
        {
            marker = ball.AddComponent<BallHandSlotMarker>();
        }

        marker.SetSlotIndex(slotIndex);
    }

    private GameObject SpawnBallAtSpawnPoint()
    {
        if (ballPrefab == null)
        {
            Debug.LogWarning($"{nameof(BallSpawner)} missing ballPrefab.", this);
            return null;
        }

        if (enforceSingleActiveBall && _activeBalls.Count == 1)
        {
            DespawnBall(_activeBalls[0]);
        }

        GameObject newBall = Instantiate(ballPrefab, spawnPoint.position, spawnPoint.rotation);
        newBall.name = $"{ballPrefab.name}_ActiveBall";
        _activeBalls.Add(newBall);
        return newBall;
    }

    private Vector3 GetHandBallWorldPos(int index)
    {
        if (handSlots != null && index >= 0 && index < handSlots.Count && handSlots[index] != null)
        {
            return handSlots[index].transform.position;
        }
        // Fallback: stack at spawner transform so the scene doesn't blow up if slots aren't wired.
        return transform.position;
    }

    private Quaternion GetHandBallWorldRot()
    {
        return Quaternion.identity;
    }

    private void ComputeHandLayout(int count, out List<Vector3> positions)
    {
        positions = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            positions.Add(GetHandBallWorldPos(i));
        }
    }

    private void LayoutHandImmediate()
    {
        ComputeHandLayout(_handBalls.Count, out var positions);
        for (int i = 0; i < _handBalls.Count; i++)
        {
            var b = _handBalls[i];
            if (b == null) continue;
            if (i < positions.Count)
            {
                b.transform.position = positions[i];
            }
        }
    }

    private void CacheAndConfigureAsHandBall(GameObject ball)
    {
        if (ball == null) return;

        int id = ball.GetInstanceID();

        Rigidbody rb = ball.GetComponent<Rigidbody>();
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

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        else
        {
            _rbStateById[id] = new RigidbodyState { hasRb = false };
        }

        // Disable ball colliders while parked in hand so shop raycasts hit the slot cubes only.
        Collider[] cols = ball.GetComponentsInChildren<Collider>(includeInactive: false);
        if (cols != null && cols.Length > 0)
        {
            var states = new ColliderState[cols.Length];
            for (int i = 0; i < cols.Length; i++)
            {
                bool wasEnabled = cols[i] != null && cols[i].enabled;
                states[i] = new ColliderState { col = cols[i], enabled = wasEnabled };
                if (cols[i] != null) cols[i].enabled = false;
            }
            _colStateById[id] = states;
        }
        else
        {
            _colStateById[id] = System.Array.Empty<ColliderState>();
        }
    }

    private void RestoreHandBallCollidersToActive(GameObject ball)
    {
        if (ball == null) return;
        int id = ball.GetInstanceID();
        if (_colStateById.TryGetValue(id, out var states))
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].col != null) states[i].col.enabled = states[i].enabled;
            }
            _colStateById.Remove(id);
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
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }

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

        if (disablePhysicsWhileMoving && hadRb)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
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

        RestoreRigidbodyForActive(ball);
        RestoreHandBallCollidersToActive(ball);
        Physics.SyncTransforms();

        _moveCoroutine = null;
    }

    #region Shop Animations

    private void CancelLayoutAnimation()
    {
        if (_layoutCoroutine != null)
        {
            StopCoroutine(_layoutCoroutine);
            _layoutCoroutine = null;
        }
    }

    public void AnimateLayoutTransition()
    {
        CancelLayoutAnimation();
        _layoutCoroutine = StartCoroutine(AnimateLayoutCoroutine());
    }

    private IEnumerator AnimateLayoutCoroutine()
    {
        ComputeHandLayout(_handBalls.Count, out var targets);

        var starts = new List<Vector3>(_handBalls.Count);
        for (int i = 0; i < _handBalls.Count; i++)
        {
            starts.Add(_handBalls[i] != null
                ? _handBalls[i].transform.position
                : (i < targets.Count ? targets[i] : Vector3.zero));
        }

        float dur = Mathf.Max(0.01f, layoutAnimDuration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float eased = moveCurve != null ? moveCurve.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);

            for (int i = 0; i < _handBalls.Count; i++)
            {
                if (_handBalls[i] != null && i < targets.Count)
                {
                    _handBalls[i].transform.position =
                        Vector3.LerpUnclamped(starts[i], targets[i], eased);
                }
            }

            yield return null;
        }

        for (int i = 0; i < _handBalls.Count; i++)
        {
            if (_handBalls[i] == null || i >= targets.Count) continue;
            _handBalls[i].transform.position = targets[i];
        }

        _layoutCoroutine = null;
    }

    /// <summary>
    /// Animates hand balls to visually show an insert gap at <paramref name="gapHandIndex"/>.
    /// Balls with index &lt; gap stay put; balls &gt;= gap shift one slot forward to make room.
    /// </summary>
    public void PreviewInsertGap(int gapHandIndex)
    {
        if (gapHandIndex == _previewGapIndex && _isGapPreviewActive)
        {
            return;
        }

        _previewGapIndex = gapHandIndex;
        _isGapPreviewActive = true;

        CancelLayoutAnimation();
        _layoutCoroutine = StartCoroutine(AnimateToGapPreviewCoroutine(gapHandIndex));
    }

    public void ClearInsertGapPreview()
    {
        if (!_isGapPreviewActive)
        {
            return;
        }

        _isGapPreviewActive = false;
        _previewGapIndex = -1;

        AnimateLayoutTransition();
    }

    private IEnumerator AnimateToGapPreviewCoroutine(int gapHandIndex)
    {
        int virtualCount = _handBalls.Count + 1;
        ComputeHandLayout(virtualCount, out var virtualPositions);

        gapHandIndex = Mathf.Clamp(gapHandIndex, 0, _handBalls.Count);

        var starts = new List<Vector3>(_handBalls.Count);
        var targets = new List<Vector3>(_handBalls.Count);

        int realIdx = 0;
        for (int v = 0; v < virtualCount && realIdx < _handBalls.Count; v++)
        {
            if (v == gapHandIndex) continue;

            starts.Add(_handBalls[realIdx] != null
                ? _handBalls[realIdx].transform.position
                : (v < virtualPositions.Count ? virtualPositions[v] : Vector3.zero));
            targets.Add(v < virtualPositions.Count ? virtualPositions[v] : Vector3.zero);
            realIdx++;
        }

        float dur = Mathf.Max(0.01f, layoutAnimDuration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float eased = moveCurve != null ? moveCurve.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);

            for (int i = 0; i < _handBalls.Count; i++)
            {
                if (_handBalls[i] != null && i < targets.Count)
                {
                    _handBalls[i].transform.position =
                        Vector3.LerpUnclamped(starts[i], targets[i], eased);
                }
            }

            yield return null;
        }

        for (int i = 0; i < _handBalls.Count; i++)
        {
            if (_handBalls[i] != null && i < targets.Count)
            {
                _handBalls[i].transform.position = targets[i];
            }
        }

        _layoutCoroutine = null;
    }

    /// <summary>
    /// Spawns a new ball into the hand with a scale-up animation and smooth layout shift.
    /// The loadout must already have been updated by the caller.
    /// </summary>
    public GameObject AddBallAnimated(GameObject prefab, int loadoutSlotIndex)
    {
        CancelLayoutAnimation();
        _isGapPreviewActive = false;
        _previewGapIndex = -1;

        if (prefab == null) return null;

        int handInsertIndex = Mathf.Clamp(loadoutSlotIndex, 0, _handBalls.Count);

        int newCount = _handBalls.Count + 1;
        ComputeHandLayout(newCount, out var targetPositions);

        Vector3 spawnPos = handInsertIndex < targetPositions.Count
            ? targetPositions[handInsertIndex]
            : (targetPositions.Count > 0
                ? targetPositions[targetPositions.Count - 1]
                : transform.position);

        GameObject newBall = Instantiate(prefab, spawnPos, GetHandBallWorldRot());
        newBall.name = $"{prefab.name}_HandBall_{handInsertIndex + 1}";
        TrySetHandSlotIndex(newBall, handInsertIndex);
        CacheAndConfigureAsHandBall(newBall);

        Vector3 targetScale = newBall.transform.localScale;
        newBall.transform.localScale = Vector3.zero;

        _handBalls.Insert(handInsertIndex, newBall);
        SyncAllHandSlotMarkers();

        _layoutCoroutine = StartCoroutine(
            AddBallAnimatedCoroutine(newBall, targetScale, targetPositions));

        return newBall;
    }

    private IEnumerator AddBallAnimatedCoroutine(
        GameObject newBall,
        Vector3 targetScale,
        List<Vector3> targetPositions)
    {
        var starts = new List<Vector3>(_handBalls.Count);
        for (int i = 0; i < _handBalls.Count; i++)
        {
            starts.Add(_handBalls[i] != null
                ? _handBalls[i].transform.position
                : (i < targetPositions.Count ? targetPositions[i] : Vector3.zero));
        }

        float dur = Mathf.Max(0.01f, layoutAnimDuration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float eased = moveCurve != null ? moveCurve.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);

            for (int i = 0; i < _handBalls.Count; i++)
            {
                if (_handBalls[i] == null || i >= targetPositions.Count) continue;

                _handBalls[i].transform.position =
                    Vector3.LerpUnclamped(starts[i], targetPositions[i], eased);

                if (_handBalls[i] == newBall)
                {
                    _handBalls[i].transform.localScale =
                        Vector3.LerpUnclamped(Vector3.zero, targetScale, eased);
                }
            }

            yield return null;
        }

        for (int i = 0; i < _handBalls.Count; i++)
        {
            if (_handBalls[i] == null || i >= targetPositions.Count) continue;

            _handBalls[i].transform.position = targetPositions[i];

            if (_handBalls[i] == newBall)
            {
                _handBalls[i].transform.localScale = targetScale;
            }
        }

        _layoutCoroutine = null;
    }

    /// <summary>
    /// Replaces the ball at <paramref name="loadoutSlotIndex"/> with a new prefab,
    /// animating the old ball out and the new ball in.
    /// </summary>
    public void ReplaceBallAnimated(int loadoutSlotIndex, GameObject newPrefab)
    {
        CancelLayoutAnimation();

        if (newPrefab == null) return;

        int handIndex = Mathf.Clamp(loadoutSlotIndex, 0, _handBalls.Count - 1);
        if (handIndex < 0 || handIndex >= _handBalls.Count) return;

        GameObject oldBall = _handBalls[handIndex];
        Vector3 pos = oldBall != null ? oldBall.transform.position : GetHandBallWorldPos(handIndex);

        if (oldBall != null)
        {
            int id = oldBall.GetInstanceID();
            _rbStateById.Remove(id);
            _colStateById.Remove(id);
            Destroy(oldBall);
        }

        GameObject newBall = Instantiate(newPrefab, pos, GetHandBallWorldRot());
        newBall.name = $"{newPrefab.name}_HandBall_{handIndex + 1}";
        TrySetHandSlotIndex(newBall, handIndex);
        CacheAndConfigureAsHandBall(newBall);

        Vector3 targetScale = newBall.transform.localScale;
        newBall.transform.localScale = Vector3.zero;

        _handBalls[handIndex] = newBall;

        _layoutCoroutine = StartCoroutine(
            ReplaceBallAnimatedCoroutine(newBall, targetScale));
    }

    private IEnumerator ReplaceBallAnimatedCoroutine(GameObject newBall, Vector3 targetScale)
    {
        if (newBall == null) yield break;

        float dur = Mathf.Max(0.01f, layoutAnimDuration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float eased = moveCurve != null ? moveCurve.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);

            if (newBall != null)
            {
                newBall.transform.localScale =
                    Vector3.LerpUnclamped(Vector3.zero, targetScale, eased);
            }

            yield return null;
        }

        if (newBall != null)
        {
            newBall.transform.localScale = targetScale;
        }

        LayoutHandImmediate();
        _layoutCoroutine = null;
    }

    public void SwapHandBallsAnimated(int loadoutSlotA, int loadoutSlotB)
    {
        CancelLayoutAnimation();

        if (loadoutSlotA < 0 || loadoutSlotB < 0) return;
        if (loadoutSlotA >= _handBalls.Count || loadoutSlotB >= _handBalls.Count) return;
        if (loadoutSlotA == loadoutSlotB) return;

        (_handBalls[loadoutSlotA], _handBalls[loadoutSlotB]) = (_handBalls[loadoutSlotB], _handBalls[loadoutSlotA]);
        SyncAllHandSlotMarkers();
        AnimateLayoutTransition();
    }

    public void MoveHandBallAnimated(int loadoutSlotFrom, int loadoutSlotTo)
    {
        CancelLayoutAnimation();

        if (loadoutSlotFrom < 0 || loadoutSlotFrom >= _handBalls.Count) return;
        int targetIdx = Mathf.Clamp(loadoutSlotTo, 0, _handBalls.Count - 1);
        if (loadoutSlotFrom == targetIdx) return;

        GameObject ball = _handBalls[loadoutSlotFrom];
        _handBalls.RemoveAt(loadoutSlotFrom);
        _handBalls.Insert(targetIdx, ball);

        SyncAllHandSlotMarkers();
        AnimateLayoutTransition();
    }

    private void SyncAllHandSlotMarkers()
    {
        for (int i = 0; i < _handBalls.Count; i++)
        {
            if (_handBalls[i] != null)
            {
                TrySetHandSlotIndex(_handBalls[i], i);
            }
        }
    }

    #endregion
}
