using System.Collections.Generic;
using UnityEngine;

public class EyeOnThePrizeBall : Ball
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

    void Awake()
    {
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

        // Carry the original ball's loadout slot onto every spawned ball so that
        // DrainHandler always pops the correct slot even if the red ball drains first.
        int slotIndex = -1;
        var myMarker = GetComponent<BallHandSlotMarker>();
        if (myMarker != null) slotIndex = myMarker.SlotIndex;

        var allBalls = new List<EyeOnThePrizeBall> { this };
        Vector3 center = transform.position;

        for (int i = 0; i < 9; i++)
        {
            float angle = (i / 9f) * 360f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
            GameObject go = Instantiate(chaosBallPrefab, center + offset, Quaternion.identity);
            EnsureOwnMaterials(go);

            if (slotIndex >= 0)
            {
                var marker = go.GetComponent<BallHandSlotMarker>() ?? go.AddComponent<BallHandSlotMarker>();
                marker.SetSlotIndex(slotIndex);
            }

            var cb = go.GetComponent<EyeOnThePrizeBall>();
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

    void OnDestroy()
    {
        ChaosRoundTracker.Unregister(this);
        if (ChaosRoundTracker.RemainingCount == 0)
            ChaosRoundTracker.ClearRound();
    }
}
