using TMPro;
using UnityEngine;

public class Drone : MonoBehaviour
{
    [SerializeField] private float speed = 7.5f;
    [SerializeField] private float rotationSpeed = 3f;
    [SerializeField] private Collider box;
    [SerializeField] protected Vector3 targetPosition;
    [SerializeField] private float arriveDistance = 0.25f;

    protected virtual void Start()
    {
        PickNewTarget();
    }

    protected virtual void Update()
    {
        if (box == null) return;

        Vector3 toTarget = targetPosition - transform.position;

        if (toTarget.magnitude <= arriveDistance)
        {
            PickNewTarget();
            toTarget = targetPosition - transform.position;
        }

        Vector3 direction = toTarget.normalized;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            speed * Time.deltaTime);

        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }
    }

    private void PickNewTarget()
    {
        Bounds bounds = box.bounds;

        targetPosition = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            Random.Range(bounds.min.z, bounds.max.z));
    }

    public void SetBox(Collider box)
    {
        this.box = box;
    }   
}