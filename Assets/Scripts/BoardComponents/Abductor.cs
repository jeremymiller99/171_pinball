// Change: replaced MoveTowards/LookAt darting with velocity-smoothed steering,
// smoothed banking heading and an idle hover, plus a hit flash / particle burst / recoil
// when the ball connects.
using UnityEngine;
using System.Collections.Generic;

public class Abductor : MonoBehaviour
{
    private enum AbductionState
    {
        Idle,
        GoingToAbduction,
        Abducting,
        Fighting,
        ReturningAbductedObject,
        Leaving
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private AbductionState abductionState;
    [SerializeField] private Vector3 startingPosition;
    [SerializeField] private FrenzyManager frenzyManager;
    [SerializeField] private bool activatedFrenzy;
    [Tooltip("Point where the frenzy VFX spawns. Falls back to the abducted object's position.")]
    [SerializeField] private Transform frenzyVFXPoint;

    [Header("Going To Abduction")]
    [SerializeField] private Transform abductionPosition;
    [SerializeField] private float abductionSpeed;

    [Header("Abduction Process")]
    [SerializeField] private GameObject objectToAbduct;
    [SerializeField] private float abductingObjectSpeed;
    [SerializeField] private Transform targetAbductionPosition;
    [SerializeField] private float abductionShrinkingSpeed;

    [Header("Fighting")]
    [SerializeField] private GameObject fightingPositionParent;
    [SerializeField] private List<Vector3> fightingPositions;
    [SerializeField] private int fightingPositionIndex = 0;
    [SerializeField] private float fightingMovementSpeed;
    [SerializeField] private int maxHealth;
    [SerializeField] private int health;

    [Header("Returning Abducted Object")]
    [SerializeField] private Vector3 returningObjectPosition;
    [SerializeField] private Vector3 abductedObjectSize;

    [Header("Movement Feel")]
    [Tooltip("How fast velocity turns toward the desired direction. " +
        "Lower = smoother, wider arcs. The speed fields above stay the top speed.")]
    [SerializeField] private float velocitySmoothSpeed = 3f;
    [Tooltip("Distance from a point that counts as arrived when the ship has to settle there.")]
    [SerializeField] private float arriveRadius = 1.5f;
    [Tooltip("Distance from a fighting waypoint that advances to the next one. " +
        "Larger = the ship curves through waypoints instead of stopping dead at each.")]
    [SerializeField] private float fightingArriveRadius = 4f;
    [Tooltip("Distance at which the ship starts easing to a stop when holding a position.")]
    [SerializeField] private float slowDownRadius = 6f;

    [Header("Heading")]
    [Tooltip("How fast the hull swings to face its heading. Lower = lazier turns.")]
    [SerializeField] private float turnSmoothSpeed = 6f;
    [Tooltip("Degrees of roll per degree-per-second of turn rate.")]
    [SerializeField] private float bankPerTurnRate = 0.12f;
    [SerializeField] private float maxBankAngle = 28f;
    [SerializeField] private float bankSmoothSpeed = 5f;

    [Header("Idle Hover")]
    [SerializeField] private float hoverAmplitude = 0.25f;
    [SerializeField] private float hoverFrequency = 0.9f;
    [SerializeField] private float swayAmplitude = 0.12f;
    [SerializeField] private float swayFrequency = 0.6f;

    [Header("Hit Reaction")]
    [SerializeField] private Color hitFlashColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.18f;
    [Tooltip("How hard the flash colour is pushed into emission. 0 disables the glow.")]
    [SerializeField] private float hitFlashEmissionIntensity = 2f;
    [Tooltip("Optional one-shot VFX prefab (e.g. a CFXR spark) spawned at the impact point. " +
        "Destroyed after hitParticlesLifetime.")]
    [SerializeField] private GameObject hitParticlesPrefab;
    [SerializeField] private float hitParticlesLifetime = 2f;
    [Tooltip("Optional existing burst emitter, used instead of / alongside the prefab. " +
        "Set its Simulation Space to World so the particles don't ride the ship.")]
    [SerializeField] private ParticleSystem hitParticles;
    [SerializeField] private int hitParticleCount = 24;
    [Tooltip("Speed kicked into the ship away from the impact, so the hit reads in its motion too.")]
    [SerializeField] private float hitRecoilSpeed = 9f;

    private Vector3 velocity;
    private Vector3 logicalPosition;
    private Vector3 visualOffset;
    private Vector3 heading = Vector3.forward;
    private float bobTime;
    private float bankAngle;

