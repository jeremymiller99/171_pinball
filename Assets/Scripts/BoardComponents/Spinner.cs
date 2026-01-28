using System.Drawing;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class Spinner : MonoBehaviour
{

    [Tooltip("How much the relative velocity gets multiplied when adding to the rigid body's angular velocity.")]
    [SerializeField] private float forcingConstant;

    private float prevAngle = 0;

    private Rigidbody rb;
    private PointAdder pa;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        pa = GetComponent<PointAdder>();
    }

    void OnCollisionEnter(Collision collision)
    {
        rb.angularVelocity += (collision.relativeVelocity * forcingConstant);
    }

    void Update()
    {
        Quaternion newQ = transform.rotation.normalized;
        newQ.ToAngleAxis(out float newAngle, out Vector3 newAxis);

        if (prevAngle == 0)
        {
            prevAngle = newAngle;
            return;
        }
        
        if ((newAngle >= 170 && prevAngle < 170) || (newAngle >= 250 && prevAngle < 250))
        {
            pa.AddPoints(transform);
        }
        prevAngle = newAngle;

    }
}
