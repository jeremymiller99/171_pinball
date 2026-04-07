using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BallParticleHandler : MonoBehaviour
{
    [SerializeField] private int amountToEmit;
    [SerializeField] private GameObject particleObject;
    [SerializeField] private Stack<GameObject> pool;
    [SerializeField] private int poolSize;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
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
        if (Ball.GetBoardComponentsForScoring(collision.collider).Length == 0) return;
        GameObject emitterObj = pool.Pop();
        ParticleSystem emitter = emitterObj.GetComponent<ParticleSystem>();
        emitter.transform.position = transform.position;
        var emitterShape = emitter.shape;
        if (transform.position.x < collision.transform.position.x)
        {
            emitterShape.rotation = Vector3.down * Vector3.Angle(transform.position - collision.transform.position, Vector3.forward);
        }
        else
        {
            emitterShape.rotation = Vector3.up * Vector3.Angle(transform.position - collision.transform.position, Vector3.forward);
        }

        emitter.Emit(amountToEmit);
        pool.Push(emitterObj);
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