    private Renderer[] flashRenderers;
    // Per renderer, per submesh: the hull is a single MeshRenderer with several material
    // slots, so one colour per renderer would tint every slot with slot 0's colour.
    private Color[][] originalBaseColors;
    private Color[][] originalEmissionColors;
    private MaterialPropertyBlock propertyBlock;
    private float flashTimer;
    private bool flashApplied;

    private void Awake()
    {
        frenzyManager = FindAnyObjectByType<FrenzyManager>();
        CacheFlashRenderers();
    }

    private void OnEnable()
    {
        // The bob is a visual offset on top of logicalPosition, so peel it back off
        // before taking ownership of the transform again.
        logicalPosition = transform.position - visualOffset;
        visualOffset = Vector3.zero;
        velocity = Vector3.zero;
        bankAngle = 0f;
        bobTime = 0f;
        heading = transform.forward;
    }

    private void OnDisable()
    {
        flashTimer = 0f;
        ClearFlash();
    }

    private void Start()
    {
        startingPosition = transform.position;
        gameObject.SetActive(false);
        abductionState = AbductionState.Idle;
        foreach (Transform child in fightingPositionParent.transform)
        {
            fightingPositions.Add(child.position);
        }
    }

    private void Update()
    {
        UpdateHitFlash();

        switch (abductionState)
        {
            case AbductionState.GoingToAbduction:
                SteerToward(abductionPosition.position, abductionSpeed, true);

                if (HasArrived(abductionPosition.position, arriveRadius))
                {
                    abductedObjectSize = objectToAbduct.transform.localScale;
                    returningObjectPosition = objectToAbduct.transform.position;
                    abductionState = AbductionState.Abducting;
                }
                break;
            case AbductionState.Abducting:
                // Keep steering at the abduction point so the ship hovers over the beam
                // instead of freezing the frame it arrives.
                SteerToward(abductionPosition.position, abductionSpeed, true);

                objectToAbduct.transform.position = Vector3.MoveTowards(objectToAbduct.transform.position, targetAbductionPosition.position, abductingObjectSpeed * Time.deltaTime);
                objectToAbduct.transform.localScale -= Time.deltaTime * abductionShrinkingSpeed * Vector3.one;
                if (objectToAbduct.transform.position == targetAbductionPosition.position)
                {
                    objectToAbduct.GetComponentInChildren<Renderer>().enabled = false;
                    objectToAbduct.GetComponentInChildren<Collider>().enabled = false;
                    abductionState = AbductionState.Fighting;
                    fightingPositionIndex = 0;
                }
                break;
            case AbductionState.Fighting:
                UpdateFighting();
                break;
            case AbductionState.ReturningAbductedObject:
                SteerToward(abductionPosition.position, abductionSpeed, true);
                if (HasArrived(abductionPosition.position, arriveRadius))
                {
                    objectToAbduct.GetComponentInChildren<Renderer>().enabled = true;
                    objectToAbduct.GetComponentInChildren<Collider>().enabled = true;
                    objectToAbduct.GetComponent<OnDropAbduction>().gettingAbducted = false;
                    objectToAbduct.transform.position = Vector3.MoveTowards(objectToAbduct.transform.position, returningObjectPosition, abductingObjectSpeed * Time.deltaTime);
                    objectToAbduct.transform.localScale += Time.deltaTime * abductionShrinkingSpeed * Vector3.one;
                }

                if (objectToAbduct.transform.position == returningObjectPosition)
                {
                    objectToAbduct.transform.localScale = abductedObjectSize;
                    abductionState = AbductionState.Leaving;
                }
                break;
            case AbductionState.Leaving:
                if (!activatedFrenzy)
                {
                    Vector3 frenzyVFXPos = frenzyVFXPoint != null
                        ? frenzyVFXPoint.position
                        : objectToAbduct.transform.position;
                    frenzyManager.ActivateFrenzy(frenzyVFXPos);
                    activatedFrenzy = true;
                }

                SteerToward(startingPosition, abductionSpeed, true);
                break;

        }

        UpdateHeading();
        ApplyHover();
    }

    public void StartAbduction()
    {
        gameObject.SetActive(true);
        abductionState = AbductionState.GoingToAbduction;
        activatedFrenzy = false;
        health = maxHealth;
        flashTimer = 0f;
        ClearFlash();
    }

