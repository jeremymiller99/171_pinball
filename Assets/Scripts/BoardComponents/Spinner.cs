using System.Drawing;
using Unity.Mathematics;
using UnityEngine;

public class Spinner : MonoBehaviour
{
    [SerializeField] private float prevAngle = 0;
    [SerializeField] private PointAdder pointAdder;
    [SerializeField] private float maxAngle = 300;
    [SerializeField] private float minAngle = 100;

    void Awake()
    {
        pointAdder = GetComponent<PointAdder>();
    }
    void FixedUpdate()
    {
        
        float newAngle = transform.rotation.eulerAngles.z;
        if (prevAngle == 0)
        {
            prevAngle = newAngle;
            return;
        }

        if ((newAngle <= minAngle && prevAngle >= maxAngle) || (newAngle >= maxAngle && prevAngle <= minAngle))
        {
            pointAdder.AddPoints(transform);
        }
        prevAngle = newAngle;
    }
}
