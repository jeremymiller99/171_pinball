using UnityEngine;
using System.Collections.Generic;

public class ScoreBall : Ball
{
    [Header("Score Ball")]
    [Tooltip("Multiplier applied to PointAdder values while this ball is in contact.\n" +
             "For 'Blue Two' this should be 2.")]
    [Min(0.01f)]
    [SerializeField] private float amountToMultiply = 2f;

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
        if (component.typeOfScore == TypeOfScore.points)
        {
            AddScore(component.amountToScore * amountToMultiply, TypeOfScore.points, transform);
        } else
        {
            AddScore(component.amountToScore, component.typeOfScore, transform);
        }
    }
}

