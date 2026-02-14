using System.Net.Sockets;
using UnityEngine;

public class ScoreBall : Ball
{
    [SerializeField] private float amountToMultiply;

    void OnCollisionEnter(Collision collision)
    {
        PointAdder adder = collision.collider.GetComponent<PointAdder>();
        if(adder)
        {
            adder.multiplyPointsToAdd(amountToMultiply);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        PointAdder adder = collision.collider.GetComponent<PointAdder>();
        if(adder)
        {
            adder.multiplyPointsToAdd(1 / amountToMultiply);
        }
    }

    void OnTriggerEnter(Collider collision)
    {
        PointAdder adder = collision.GetComponent<PointAdder>();
        if (adder)
        {
            adder.multiplyPointsToAdd(amountToMultiply);
        }
    }

    void OnTriggerExit(Collider collision)
    {
        PointAdder adder = collision.GetComponent<PointAdder>();
        if (adder)
        {
            adder.multiplyPointsToAdd(1 / amountToMultiply);
        }
    }
}

