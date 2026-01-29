using System.Drawing;
using Unity.Mathematics;
using UnityEngine;

public class Spinner : MonoBehaviour
{
    [SerializeField] private float prevAngle = 0;
    [SerializeField] private PointAdder pa;
    [SerializeField] private Transform bt;

    void Awake()
    {
        pa = GetComponent<PointAdder>();
    }
    void FixedUpdate()
    {
        
        float newAngle = transform.rotation.eulerAngles.z;
        if (prevAngle == 0)
        {
            prevAngle = newAngle;
            return;
        }

        if ((newAngle <= 100 && prevAngle >= 300) || (newAngle >= 300 && prevAngle <= 100))
        {
            pa.AddPoints(transform);
        }
        prevAngle = newAngle;
    }

    void OnCollisionEnter(Collision collision)
    {
        
        if (collision.collider.CompareTag("Ball"))
        {
            bt = collision.collider.transform;
        }
    }
}
