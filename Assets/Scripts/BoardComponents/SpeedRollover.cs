using UnityEngine;

public class SpeedRollover : MonoBehaviour
{
    [SerializeField] private Rigidbody rigidBody;
    [SerializeField] private float forceAdder;
    [SerializeField] private float maxVelocity = 50;
    void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
    }
    void OnTriggerStay(Collider other)
    {
        if (maxVelocity < rigidBody.linearVelocity.z && rigidBody.linearVelocity.z > 0)
        {
            rigidBody.AddForce(Vector3.forward * forceAdder);
        }
    }
}
