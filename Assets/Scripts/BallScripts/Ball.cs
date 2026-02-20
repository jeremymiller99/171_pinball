// Updated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-17.
using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using Unity.Mathematics;
using UnityEngine;
using FMODUnity;
using System;
using UnityEngine.ResourceManagement.ResourceProviders.Simulation;

public class Ball : MonoBehaviour
{
    public ScoreManager scoreManager;

    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private int amountToEmit;
    [SerializeField] private GameObject particleObject;
    [SerializeField] private Stack<GameObject> pool;
    [SerializeField] private int poolSize;
    [SerializeField] protected int componentHits;
    [SerializeField] protected float pointMultiplier = 1f;
    [SerializeField] protected float multMultiplier = 1f;
    [SerializeField] protected int coinMultiplier = 1;

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
        BoardComponent component = collision.collider.GetComponent<BoardComponent>();
        if (component)
        {
            componentHits++;
            AddScore(component.amountToScore, component.typeOfScore, transform);
            HandleParticles(collision);
        }

    }

    void OnTriggerEnter(Collider collider)
    {
        BoardComponent component = collider.GetComponent<BoardComponent>();
        if (component)
        {
            if (collider.GetComponent<Portal>())
            {
                trailRenderer.enabled = false;
                return;            
            }
            
            AddScore(component.amountToScore, component.typeOfScore, transform);
        }
        
    }

    void OnTriggerExit(Collider collider)
    {
        if (collider.GetComponent<Portal>())
        {
            trailRenderer.enabled = true;
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
