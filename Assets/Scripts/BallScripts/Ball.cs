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
    /// <summary>
    /// Per-ball multiplier applied to point awards triggered by this ball.
    /// Default is 1. Override in specific ball types (e.g. split-scatter children).
    /// </summary>
    public virtual float PointsAwardMultiplier => 1f;

    public ScoreManager scoreManager;

    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private int amountToEmit;
    [SerializeField] private GameObject particleObject;
    [SerializeField] private Stack<GameObject> pool;
    [SerializeField] private int poolSize;

    protected void Awake()
    {
        trailRenderer = GetComponent<TrailRenderer>();
        scoreManager = FindFirstObjectByType<ScoreManager>();
    }

    void Start()
    {
        pool = new Stack<GameObject>();
        for (int i = 0; i < poolSize; i++)
        {
            pool.Push(Instantiate(particleObject, Vector3.zero, quaternion.identity));
        }
    }

    protected void OnCollisionEnter(Collision collision)
    {   
        BoardComponent component = collision.collider.GetComponent<BoardComponent>();
        if (component)
        {
            AddScore(component.amountToScore, component.typeOfScore, transform);
            HandleParticles(collision);
        }

    }

    void OnTriggerEnter(Collider collider)
    {
        BoardComponent component = collider.GetComponent<BoardComponent>();
        if (component)
        {
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

        if (collision.collider.GetComponent<Portal>())
        {
            trailRenderer.enabled = false;
            return;            
        }
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

    protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        scoreManager.AddScore(amount, typeOfScore, pos);
    }
}
