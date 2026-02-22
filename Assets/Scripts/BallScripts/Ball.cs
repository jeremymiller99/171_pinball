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
    [SerializeField] protected int componentHits;
    public float pointMultiplier = 1f;
    public float multMultiplier = 1f;
    public int coinMultiplier = 1;

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
        BoardComponent[] components = collision.collider.GetComponents<BoardComponent>();
        if (components.Length > 0)
        {
            componentHits++;
            HandleParticles(collision);
            foreach(BoardComponent component in components)
            {
                AddScore(component.amountToScore, component.typeOfScore, transform);
            }
        }

    }

    void OnTriggerEnter(Collider collider)
    {
        BoardComponent[] components = collider.GetComponents<BoardComponent>();
        if (components.Length > 0)
        {
            componentHits++;
            foreach(BoardComponent component in components)
            {
                AddScore(component.amountToScore, component.typeOfScore, transform);
            }
        }
        
    }

    protected void HandleParticles(Collision collision)
    {
        if (!collision.collider.GetComponent<Component>()) return;
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
