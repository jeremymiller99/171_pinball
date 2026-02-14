using UnityEngine;

public class MultBall : Ball
{
    [SerializeField] private float amountToMultiply;

    void OnCollisionEnter(Collision collision)
    {
        MultAdder adder = collision.collider.GetComponent<MultAdder>();
        if(adder)
        {
            adder.multiplyMultToAdd(amountToMultiply);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        MultAdder adder = collision.collider.GetComponent<MultAdder>();
        if(adder)
        {
            adder.multiplyMultToAdd(1 / amountToMultiply);
        }
    }

    void OnTriggerEnter(Collider collider)
    {
        MultAdder adder = collider.GetComponent<MultAdder>();
        if(adder)
        {
            adder.multiplyMultToAdd(amountToMultiply);
        }
    }

    void OnTriggerExit(Collider collider)
    {
        MultAdder adder = collider.GetComponent<MultAdder>();
        if(adder)
        {
            adder.multiplyMultToAdd(1 / amountToMultiply);
        }
    }
}
