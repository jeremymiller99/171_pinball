using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Projectile fired by the Plasma Launcher. Glides across the board without
/// colliding and activates every board component it passes through, each at
/// most once per short cooldown. Portals are skipped so the projectile cannot
/// fire teleports, and activations use the weak-shake scoring path so a sweep
/// through a cluster does not hammer the camera.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class PlasmaBall : MonoBehaviour
{
    [SerializeField] private float lifetimeSeconds = 4f;
    [SerializeField] private float perComponentCooldownSeconds = 0.5f;

    private readonly Dictionary<int, float> _lastActivationById = new Dictionary<int, float>();
    private Vector3 _velocity;
    private float _secondsAlive;

    public void Launch(Vector3 velocity)
    {
        _velocity = velocity;
    }

    private void Update()
    {
        transform.position += _velocity * Time.deltaTime;

        _secondsAlive += Time.deltaTime;
        if (_secondsAlive >= lifetimeSeconds)
        {
            ChargeDebug.Log($"{name} fizzles out");
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        BoardComponent[] components = Ball.GetBoardComponentsForScoring(other);

        foreach (BoardComponent component in components)
        {
            if (component == null || component.componentType == BoardComponentType.Portal)
            {
                continue;
            }

            int id = component.GetInstanceID();
            if (_lastActivationById.TryGetValue(id, out float lastTime)
                && Time.time - lastTime < perComponentCooldownSeconds)
            {
                continue;
            }

            _lastActivationById[id] = Time.time;
            ChargeDebug.Log($"{name} activates {component.name}");
            component.ActivateAsBurnTick();
        }
    }
}
