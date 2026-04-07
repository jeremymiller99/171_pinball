// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using System.Collections.Generic;
using System.Collections;

using Unity.Mathematics;
using UnityEngine;
using FMODUnity;
using TMPro;
using System;

public class Ball : MonoBehaviour
{
    protected ScoreManager scoreManager;

    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private int amountToEmit;
    [SerializeField] private GameObject particleObject;
    [SerializeField] private Stack<GameObject> pool;
    [SerializeField] private int poolSize;
    protected int componentHits;
    protected float pointMultiplier = 1f;
    protected float multMultiplier = 1f;
    protected int coinMultiplier = 1;
    protected GameObject lastObjectHit;

    public int ComponentHits => componentHits;
    public GameObject LastObjectHit => lastObjectHit;
    public float PointMultiplier
    {
        get => pointMultiplier;
        set => pointMultiplier = value;
    }
    public float MultMultiplier
    {
        get => multMultiplier;
        set => multMultiplier = value;
    }
    public int CoinMultiplier
    {
        get => coinMultiplier;
        set => coinMultiplier = value;
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

    public virtual float PointsAwardMultiplier => pointMultiplier;

    virtual protected void Awake()
    {
        trailRenderer = GetComponent<TrailRenderer>();
    }

    virtual protected void Start()
    {
        pool = new Stack<GameObject>();
        for (int i = 0; i < poolSize; i++)
        {
            pool.Push(Instantiate(particleObject, Vector3.zero, quaternion.identity));
        }
    }

    virtual protected void OnCollisionEnter(Collision collision)
        => HandleBoardComponentHit(collision.collider, collision);

    void OnTriggerEnter(Collider collider)
        => HandleBoardComponentHit(collider);

    private void HandleBoardComponentHit(
        Collider collider, Collision collision = null)
    {
        lastObjectHit = collider.gameObject;
        BoardComponent[] components = GetBoardComponentsForScoring(collider);
        if (components.Length == 0) return;

        ServiceLocator.Get<HapticManager>()?.PlayCollisionHaptic(true);
        if (collision != null) HandleParticles(collision);

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
        }
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

    /// <summary>
    /// Gets BoardComponents for scoring. Checks the collider's GameObject first, then parent hierarchy
    /// (for bumpers/targets whose collider is on a child, e.g. visual mesh).
    /// </summary>
    private static BoardComponent[] GetBoardComponentsForScoring(Collider collider)
    {
        BoardComponent[] components = collider.GetComponents<BoardComponent>();
        if (components.Length > 0)
            return components;
        BoardComponent parentComponent = collider.GetComponentInParent<BoardComponent>();
        return parentComponent != null ? new[] { parentComponent } : System.Array.Empty<BoardComponent>();
    }

    protected void HandleParticles(Collision collision)
    {
        if (GetBoardComponentsForScoring(collision.collider).Length == 0) return;
        GameObject emitterObj = pool.Pop();
        ParticleSystem emitter = emitterObj.GetComponent<ParticleSystem>();
        emitter.transform.position = transform.position;
        var emitterShape = emitter.shape;
        if (transform.position.x < collision.transform.position.x)
        {
            emitterShape.rotation = Vector3.down * Vector3.Angle(transform.position - collision.transform.position, Vector3.forward);
        } else {
            emitterShape.rotation = Vector3.up * Vector3.Angle(transform.position - collision.transform.position, Vector3.forward);
        }

        emitter.Emit(amountToEmit);
        pool.Push(emitterObj);
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

    protected virtual void OnDestroy()
    {
        CleanupParticlePool();
    }

    protected void CleanupParticlePool()
    {
        if (pool == null) return;
        while (pool.Count > 0)
        {
            var obj = pool.Pop();
            if (obj != null) Destroy(obj);
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
                scoreManager.AddScore(amount * pointMultiplier, typeOfScore, pos);
                break;
            case TypeOfScore.mult:
                scoreManager.AddScore(amount * multMultiplier, typeOfScore, pos);
                break;
            case TypeOfScore.coins:
                scoreManager.AddScore(amount * coinMultiplier, typeOfScore, pos);
                break;
        }
    }
}
