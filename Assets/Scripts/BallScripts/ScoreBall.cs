using System.Net.Sockets;
using UnityEngine;

public class ScoreBall : Ball
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //Doubles adder's pointstoadd when it enters, halves it when collision is over.
    void OnCollisionEnter(Collision collision)
    {
        PointAdder adder = collision.collider.GetComponent<PointAdder>();
        if(adder)
        {
            adder.multiplyPointsToAdd(2);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        PointAdder adder = collision.collider.GetComponent<PointAdder>();
        if(adder)
        {
            adder.multiplyPointsToAdd(.5f);
        }
    }

    void OnTriggerEnter(Collider collision)
    {
        PointAdder adder = collision.GetComponent<PointAdder>();
        if (adder)
        {
            adder.multiplyPointsToAdd(2);
        }
    }

    void OnTriggerExit(Collider collision)
    {
        PointAdder adder = collision.GetComponent<PointAdder>();
        if (adder)
        {
            adder.multiplyPointsToAdd(.5f);
        }
    }
}

