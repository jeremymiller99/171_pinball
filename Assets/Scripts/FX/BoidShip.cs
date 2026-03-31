// Generated with Cursor on 2026-03-15.
using UnityEngine;

public enum MovementPlane
{
    XY,
    XZ,
    YZ
}

public class BoidShip : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [Tooltip("How quickly velocity changes toward desired direction. Lower = smoother turns.")]
    [SerializeField] private float velocitySmoothSpeed = 4f;

    [Header("Wandering")]
    [Tooltip("Distance to waypoint before picking a new random target.")]
    [SerializeField] private float waypointArrivalThreshold = 8f;

    [Header("Boids")]
    [Tooltip("Radius to detect nearby ships for flocking.")]
    [SerializeField] private float perceptionRadius = 8f;
    [Tooltip("Distance at which separation (avoidance) kicks in.")]
    [SerializeField] private float separationRadius = 10f;
    [SerializeField] private float separationWeight = 0.5f;
    [SerializeField] private float alignmentWeight = 0.3f;
    [SerializeField] private float cohesionWeight = 0.3f;
    [Tooltip("Weight for steering toward the current waypoint.")]
    [SerializeField] private float seekWeight = 1f;

    [Header("Rotation")]
    [Tooltip("Optional child that defines the ship's forward. If set, rotation uses this instead of movement plane.")]
    [SerializeField] private Transform forwardIndicator;
    public Transform ForwardIndicator => forwardIndicator;
    [Tooltip("Plane ships move in. Used when forward indicator is not set. XZ = vertical pinball board.")]
    [SerializeField] private MovementPlane movementPlane = MovementPlane.XZ;
    [Tooltip("Degrees to add to facing angle (e.g. 90 if model nose points wrong way).")]
    [SerializeField] private float rotationOffsetDegrees = 0f;
    [Tooltip("Smoothing for rotation. Lower = smoother, more gradual turns.")]
    [SerializeField] private float rotationSmoothSpeed = 3f;
    [Tooltip("Max rotation around X axis in degrees. Keeps ships from tilting too much.")]
    [SerializeField] private float maxXRotationDegrees = 15f;

    private Vector3 targetPosition;
    private Vector3 velocity;
    private Collider moveableArea;

    /// <summary>Current movement velocity for boids alignment.</summary>
    public Vector3 Velocity => velocity;

    /// <summary>Ship's forward direction. Uses forward indicator child if set, otherwise transform.forward.</summary>
    public Vector3 Forward => forwardIndicator != null ? forwardIndicator.forward : transform.forward;

    private void Update()
    {
        if (moveableArea == null)
        {
            return;
        }

        Vector3 toTarget = targetPosition - transform.position;
        float distance = toTarget.magnitude;

        if (distance <= waypointArrivalThreshold)
        {
            PickNewWaypoint();
            toTarget = targetPosition - transform.position;
            distance = toTarget.magnitude;
        }

        Vector3 desiredVelocity = ComputeDesiredVelocity(toTarget, distance);
        velocity = Vector3.Lerp(velocity, desiredVelocity, velocitySmoothSpeed * Time.deltaTime);
        velocity = Vector3.ClampMagnitude(velocity, moveSpeed);

        transform.position += velocity * Time.deltaTime;

        if (velocity.sqrMagnitude > 0.0001f)
        {
            UpdateRotation(velocity.normalized);
        }
    }

    private Vector3 ComputeDesiredVelocity(Vector3 toTarget, float distanceToTarget)
    {
        Vector3 seek = toTarget.normalized * moveSpeed;
        Vector3 separation = ComputeSeparation();
        Vector3 alignment = ComputeAlignment();
        Vector3 cohesion = ComputeCohesion();

        Vector3 desired = (seek * seekWeight) + (separation * separationWeight) +
            (alignment * alignmentWeight) + (cohesion * cohesionWeight);

        if (desired.sqrMagnitude < 0.0001f)
        {
            return seek;
        }

        return desired.normalized * moveSpeed;
    }

    private Vector3 ComputeSeparation()
    {
        Vector3 steer = Vector3.zero;
        int count = 0;

        BoidShip[] allShips = FindObjectsByType<BoidShip>(FindObjectsSortMode.None);
        foreach (BoidShip other in allShips)
        {
            if (other == null || other == this)
            {
                continue;
            }

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist < separationRadius && dist > 0.0001f)
            {
                Vector3 away = (transform.position - other.transform.position).normalized;
                steer += away * (1f - dist / separationRadius);
                count++;
            }
        }

        if (count == 0)
        {
            return Vector3.zero;
        }

        steer /= count;
        return steer.sqrMagnitude > 0.0001f ? steer.normalized * moveSpeed : Vector3.zero;
    }

    private Vector3 ComputeAlignment()
    {
        Vector3 avgVelocity = Vector3.zero;
        int count = 0;

        BoidShip[] allShips = FindObjectsByType<BoidShip>(FindObjectsSortMode.None);
        foreach (BoidShip other in allShips)
        {
            if (other == null || other == this)
            {
                continue;
            }

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist < perceptionRadius && dist > 0.0001f)
            {
                avgVelocity += other.Velocity;
                count++;
            }
        }

        if (count == 0)
        {
            return Vector3.zero;
        }

        avgVelocity /= count;
        return avgVelocity.sqrMagnitude > 0.0001f ? avgVelocity.normalized * moveSpeed : Vector3.zero;
    }

    private Vector3 ComputeCohesion()
    {
        Vector3 centerOfMass = Vector3.zero;
        int count = 0;

        BoidShip[] allShips = FindObjectsByType<BoidShip>(FindObjectsSortMode.None);
        foreach (BoidShip other in allShips)
        {
            if (other == null || other == this)
            {
                continue;
            }

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist < perceptionRadius && dist > 0.0001f)
            {
                centerOfMass += other.transform.position;
                count++;
            }
        }

        if (count == 0)
        {
            return Vector3.zero;
        }

        centerOfMass /= count;
        Vector3 toCenter = centerOfMass - transform.position;
        return toCenter.sqrMagnitude > 0.0001f ? toCenter.normalized * moveSpeed : Vector3.zero;
    }

    private void UpdateRotation(Vector3 direction)
    {
        Vector3 flatDirection = ProjectDirectionToPlane(direction);
        if (flatDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        flatDirection = flatDirection.normalized;

        Quaternion targetRot;

        if (forwardIndicator != null)
        {
            Vector3 currentForward = forwardIndicator.forward;
            if (currentForward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 flatCurrent = ProjectDirectionToPlane(currentForward);
            if (flatCurrent.sqrMagnitude < 0.0001f)
            {
                return;
            }

            flatCurrent = flatCurrent.normalized;
            targetRot = Quaternion.FromToRotation(flatCurrent, flatDirection) * transform.rotation;
        }
        else
        {
            float angle;
            switch (movementPlane)
            {
                case MovementPlane.XY:
                    angle = Mathf.Atan2(flatDirection.y, flatDirection.x) * Mathf.Rad2Deg;
                    break;
                case MovementPlane.XZ:
                    angle = Mathf.Atan2(flatDirection.z, flatDirection.x) * Mathf.Rad2Deg;
                    break;
                case MovementPlane.YZ:
                    angle = Mathf.Atan2(flatDirection.z, flatDirection.y) * Mathf.Rad2Deg;
                    break;
                default:
                    angle = Mathf.Atan2(flatDirection.z, flatDirection.x) * Mathf.Rad2Deg;
                    break;
            }

            angle += rotationOffsetDegrees;

            switch (movementPlane)
            {
                case MovementPlane.XY:
                    targetRot = Quaternion.Euler(0f, 0f, angle);
                    break;
                case MovementPlane.XZ:
                    targetRot = Quaternion.Euler(0f, angle, 0f);
                    break;
                case MovementPlane.YZ:
                    targetRot = Quaternion.Euler(angle, 0f, 0f);
                    break;
                default:
                    targetRot = Quaternion.Euler(0f, angle, 0f);
                    break;
            }
        }

        Vector3 targetEuler = targetRot.eulerAngles;
        float targetX = targetEuler.x;
        if (targetX > 180f)
        {
            targetX -= 360f;
        }

        targetX = Mathf.Clamp(targetX, -maxXRotationDegrees, maxXRotationDegrees);
        targetRot = Quaternion.Euler(targetX, targetEuler.y, targetEuler.z);

        Quaternion rot = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotationSmoothSpeed * Time.deltaTime);

        Vector3 euler = rot.eulerAngles;
        float x = euler.x;
        if (x > 180f)
        {
            x -= 360f;
        }

        euler.x = Mathf.Clamp(x, -maxXRotationDegrees, maxXRotationDegrees);
        transform.rotation = Quaternion.Euler(euler);
    }

    private Vector3 ProjectDirectionToPlane(Vector3 dir)
    {
        switch (movementPlane)
        {
            case MovementPlane.XY:
                return new Vector3(dir.x, dir.y, 0f);
            case MovementPlane.XZ:
                return new Vector3(dir.x, 0f, dir.z);
            case MovementPlane.YZ:
                return new Vector3(0f, dir.y, dir.z);
            default:
                return new Vector3(dir.x, 0f, dir.z);
        }
    }

    /// <summary>Set by spawner: bounds to fly within and speed.</summary>
    public void SetFlyingArea(Collider area, float speed)
    {
        moveableArea = area;
        moveSpeed = Mathf.Max(0f, speed);
        PickNewWaypoint();
        velocity = (targetPosition - transform.position).normalized * moveSpeed;
    }

    private void PickNewWaypoint()
    {
        if (moveableArea == null)
        {
            return;
        }

        Bounds bounds = moveableArea.bounds;
        targetPosition = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            Random.Range(bounds.min.z, bounds.max.z));
    }
}
