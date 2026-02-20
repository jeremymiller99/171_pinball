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

    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private int amountToEmit;
    [SerializeField] private GameObject particleObject;
    [SerializeField] private Stack<GameObject> pool;
    [SerializeField] private int poolSize;

    void Awake()
    {
        trailRenderer = GetComponent<TrailRenderer>();
    }

    void Start()
    {
        pool = new Stack<GameObject>();
        if (particleObject == null) return;
        int safeCount = Mathf.Clamp(poolSize, 0, 64);
        for (int i = 0; i < safeCount; i++)
        {
            pool.Push(Instantiate(particleObject, Vector3.zero, quaternion.identity));
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        
        if (collision.collider.GetComponent<Portal>())
        {
            trailRenderer.enabled = false;
            return;
        }


        if (collision.collider.GetComponent<PointAdder>() || collision.collider.GetComponent<MultAdder>())
        {
            if (pool == null || pool.Count == 0)
                return;
            GameObject emitterObj = pool.Pop();
            ParticleSystem emitter = emitterObj != null ? emitterObj.GetComponent<ParticleSystem>() : null;
            if (emitter == null)
            {
                pool.Push(emitterObj);
                return;
            }
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

    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.collider.GetComponent<Portal>())
        {
            trailRenderer.enabled = true;
        }
    }

    void OnDestroy()
    {
        if (pool == null) return;
        while (pool.Count > 0)
        {
            var obj = pool.Pop();
            if (obj != null) Destroy(obj);
        }
    }
}
