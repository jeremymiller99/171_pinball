using System.Drawing;
using Unity.Mathematics;
using UnityEngine;

public class Ball : MonoBehaviour
{
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private int amountToEmit;
    [SerializeField] private Vector3 baseSizeVector;

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
            if (transform.position.x < collision.transform.position.x)
            {
                emitterShape.rotation = Vector3.down * Vector3.Angle(transform.position - collision.transform.position, Vector3.forward);
            } else
            {
                emitterShape.rotation = Vector3.up * Vector3.Angle(transform.position - collision.transform.position, Vector3.forward);
            }

            emitterShape.position = collision.contacts[0].normal;
            emitterShape.scale = new Vector3{
                x = baseSizeVector.x / emitter.transform.localScale.x,
                y = baseSizeVector.y / emitter.transform.localScale.y,
                z = baseSizeVector.z / emitter.transform.localScale.z
            };
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
