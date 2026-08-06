// Change: replaced MoveTowards/LookAt darting with velocity-smoothed steering,
// smoothed banking heading and an idle hover, plus a hit flash / particle burst / recoil
// when the ball connects.
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

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
    [FormerlySerializedAs("frenzyManager")]
    [SerializeField] private PowerSurgeManager powerSurgeManager;
    [FormerlySerializedAs("activatedFrenzy")]
    [SerializeField] private bool activatedPowerSurge;
    [Tooltip("Point where the Power Surge VFX spawns. Falls back to the abducted object's position.")]
    [FormerlySerializedAs("frenzyVFXPoint")]
    [SerializeField] private Transform powerSurgeVFXPoint;

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

    [Header("Abduction Hologram")]
    [Tooltip("Material the abducted object wears while it is beamed up and put back " +
        "(e.g. M_NavHologram). Its own materials are restored once it is fully returned. " +
        "Leave empty to keep the object looking normal throughout.")]
    [SerializeField] private Material abductionHologramMaterial;

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

    [Header("Drop Target Progress Lights")]
    [Tooltip("Cylinders that light up one per hit on the abduction drop target, in order. " +
        "They MUST live outside this ship: it is deactivated for most of the game (so " +
        "children would be hidden with it) and the hit flash tints every renderer under it.")]
    [SerializeField] private Renderer[] progressLights;
    [Tooltip("Material worn before that light's hit lands — the grey/off look.")]
    [SerializeField] private Material progressLightOffMaterial;
    [Tooltip("Material swapped in once the hit lands — the lit look.")]
    [SerializeField] private Material progressLightOnMaterial;
    [Tooltip("Once every light is lit and the ship is out, they blink between the on and off " +
        "materials at this rate (full on-off cycles per second). 0 leaves them solid lit.")]
    [SerializeField] private float progressLightFlashRate = 3f;

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

    // Abducted-object hologram state. The originals are held as the shared assets read
    // off the renderers, so restoring writes back exactly what was authored.
    private Renderer[] hologramRenderers;
    private Material[][] hologramOriginalMaterials;
    private Outline[] hologramOutlines;
    private bool[] hologramOutlineWasEnabled;
    private bool hologramApplied;

    // Progress-light state. Driven by Dropper events rather than polled in Update:
    // the ship is inactive between abductions, so its Update doesn't run while the
    // player is knocking the target down — but a delegate still reaches it.
    private Dropper progressDropper;
    private bool progressLatched;
    private bool progressFlashing;
    private float progressFlashTimer;
    private bool progressFlashOn;

    private void Awake()
    {
        powerSurgeManager = FindAnyObjectByType<PowerSurgeManager>();
        CacheFlashRenderers();
        SubscribeProgressLights();
        ResetProgressLights();

        // Subscribe here, not in OnEnable: the abductor spends most of the game
        // inactive (it deactivates itself in Start), so an OnEnable/OnDisable pair
        // would miss any replacement that happens while it's hidden — including a
        // shop swap of the very object it abducts.
        BoardComponent.Replaced += OnComponentReplaced;
    }

    private void OnDestroy()
    {
        BoardComponent.Replaced -= OnComponentReplaced;
        UnsubscribeProgressLights();
    }

    // The abducted object can be swapped out in the shop. Re-point to the
    // replacement and re-create its OnDropAbduction wiring, so the abduction
    // sequence doesn't dereference the destroyed original (a NullReferenceException
    // that would abort the sequence mid-way).
    private void OnComponentReplaced(
        BoardComponent oldComponent, BoardComponent newComponent)
    {
        if (oldComponent == null || newComponent == null) return;
        if (objectToAbduct == null || oldComponent.gameObject != objectToAbduct)
            return;

        // The cached renderers belong to the instance being destroyed, so restoring
        // through them would be pointless at best. Forget them and re-skin the
        // replacement instead, so a swap mid-abduction doesn't strand either object.
        bool wasHolographic = hologramApplied;
        ForgetHologramCache();

        objectToAbduct = newComponent.gameObject;

        if (wasHolographic)
        {
            ApplyAbductionHologram();
        }

        // The OnDropAbduction satellite is a scene-only addition, so the fresh
        // prefab instance won't carry one — read the old wiring, then rebuild it on
        // the replacement pointing at the same dropper and this abductor.
        OnDropAbduction oldWiring = oldComponent.GetComponent<OnDropAbduction>();
        Dropper dropper = oldWiring != null ? oldWiring.DropTarget : null;

        OnDropAbduction newWiring = newComponent.GetComponent<OnDropAbduction>();
        if (newWiring == null)
        {
            newWiring = newComponent.gameObject.AddComponent<OnDropAbduction>();
        }
        newWiring.Configure(dropper, this);

        // The dropper itself survives a swap of the abducted object, but re-point the
        // lights through the new wiring anyway so they can't end up listening to a
        // dropper the replacement no longer uses.
        UnsubscribeProgressLights();
        SubscribeProgressLights();
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

        // Nothing ticks the blink while the ship is hidden, so park the lights solid lit
        // rather than leaving them frozen on whichever half of the cycle we happened to
        // be in. Guarded on progressFlashing, so the SetActive(false) in Start — which
        // runs before anything is lit — doesn't switch them on.
        StopProgressLightFlash(leaveLit: true);

        // An abduction cut short (drain, scene unload) would otherwise leave the object
        // holographic for the rest of the run — the ship is what gets disabled, not it.
        RestoreAbductedObjectMaterials();
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
        UpdateProgressLightFlash();

        switch (abductionState)
        {
            case AbductionState.GoingToAbduction:
                SteerToward(abductionPosition.position, abductionSpeed, true);

                if (HasArrived(abductionPosition.position, arriveRadius))
                {
                    abductedObjectSize = objectToAbduct.transform.localScale;
                    returningObjectPosition = objectToAbduct.transform.position;
                    // Goes holographic for the whole trip: the beam-up here, and the
                    // beam-down on the way out. In between its renderer is off anyway.
                    ApplyAbductionHologram();
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
                    // Fully back at its original spot and size — drop the hologram.
                    RestoreAbductedObjectMaterials();
                    abductionState = AbductionState.Leaving;
                }
                break;
            case AbductionState.Leaving:
                if (!activatedPowerSurge)
                {
                    Vector3 powerSurgeVFXPos = powerSurgeVFXPoint != null
                        ? powerSurgeVFXPoint.position
                        : objectToAbduct.transform.position;
                    powerSurgeManager.ActivatePowerSurge(powerSurgeVFXPos);
                    activatedPowerSurge = true;
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
        activatedPowerSurge = false;
        health = maxHealth;
        flashTimer = 0f;
        ClearFlash();
        RestoreAbductedObjectMaterials();

        // The target is down and the ship is out — every light is earned, so light them
        // all and start blinking. Doing it here rather than off the last OnHit also covers
        // trigger-style droppers, which drop on first contact and never fire OnHit at all.
        ArmProgressLightFlash();
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

    // ── Abducted-object hologram ──────────────────────────────

    /// <summary>
    /// Swaps every material slot on the abducted object for the hologram material, keeping
    /// the originals so <see cref="RestoreAbductedObjectMaterials"/> can put them back.
    /// Writes through <c>materials</c> rather than <c>sharedMaterials</c> so the swap never
    /// touches the shared assets — in the editor that would permanently convert the prefab.
    /// </summary>
    private void ApplyAbductionHologram()
    {
        if (hologramApplied || abductionHologramMaterial == null || objectToAbduct == null)
        {
            return;
        }

        // QuickOutline appends its mask/fill materials to every renderer while it is
        // enabled, and BoardComponent turns one on for every component. Those extra slots
        // must not be captured: the hologram would blanket them, and the restore would
        // write them back after Outline.OnDisable had already let go of them, leaving the
        // outline stuck on. Switching it off first makes it strip its own slots, so all we
        // ever see is the object's own materials.
        SuspendAbductedObjectOutlines();

        Renderer[] all = objectToAbduct.GetComponentsInChildren<Renderer>(true);
        List<Renderer> targets = new List<Renderer>(all.Length);

        for (int i = 0; i < all.Length; i++)
        {
            // Particle and trail renderers own their own look; re-materialising them would
            // replace the effect itself rather than skin the object.
            if (all[i] == null || all[i] is ParticleSystemRenderer || all[i] is TrailRenderer)
            {
                continue;
            }

            Material[] shared = all[i].sharedMaterials;
            if (shared == null || shared.Length == 0)
            {
                continue;
            }

            targets.Add(all[i]);
        }

        if (targets.Count == 0)
        {
            // Nothing to skin, so don't leave the outlines switched off behind us.
            ResumeAbductedObjectOutlines();
            return;
        }

        hologramRenderers = targets.ToArray();
        hologramOriginalMaterials = new Material[hologramRenderers.Length][];

        for (int i = 0; i < hologramRenderers.Length; i++)
        {
            Material[] shared = hologramRenderers[i].sharedMaterials;
            hologramOriginalMaterials[i] = shared;

            Material[] holo = new Material[shared.Length];
            for (int slot = 0; slot < holo.Length; slot++)
            {
                holo[slot] = abductionHologramMaterial;
            }

            hologramRenderers[i].materials = holo;
        }

        hologramApplied = true;
    }

    /// <summary>
    /// Puts the abducted object's own materials back. Safe to call when no hologram is
    /// applied, so the abort paths can call it unconditionally.
    /// </summary>
    private void RestoreAbductedObjectMaterials()
    {
        if (!hologramApplied)
        {
            return;
        }

        for (int i = 0; i < hologramRenderers.Length; i++)
        {
            if (hologramRenderers[i] == null)
            {
                continue;
            }

            hologramRenderers[i].sharedMaterials = hologramOriginalMaterials[i];
        }

        // Materials first, outlines second: re-enabling appends the mask/fill slots on top
        // of whatever the renderers hold, so the object's own materials have to be back.
        ResumeAbductedObjectOutlines();
        ForgetHologramCache();
    }

    /// <summary>
    /// Turns off every Outline on the abducted object, remembering which were on so
    /// <see cref="ResumeAbductedObjectOutlines"/> can put them back exactly as they were.
    /// </summary>
    private void SuspendAbductedObjectOutlines()
    {
        hologramOutlines = objectToAbduct.GetComponentsInChildren<Outline>(true);
        hologramOutlineWasEnabled = new bool[hologramOutlines.Length];

        for (int i = 0; i < hologramOutlines.Length; i++)
        {
            if (hologramOutlines[i] == null)
            {
                continue;
            }

            hologramOutlineWasEnabled[i] = hologramOutlines[i].enabled;
            hologramOutlines[i].enabled = false;
        }
    }

    private void ResumeAbductedObjectOutlines()
    {
        if (hologramOutlines == null)
        {
            return;
        }

        for (int i = 0; i < hologramOutlines.Length; i++)
        {
            if (hologramOutlines[i] != null && hologramOutlineWasEnabled[i])
            {
                hologramOutlines[i].enabled = true;
            }
        }

        hologramOutlines = null;
        hologramOutlineWasEnabled = null;
    }

    private void ForgetHologramCache()
    {
        hologramRenderers = null;
        hologramOriginalMaterials = null;
        hologramOutlines = null;
        hologramOutlineWasEnabled = null;
        hologramApplied = false;
    }

    // ── Drop-target progress lights ───────────────────────────

    /// <summary>
    /// Finds the dropper that triggers this abduction — through the OnDropAbduction
    /// satellite on the abducted object, the same wiring the abduction itself uses —
    /// and listens for its hits.
    /// </summary>
    private void SubscribeProgressLights()
    {
        if (progressDropper != null)
        {
            return;
        }

        OnDropAbduction wiring = objectToAbduct != null
            ? objectToAbduct.GetComponent<OnDropAbduction>()
            : null;

        progressDropper = wiring != null ? wiring.DropTarget : null;
        if (progressDropper == null)
        {
            return;
        }

        progressDropper.OnHit += OnDropTargetHit;
        // Clear on the rise, not on the drop: the lights should stay full for the whole
        // abduction and go dark as the target comes back up ready for the next one.
        progressDropper.onStartUp += ResetProgressLights;
    }

    private void UnsubscribeProgressLights()
    {
        if (progressDropper == null)
        {
            return;
        }

        progressDropper.OnHit -= OnDropTargetHit;
        progressDropper.onStartUp -= ResetProgressLights;
        progressDropper = null;
    }

    private void OnDropTargetHit(int hitCount)
    {
        // The target's collider stays live through its sink animation, so a ball can hit
        // it again on the way down and restart the count at 1. Latch at the full count
        // so that can't blink the lights back off mid-drop.
        if (progressLatched)
        {
            return;
        }

        if (progressDropper != null && hitCount >= progressDropper.HitsToDrop)
        {
            progressLatched = true;
        }

        SetLitLightCount(hitCount);
    }

    private void ResetProgressLights()
    {
        progressLatched = false;
        progressFlashing = false;
        SetLitLightCount(0);
    }

    /// <summary>
    /// Lights every cylinder and starts the blink. Called when the abduction begins, i.e.
    /// the moment the target is fully down and the ship comes out.
    /// </summary>
    private void ArmProgressLightFlash()
    {
        if (progressLights == null || progressLights.Length == 0)
        {
            return;
        }

        progressLatched = true;
        progressFlashing = true;
        // Starts on the lit half so the ship's arrival doesn't coincide with the lights
        // blinking off, which would read as them switching off rather than starting to flash.
        progressFlashOn = true;
        progressFlashTimer = 0f;
        SetLitLightCount(progressLights.Length);
    }

    private void StopProgressLightFlash(bool leaveLit)
    {
        if (!progressFlashing)
        {
            return;
        }

        progressFlashing = false;

        if (leaveLit && progressLights != null)
        {
            SetLitLightCount(progressLights.Length);
        }
    }

    /// <summary>
    /// Square-wave blink across the whole bank, driven from Update rather than a coroutine
    /// so it dies with the ship's own lifetime. Materials are only written on a phase flip.
    /// </summary>
    private void UpdateProgressLightFlash()
    {
        if (!progressFlashing || progressLights == null || progressLightFlashRate <= 0f)
        {
            return;
        }

        float halfPeriod = 0.5f / progressLightFlashRate;
        progressFlashTimer += Time.deltaTime;

        bool shouldBeOn = (int)(progressFlashTimer / halfPeriod) % 2 == 0;
        if (shouldBeOn == progressFlashOn)
        {
            return;
        }

        progressFlashOn = shouldBeOn;
        SetLitLightCount(progressFlashOn ? progressLights.Length : 0);
    }

    /// <summary>Lights the first <paramref name="litCount"/> cylinders and greys the rest.</summary>
    private void SetLitLightCount(int litCount)
    {
        if (progressLights == null)
        {
            return;
        }

        for (int i = 0; i < progressLights.Length; i++)
        {
            Renderer light = progressLights[i];
            if (light == null)
            {
                continue;
            }

            // sharedMaterial, not material: these are authored assets being pointed at,
            // so there is nothing to instance and nothing to leak.
            Material material = i < litCount ? progressLightOnMaterial : progressLightOffMaterial;
            if (material != null)
            {
                light.sharedMaterial = material;
            }
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
