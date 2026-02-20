using UnityEngine;
using System.Collections.Generic;

public class MultBall : Ball
{
    [Header("Mult Ball")]
    [Tooltip("Multiplier applied to MultAdder values while this ball is in contact.\n" +
             "For 'Red Two' this should be 2.")]
    [Min(0.01f)]
    [SerializeField] private float amountToMultiply = 2f;

    private readonly HashSet<MultAdder> activeAdders = new HashSet<MultAdder>();

    private void OnValidate()
    {
        if (amountToMultiply < 0.01f)
        {
            amountToMultiply = 2f;
        }
    }

    void OnCollisionEnter(Collision collision)
    {

        BoardComponent component = collision.collider.GetComponent<BoardComponent>();
        if (!component) return;

        
        HandleParticles(collision);

        if (component.typeOfScore == TypeOfScore.mult)
        {
            AddScore(component.amountToScore * amountToMultiply, TypeOfScore.mult, transform);
        } else
        {
            AddScore(component.amountToScore, component.typeOfScore, transform);
        }
    }

}
