using System.Collections.Generic;
using UnityEngine;

public class ChaosBall : Ball
{
    [SerializeField] private GameObject chaosBallPrefab;
    [SerializeField] private float lastBallPointsMultiplier = 3f;
    [SerializeField] private float whiteBallPointsMultiplier = 0.25f;
    [SerializeField] private float spawnRadius = 0.5f;
    [Header("Initial ball = red, duplicates = white")]
    [SerializeField] private Color redColor = Color.red;
    [SerializeField] private Color whiteColor = Color.white;
    [SerializeField] private bool applyColors = true;
    private bool _isRed;
    private bool _hasSpawned;
    private BallSpawner _spawner;

    new void Awake()
    {
        base.Awake();
        _spawner = ServiceLocator.Get<BallSpawner>();
    }

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (!_hasSpawned)
            TryStartChaosRound();

        if (_isRed)
            amount *= lastBallPointsMultiplier;
        else
            amount *= whiteBallPointsMultiplier;

        base.AddScore(amount, typeOfScore, pos);
    }

    private void TryStartChaosRound()
    {
        if (_hasSpawned || chaosBallPrefab == null) return;
        _hasSpawned = true;

        var allBalls = new List<ChaosBall> { this };
        Vector3 center = transform.position;

        for (int i = 0; i < 9; i++)
        {
            float angle = (i / 9f) * 360f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
            GameObject go = Instantiate(chaosBallPrefab, center + offset, Quaternion.identity);
            EnsureOwnMaterials(go);

            var cb = go.GetComponent<ChaosBall>();
            if (cb != null)
            {
                cb._hasSpawned = true;
                cb._spawner = _spawner;
                if (applyColors)
                    cb.ApplyColor(whiteColor);
                ChaosRoundTracker.Register(cb);
                allBalls.Add(cb);
            }
            if (_spawner != null)
                _spawner.ActiveBalls.Add(go);
        }

        _isRed = true;
        ChaosRoundTracker.Register(this);
        ChaosRoundTracker.SetRed(this);
        if (applyColors)
            ApplyColor(redColor);
    }

    private void ApplyColor(Color color)
    {
        var r = GetComponent<Renderer>();
        if (r != null && r.material != null)
            r.material.color = color;
    }

    protected override void OnDestroy()
    {
        ChaosRoundTracker.Unregister(this);
        if (ChaosRoundTracker.RemainingCount == 0)
            ChaosRoundTracker.ClearRound();

        base.OnDestroy();
    }
}
