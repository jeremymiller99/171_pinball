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
    [SerializeField] Rigidbody _rb;

    // The emitter prefab's authored start size. The per-component marker size
    // multiplies this rather than replacing it, so the prefab (HitParticles) stays the
    // base — otherwise a marker size of 1 would overwrite the authored size and inflate
    // every hit's particles.
    private float _baseStartSize = 1f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pool = new Stack<GameObject>();
        for (int i = 0; i < poolSize; i++)
        {
            pool.Push(Instantiate(particleObject, Vector3.zero, quaternion.identity));
        }

        ParticleSystem prefabPs = particleObject != null
            ? particleObject.GetComponent<ParticleSystem>()
            : null;
        if (prefabPs != null)
        {
            _baseStartSize = prefabPs.main.startSizeMultiplier;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        BoardComponent[] components = Ball.GetBoardComponentsForScoring(collision.collider);
        if (components.Length == 0) return;

        BoardComponent component = components[0];
        if (!ComponentTypeCatalog.EmitsBallParticles(component.componentType)) return;

        // Per-prefab tuning lives on the component's HitParticleOrigin marker (position,
        // size, amount), so each variant adjusts independently. No marker → emit from
        // the component origin at the ball's own defaults.
        HitParticleOrigin marker = component.GetComponentInChildren<HitParticleOrigin>();
        Vector3 emitPos = marker != null ? marker.transform.position : component.transform.position;
        float size = marker != null && marker.size > 0f ? marker.size : 1f;
        int baseAmount = marker != null && marker.emitAmount > 0 ? marker.emitAmount : startingEmitAmount;

        GameObject emitterObj = pool.Pop();
        ParticleSystem emitter = emitterObj.GetComponent<ParticleSystem>();
        emitter.transform.position = emitPos;

        ParticleSystem.MainModule main = emitter.main;
        main.startSizeMultiplier = _baseStartSize * size;

        var emitterShape = emitter.shape;
        if (transform.position.x < collision.transform.position.x)
        {
            emitterShape.rotation = Vector3.down * Vector3.Angle(transform.position - collision.transform.position, Vector3.forward);
        }
        else
        {
            emitterShape.rotation = Vector3.up * Vector3.Angle(transform.position - collision.transform.position, Vector3.forward);
        }

        int emitMult = Mathf.FloorToInt(speedMultiplier * _rb.linearVelocity.magnitude);
        emitter.Emit(Mathf.Max(0, baseAmount * emitMult));
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