// Updated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-17.
using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using Unity.Mathematics;
using UnityEngine;
using FMODUnity;
using System;

public class Ball : MonoBehaviour
{
    public ScoreManager scoreManager;

    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private int amountToEmit;
    [SerializeField] private GameObject particleObject;
    [SerializeField] private Stack<GameObject> pool;
    [SerializeField] private int poolSize;
    public int componentHits;
    public float pointMultiplier = 1f;
    public float multMultiplier = 1f;
    public int coinMultiplier = 1;
    public GameObject lastObjectHit;

    public virtual float PointsAwardMultiplier => pointMultiplier;

    virtual protected void Awake()
    {
        trailRenderer = GetComponent<TrailRenderer>();
        scoreManager = FindFirstObjectByType<ScoreManager>();
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
    {
        lastObjectHit = collision.gameObject;
        BoardComponent[] components = GetBoardComponentsForScoring(collision.collider);
        if (components.Length > 0 && HapticManager.Instance != null)
        {
            HapticManager.Instance.PlayCollisionHaptic(true);
        }

        if (components.Length > 0)
        {
            HandleParticles(collision);
            bool scoredAnyComponent = false;
            foreach(BoardComponent component in components)
            {
                if (!ShouldScoreBoardComponent(component))
                {
                    continue;
                }

                scoredAnyComponent = true;
                AddScore(component.amountToScore, component.typeOfScore, transform);
            }

            if (scoredAnyComponent)
            {
                componentHits++;
            }
        }

    }

    void OnTriggerEnter(Collider collider)
    {
        lastObjectHit = collider.gameObject;
        BoardComponent[] components = GetBoardComponentsForScoring(collider);
        if (components.Length > 0 && HapticManager.Instance != null)
        {
            HapticManager.Instance.PlayCollisionHaptic(true);
        }

        if (components.Length > 0)
        {
            bool scoredAnyComponent = false;
            foreach(BoardComponent component in components)
            {
                if (!ShouldScoreBoardComponent(component))
                {
                    continue;
                }

                scoredAnyComponent = true;
                AddScore(component.amountToScore, component.typeOfScore, transform);
            }

            if (scoredAnyComponent)
            {
                componentHits++;
            }
        }
        
    }

    protected virtual bool ShouldScoreBoardComponent(BoardComponent component)
    {
        return component != null;
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

    void OnDestroy()
    {
        for (int i = 0; i < poolSize; i++)
        {
            Destroy(pool.Pop());
        }
    }

    virtual protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
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
