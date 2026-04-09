using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BallParticleHandler : MonoBehaviour
{
    [SerializeField] private int startingEmitAmount;
    [SerializeField] private float speedMultiplier;
    [SerializeField] private GameObject particleObject;
    [SerializeField] private Stack<GameObject> pool;
    [SerializeField] private int poolSize;
    [SerializeField] Rigidbody rigidbody;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
    }

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
        BoardComponent[] components = Ball.GetBoardComponentsForScoring(collision.collider);
        if (components.Length == 0) return;
        if (components[0].componentType == BoardComponentType.Flipper) return;
        if (components[0].componentType == BoardComponentType.Spinner) return;
        if (components[0].componentType == BoardComponentType.Rollover) return;
        if (components[0].componentType == BoardComponentType.Launcher) return;
        if (components[0].componentType == BoardComponentType.Portal) return;
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

        int emitMult = Mathf.FloorToInt(speedMultiplier * rigidbody.linearVelocity.magnitude);
        emitter.Emit(startingEmitAmount * emitMult);
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