    private void UpdateFighting()
    {
        if (fightingPositions == null || fightingPositions.Count == 0)
        {
            return;
        }

        Vector3 waypoint = fightingPositions[fightingPositionIndex];

        // No easing here: the ship should carve through the waypoint at speed rather
        // than brake into it, which is what made the old movement look like darting.
        SteerToward(waypoint, fightingMovementSpeed, false);

        // Curving wide can leave the ship just outside the arrive radius while already
        // heading away. Only treat that as arrived once it is actually near the point,
        // so leftover velocity from the previous state can't skip a waypoint.
        Vector3 toWaypoint = waypoint - logicalPosition;
        bool overshot = velocity.sqrMagnitude > 0.01f &&
            toWaypoint.sqrMagnitude <= 4f * fightingArriveRadius * fightingArriveRadius &&
            Vector3.Dot(toWaypoint, velocity) <= 0f;

        if (HasArrived(waypoint, fightingArriveRadius) || overshot)
        {
            fightingPositionIndex = (fightingPositionIndex + 1) % fightingPositions.Count;
        }
    }

    /// <summary>
    /// Accelerates toward <paramref name="target"/> instead of snapping along a straight
    /// line. <paramref name="easeIn"/> bleeds off speed near the target for the states
    /// that have to settle on a point.
    /// </summary>
    private void SteerToward(Vector3 target, float maxSpeed, bool easeIn)
    {
        Vector3 toTarget = target - logicalPosition;
        float distance = toTarget.magnitude;

        float desiredSpeed = maxSpeed;
        if (easeIn && slowDownRadius > 0.01f && distance < slowDownRadius)
        {
            desiredSpeed = maxSpeed * Mathf.SmoothStep(0f, 1f, distance / slowDownRadius);
        }

        Vector3 desiredVelocity = distance > 0.0001f
            ? toTarget / distance * desiredSpeed
            : Vector3.zero;

        velocity = Vector3.Lerp(velocity, desiredVelocity, SmoothingFactor(velocitySmoothSpeed));
        logicalPosition += velocity * Time.deltaTime;
    }

    private bool HasArrived(Vector3 target, float radius)
    {
        // Never let the radius fall below one frame of travel, or a fast ship can
        // step straight over the target and never register the arrival.
        float safeRadius = Mathf.Max(radius, velocity.magnitude * Time.deltaTime * 1.5f);
        return (target - logicalPosition).sqrMagnitude <= safeRadius * safeRadius;
    }

    private void UpdateHeading()
    {
        float targetBank = 0f;

        if (velocity.sqrMagnitude > 0.01f)
        {
            Vector3 newHeading = velocity.normalized;
            float turnRate = Vector3.SignedAngle(heading, newHeading, Vector3.up) /
                Mathf.Max(Time.deltaTime, 0.0001f);
            targetBank = Mathf.Clamp(-turnRate * bankPerTurnRate, -maxBankAngle, maxBankAngle);
            heading = newHeading;
        }

        bankAngle = Mathf.Lerp(bankAngle, targetBank, SmoothingFactor(bankSmoothSpeed));

        Quaternion targetRotation = Quaternion.LookRotation(heading, UpFor(heading)) *
            Quaternion.Euler(0f, 0f, bankAngle);

        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRotation, SmoothingFactor(turnSmoothSpeed));
    }

    private void ApplyHover()
    {
        bobTime += Time.deltaTime;

        float bob = Mathf.Sin(bobTime * hoverFrequency * Mathf.PI * 2f) * hoverAmplitude;
        float sway = Mathf.Sin(bobTime * swayFrequency * Mathf.PI * 2f + 1.3f) * swayAmplitude;

        visualOffset = (Vector3.up * bob) + (transform.right * sway);
        transform.position = logicalPosition + visualOffset;
    }

    /// <summary>
    /// Up vector for a LookRotation, swapped out when the direction is straight up or
    /// down — LookRotation logs an error and returns identity for a parallel pair.
    /// </summary>
    private static Vector3 UpFor(Vector3 direction)
    {
        return Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.999f
            ? Vector3.forward
            : Vector3.up;
    }

