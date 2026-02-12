using System.Drawing;
using Unity.Mathematics;
using UnityEngine;

public class Ball : MonoBehaviour
{
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private int amountToEmit;

    void Awake()
    {
        trailRenderer = GetComponent<TrailRenderer>();
    }

    void OnCollisionEnter(Collision collision)
    {

        if (collision.collider.GetComponent<Portal>())
        {
            trailRenderer.emitting = false;
        }


        ParticleSystem emitter = collision.collider.GetComponent<ParticleSystem>();
        if (emitter)
        {
            var emitterShape = emitter.shape;
            emitterShape.rotation = emitter.transform.position - transform.position;
            ParticleSystem.EmitParams prms = new ParticleSystem.EmitParams();

            emitter.Emit(prms, amountToEmit);
        }

    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.collider.GetComponent<Portal>())
        {
            trailRenderer.emitting = true;
        }
    }
}
