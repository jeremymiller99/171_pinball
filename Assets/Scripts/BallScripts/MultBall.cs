using UnityEngine;

public class MultBall : Ball
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //Doubles mults's multtoadd when it enters, halves it when collision is over.
    void OnCollisionEnter(Collision collision)
    {
        MultAdder adder = collision.collider.GetComponent<MultAdder>();
        if(adder)
        {
            adder.multiplyMultToAdd(2);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        MultAdder adder = collision.collider.GetComponent<MultAdder>();
        if(adder)
        {
            adder.multiplyMultToAdd(.5f);
        }
    }
}
