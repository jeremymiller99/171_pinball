using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using Unity.Mathematics;
using UnityEngine;
using FMODUnity;
using System;

public class Ball : MonoBehaviour
{
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
        for (int i = 0; i < poolSize; i++)
        {
            pool.Push(Instantiate(particleObject, Vector3.zero, quaternion.identity));
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        
        if (collision.collider.GetComponent<Portal>())
        {
            trailRenderer.enabled = false;
            return;
        }


        if (collision.collider.GetComponent<PointAdder>() || collision.collider.GetComponent<MultAdder>())
        {
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
        for (int i = 0; i < poolSize; i++)
        {
            Destroy(pool.Pop());
        }
    }
}
