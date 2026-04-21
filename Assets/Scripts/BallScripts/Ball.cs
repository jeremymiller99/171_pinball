// Updated with Claude Code (claude-opus-4-7) by jjmil on 2026-04-20.
using System.Collections.Generic;
using System.Collections;

using Unity.Mathematics;
using UnityEngine;
using FMODUnity;
using TMPro;
using System;

public class Ball : MonoBehaviour
{
    public const float ampedUpProcChance = 0.25f;
    public const float ampedUpMultReward = 0.1f;

    protected ScoreManager scoreManager;
    protected int componentHits;
    protected float ballPointMultiplier = 1f;
    protected float ballMultMultiplier = 1f;
    protected int ballCoinMultiplier = 1;
    protected GameObject lastObjectHit;
    protected bool isAmpedUp;

    public int ComponentHits => componentHits;
    public GameObject LastObjectHit => lastObjectHit;
    public bool IsAmpedUp => isAmpedUp;

    public float PointMultiplier
    {
        get => ballPointMultiplier;
        set => ballPointMultiplier = value;
    }
    public float MultMultiplier
    {
        get => ballMultMultiplier;
        set => ballMultMultiplier = value;
    }
    public int CoinMultiplier
    {
        get => ballCoinMultiplier;
        set => ballCoinMultiplier = value;
    }

    public void SetAmpedUp(bool value)
    {
        isAmpedUp = value;
    }

    public void ResetComponentHits()
    {
        componentHits = 0;
    }

    [Header("Hit Count Popup")]
    [Tooltip("Font for the hit count popup. If null, uses the spawner's default (Jersey 10).")]
    [SerializeField] protected TMP_FontAsset hitCountFontAsset;
    [SerializeField] protected float hitCountPopupScale = 0.7f;
    [SerializeField] protected Vector2 hitCountPopupOffset = new Vector2(0f, 40f);
    [Tooltip("Color for the hit count popup text. Set per ball to match its visual color.")]
    [SerializeField] protected Color hitCountPopupColor = Color.white;

    protected FloatingTextSpawner floatingTextSpawner;

    protected virtual int HitIntervalForPopup => 0;

    public virtual float PointsAwardMultiplier => ballPointMultiplier;

    virtual protected void OnCollisionEnter(Collision collision)
        => HandleBoardComponentHit(collision.collider, collision);

    void OnTriggerEnter(Collider collider)
        => HandleBoardComponentHit(collider);

    private void HandleBoardComponentHit(
        Collider collider, Collision collision = null)
    {
        if (ShouldIgnoreBoardHitFromCollider(collider)) return;

        lastObjectHit = collider.gameObject;
        BoardComponent[] components = GetBoardComponentsForScoring(collider);
        if (components.Length == 0) return;

        ServiceLocator.Get<HapticManager>()?.PlayCollisionHaptic(true);

        bool scoredAny = false;
        foreach (BoardComponent component in components)
        {
            if (!ShouldScoreBoardComponent(component)) continue;

            scoredAny = true;
            AddScore(component.amountToScore, component.typeOfScore, transform);
        }

        if (scoredAny)
        {
            componentHits++;
            TrySpawnHitCountPopup();
            TryProcAmpedUpMult();
        }
    }

    private void TryProcAmpedUpMult()
    {
        if (!isAmpedUp) return;
        if (UnityEngine.Random.value > ampedUpProcChance) return;

        if (scoreManager == null)
            scoreManager = ServiceLocator.Get<ScoreManager>();
        if (scoreManager == null) return;

        scoreManager.AddScore(
            ampedUpMultReward, TypeOfScore.mult, transform);
    }

    private void TrySpawnHitCountPopup()
    {
        int interval = HitIntervalForPopup;
        if (interval <= 0) return;

        int progress = ((componentHits - 1) % interval) + 1;
        SpawnHitCountPopup(progress, interval);
    }

    protected void SpawnHitCountPopup(int current, int total)
    {
        if (floatingTextSpawner == null)
            floatingTextSpawner = ServiceLocator.Get<FloatingTextSpawner>();
        if (floatingTextSpawner == null) return;

        string text = current.ToString();
        floatingTextSpawner.SpawnText(transform.position, text, hitCountFontAsset, hitCountPopupScale, hitCountPopupOffset, hitCountPopupColor);
    }

    protected virtual bool ShouldScoreBoardComponent(BoardComponent component)
    {
        return component.amountToScore != 0;
    }

    protected virtual bool ShouldIgnoreBoardHitFromCollider(Collider collider)
    {
        return false;
    }

    /// <summary>
    /// Gets BoardComponents for scoring. Checks the collider's GameObject first, then parent hierarchy
    /// (for bumpers/targets whose collider is on a child, e.g. visual mesh).
    /// </summary>
    public static BoardComponent[] GetBoardComponentsForScoring(Collider collider)
    {
        BoardComponent[] components = collider.GetComponents<BoardComponent>();
        if (components.Length > 0)
            return components;
        BoardComponent parentComponent = collider.GetComponentInParent<BoardComponent>();
        return parentComponent != null ? new[] { parentComponent } : System.Array.Empty<BoardComponent>();
    }

    

    /// <summary>
    /// Creates independent material copies on every Renderer so this ball
    /// is not affected when another ball sharing the same material is destroyed.
    /// Call after Instantiate for any duplicated / split ball.
    /// </summary>
    public static void EnsureOwnMaterials(GameObject ball)
    {
        if (ball == null) return;

        foreach (var r in ball.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            var shared = r.sharedMaterials;
            if (shared == null || shared.Length == 0) continue;

            var copies = new Material[shared.Length];
            for (int i = 0; i < shared.Length; i++)
                copies[i] = shared[i] != null ? new Material(shared[i]) : null;
            r.materials = copies;
        }
    }

    virtual protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (scoreManager == null)
            scoreManager = ServiceLocator.Get<ScoreManager>();
            
        if (scoreManager == null) return;

        switch (typeOfScore)
        {
            case TypeOfScore.points:
                scoreManager.AddScore(amount * ballPointMultiplier, typeOfScore, pos);
                break;
            case TypeOfScore.mult:
                scoreManager.AddScore(amount * ballMultMultiplier, typeOfScore, pos);
                break;
            case TypeOfScore.coins:
                scoreManager.AddScore(amount * ballCoinMultiplier, typeOfScore, pos);
                break;
        }
    }
}
