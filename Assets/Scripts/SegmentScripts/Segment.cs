using Unity.Mathematics;
using UnityEngine;


public class Segment : MonoBehaviour
{
    public enum segmentType
    {
        topLeft,
        right
    }
    [SerializeField] private Vector3 startingSpot;

    public segmentType type;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        transform.SetLocalPositionAndRotation(startingSpot, quaternion.identity);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
