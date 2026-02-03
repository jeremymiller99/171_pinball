using UnityEngine;

public class SpeedRollover : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float forceAdder;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }
    void OnTriggerStay(Collider other)
    {
        if (50 < rb.linearVelocity.z && rb.linearVelocity.z > 0)
        {
            rb.AddForce(Vector3.forward * forceAdder);
        }
    }
}
