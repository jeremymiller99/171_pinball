using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach this to a LARGE trigger collider on the ball prefab (or a child).
/// When a score-giving target is inside this trigger, we predict whether hitting it would cross the round goal.
/// If so, we request a slow-mo (e.g. 0.5x) BEFORE the hit, so the player can feel the impending win.
///
/// Setup:
/// - Add a SphereCollider (or similar) to the ball prefab (or a child), set IsTrigger = true, size it larger.
/// - Add this script to that collider object.
/// - Ensure board targets have PointAdder/MultAdder (already true for many).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class BallWinPredictorSlowmoTrigger : MonoBehaviour
{
    [Header("Trigger filtering")]
    [SerializeField] private string ballTag = "Ball";
    [Tooltip("Optional: restrict which layers are considered as targets. Leave to Everything to keep simple.")]
    [SerializeField] private LayerMask targetLayers = ~0;

    [Header("Slow motion request")]
    [Range(0.01f, 1f)]
    [SerializeField] private float slowmoMultiplier = 0.5f;
    [Min(0f)]
    [SerializeField] private float response = 10f;
    [Min(0f)]
    [SerializeField] private float releaseResponse = 6f;

    [Header("Prediction")]
    [Tooltip("Extra margin so we slow down even on near-miss close calls. Example: 0.98 means 98% of goal.")]
    [Range(0f, 1.2f)]
    [SerializeField] private float goalMarginMultiplier = 1.0f;

    private readonly HashSet<Collider> _nearby = new HashSet<Collider>();
    private ScoreManager _score;
    private float _currentRequest = 1f;

    private void Awake()
    {
        var c = GetComponent<Collider>();
        if (c != null && !c.isTrigger)
        {
            Debug.LogWarning($"{nameof(BallWinPredictorSlowmoTrigger)} requires its Collider to be IsTrigger=true.", this);
        }
        ResolveScore();
    }

    private void OnEnable()
    {
        ResolveScore();
    }

    private void OnDisable()
    {
        if (_score != null)
        {
            _score.ClearTimeScaleRequest(this);
        }
        _nearby.Clear();
        _currentRequest = 1f;
    }

    private void ResolveScore()
    {
        if (_score != null) return;

#if UNITY_2022_2_OR_NEWER
        _score = FindFirstObjectByType<ScoreManager>();
#else
        _score = FindObjectOfType<ScoreManager>();
#endif
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (!IsLayerAllowed(other.gameObject.layer)) return;
        if (!string.IsNullOrWhiteSpace(ballTag) && other.CompareTag(ballTag)) return;
        _nearby.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        _nearby.Remove(other);
    }

    private bool IsLayerAllowed(int layer)
    {
        return (targetLayers.value & (1 << layer)) != 0;
    }

    private void LateUpdate()
    {
        ResolveScore();
        if (_score == null)
            return;

        bool shouldSlow = PredictAnyNearbyHitCouldReachGoal(out float bestRequest);
        float target = shouldSlow ? Mathf.Clamp(bestRequest, 0.01f, 1f) : 1f;

        float dt = Time.unscaledDeltaTime;
        float k = shouldSlow ? response : releaseResponse;
        float lerp = 1f - Mathf.Exp(-Mathf.Max(0f, k) * dt);
        _currentRequest = Mathf.Lerp(_currentRequest, target, lerp);

        // Apply/clear request (avoid leaving tiny offsets around 1).
        if (_currentRequest >= 0.999f && !shouldSlow)
        {
            _currentRequest = 1f;
            _score.ClearTimeScaleRequest(this);
        }
        else
        {
            _score.SetTimeScaleRequest(this, _currentRequest);
        }
    }

    private bool PredictAnyNearbyHitCouldReachGoal(out float bestRequestMultiplier)
    {
        bestRequestMultiplier = slowmoMultiplier;

        float goal = _score.Goal;
        if (goal <= 0.0001f)
            return false;

        float targetGoal = goal * Mathf.Max(0f, goalMarginMultiplier);

        // If we're already basically at/over the goal, keep slow (this pairs nicely with the goal-hit hold).
        if (_score.LiveRoundTotal >= targetGoal)
            return true;

        float effectivePointAwardMult = _score.GetEffectivePositivePointAwardMultiplier();

        // Iterate candidates; ignore destroyed/invalid colliders.
        bool canReach = false;
        var toRemove = ListPool<Collider>.Get();
        foreach (var col in _nearby)
        {
            if (col == null)
            {
                toRemove.Add(col);
                continue;
            }

            // Prefer the object/parent that actually holds the scoring components.
            PointAdder pa = col.GetComponentInParent<PointAdder>();
            MultAdder ma = col.GetComponentInParent<MultAdder>();
            if (pa == null && ma == null)
                continue;

            float addPointsRaw = pa != null ? Mathf.Max(0f, pa.PointsToAdd) : 0f;
            float addMult = ma != null ? ma.MultToAdd : 0f;

            float appliedPoints = addPointsRaw * Mathf.Max(0f, effectivePointAwardMult);
            float predictedPoints = _score.points + appliedPoints;
            float predictedMult = _score.mult + addMult;
            if (predictedMult < 0f) predictedMult = 0f;

            float predictedLive = _score.roundTotal + (predictedPoints * predictedMult);
            if (predictedLive >= targetGoal)
            {
                canReach = true;
                // If we wanted “near miss vs guaranteed”, we could vary bestRequestMultiplier here.
                bestRequestMultiplier = slowmoMultiplier;
                break;
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            _nearby.Remove(toRemove[i]);
        }
        ListPool<Collider>.Release(toRemove);

        return canReach;
    }

    // Tiny list pool to avoid per-frame allocations.
    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> Pool = new Stack<List<T>>();
        public static List<T> Get()
        {
            if (Pool.Count > 0)
            {
                var l = Pool.Pop();
                l.Clear();
                return l;
            }
            return new List<T>(8);
        }
        public static void Release(List<T> list)
        {
            if (list == null) return;
            list.Clear();
            Pool.Push(list);
        }
    }
}