    /// <summary>Framerate-independent lerp weight for an exponential approach.</summary>
    private static float SmoothingFactor(float speed)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0f, speed) * Time.deltaTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponent<Ball>() == null)
        {
            return;
        }

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : collision.transform.position;

        ReactToHit(hitPoint);

        if (abductionState != AbductionState.Fighting)
        {
            return;
        }

        health--;
        ServiceLocator.Get<AudioManager>()?.PlayBumperHit(transform.position);

        if (health <= 0)
        {
            abductionState = AbductionState.ReturningAbductedObject;
        }
    }

    private void ReactToHit(Vector3 hitPoint)
    {
        flashTimer = hitFlashDuration;
        ApplyFlash(1f);

        Vector3 awayFromHit = logicalPosition - hitPoint;
        awayFromHit = awayFromHit.sqrMagnitude > 0.0001f
            ? awayFromHit.normalized
            : Vector3.up;

        // Recoil pushes toward the ship's centre, so the spray is the opposite way:
        // outward through the hull, back along the line the ball came in on.
        Vector3 sprayDirection = -awayFromHit;
        Quaternion sprayRotation = Quaternion.LookRotation(sprayDirection, UpFor(sprayDirection));

        if (hitParticlesPrefab != null)
        {
            GameObject burst = Instantiate(hitParticlesPrefab, hitPoint, sprayRotation);
            Destroy(burst, Mathf.Max(0.1f, hitParticlesLifetime));
        }

        if (hitParticles != null)
        {
            hitParticles.transform.SetPositionAndRotation(hitPoint, sprayRotation);
            hitParticles.Emit(Mathf.Max(1, hitParticleCount));
        }

        velocity += awayFromHit * hitRecoilSpeed;
    }

    private void UpdateHitFlash()
    {
        if (flashTimer <= 0f)
        {
            return;
        }

        flashTimer -= Time.deltaTime;

        if (flashTimer <= 0f)
        {
            ClearFlash();
            return;
        }

        float t = hitFlashDuration > 0.0001f ? flashTimer / hitFlashDuration : 0f;
        ApplyFlash(t * t);
    }

    private void CacheFlashRenderers()
    {
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        List<Renderer> hull = new List<Renderer>(all.Length);

        for (int i = 0; i < all.Length; i++)
        {
            // Particle and trail renderers own their own colours; tinting them would
            // recolour the hit burst itself.
            if (all[i] == null || all[i] is ParticleSystemRenderer || all[i] is TrailRenderer)
            {
                continue;
            }

            hull.Add(all[i]);
        }

        flashRenderers = hull.ToArray();
        originalBaseColors = new Color[flashRenderers.Length][];
        originalEmissionColors = new Color[flashRenderers.Length][];

        for (int i = 0; i < flashRenderers.Length; i++)
        {
            Material[] materials = flashRenderers[i].sharedMaterials;
            originalBaseColors[i] = new Color[materials.Length];
            originalEmissionColors[i] = new Color[materials.Length];

            for (int slot = 0; slot < materials.Length; slot++)
            {
                originalBaseColors[i][slot] =
                    ReadColor(materials[slot], Color.white, BaseColorId, ColorId);
                originalEmissionColors[i][slot] =
                    ReadColor(materials[slot], Color.black, EmissionColorId);
            }
        }
    }

    private static Color ReadColor(Material material, Color fallback, params int[] propertyIds)
    {
        if (material == null)
        {
            return fallback;
        }

        for (int i = 0; i < propertyIds.Length; i++)
        {
            if (material.HasProperty(propertyIds[i]))
            {
                return material.GetColor(propertyIds[i]);
            }
        }

        return fallback;
    }

    private void ApplyFlash(float amount)
    {
        if (flashRenderers == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < flashRenderers.Length; i++)
        {
            Renderer renderer = flashRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            // Only the slots cached in Awake. Outline appends its mask/fill materials on
            // enable, and those own their colours — they must not be tinted.
            Color[] baseColors = originalBaseColors[i];
            Color[] emissionColors = originalEmissionColors[i];

            for (int slot = 0; slot < baseColors.Length; slot++)
            {
                Color baseColor = Color.Lerp(baseColors[slot], hitFlashColor, amount);
                Color emission = Color.Lerp(
                    emissionColors[slot],
                    hitFlashColor * hitFlashEmissionIntensity,
                    amount);

                renderer.GetPropertyBlock(propertyBlock, slot);
                propertyBlock.SetColor(BaseColorId, baseColor);
                propertyBlock.SetColor(ColorId, baseColor);
                propertyBlock.SetColor(EmissionColorId, emission);
                renderer.SetPropertyBlock(propertyBlock, slot);
            }
        }

        flashApplied = true;
    }

    private void ClearFlash()
    {
        if (!flashApplied || flashRenderers == null)
        {
            return;
        }

        // Drop the blocks rather than writing the originals back, so the materials render
        // exactly as authored again. Nothing else on the ship sets a block on these slots.
        for (int i = 0; i < flashRenderers.Length; i++)
        {
            Renderer renderer = flashRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            for (int slot = 0; slot < originalBaseColors[i].Length; slot++)
            {
                renderer.SetPropertyBlock(null, slot);
            }
        }

        flashApplied = false;
    }
}
