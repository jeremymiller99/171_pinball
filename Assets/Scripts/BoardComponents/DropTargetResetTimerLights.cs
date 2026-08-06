using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Ring of <see cref="BoardLight"/> bulbs around a drop target that visualise the
/// reset countdown. When the <see cref="Dropper"/> goes fully down all lights
/// turn on, then snuff out one by one so they are all dark when the reset delay
/// elapses. Lights stay off until the target is hit again.
/// </summary>
[DefaultExecutionOrder(50)]
public class DropTargetResetTimerLights : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Dropper dropTarget;

    [Tooltip("Parent transform that holds the spline-instantiated bulbs. All BoardLights in its children are collected at Awake.")]
    [SerializeField] private Transform lightsContainer;

    [Tooltip("Explicit light list. If left empty and a container is assigned, children are collected automatically.")]
    [SerializeField] private List<BoardLight> lights = new List<BoardLight>();

    [Header("Timing")]
    [Tooltip("Seconds over which the lights go out. Should match the Dropper reset delay (default 15s).")]
    [SerializeField] private float duration = 15f;

    [Header("Power Surge")]
    [Tooltip("Scoring mode that drives Power Surge activation. When Power Surge is active, lit bulbs switch to their alternative color.")]
    [FormerlySerializedAs("frenzyManager")]
    [SerializeField] private PowerSurgeManager powerSurgeManager;

    [Tooltip("Index into each BoardLight's alternativeLitColors to use during Power Surge.")]
    [Min(0)]
    [FormerlySerializedAs("frenzyAlternativeIndex")]
    [SerializeField] private int powerSurgeAlternativeIndex;

    private GameRulesManager _gameRulesManager;
    private float _elapsed;
    private bool _running;
    private int _nextToExtinguish;

    private void Awake()
    {
        _gameRulesManager = ServiceLocator.Get<GameRulesManager>();
        powerSurgeManager = ServiceLocator.Get<PowerSurgeManager>();
        if (dropTarget == null)
        {
            dropTarget = GetComponentInParent<Dropper>();
        }

        CollectLightsFromContainer();
    }

    private void CollectLightsFromContainer()
    {
        if ((lights == null || lights.Count == 0) && lightsContainer != null)
        {
            if (lights == null)
            {
                lights = new List<BoardLight>();
            }
            lights.Clear();
            lightsContainer.GetComponentsInChildren<BoardLight>(true, lights);
        }
    }

    private void OnEnable()
    {
        if (dropTarget != null)
        {
            dropTarget.OnFullyDown += HandleFullyDown;
            dropTarget.OnReturnedUp += HandleReturnedUp;
        }

        if (powerSurgeManager != null)
        {
            powerSurgeManager.OnPowerSurgeActivated += HandlePowerSurgeActivated;
            powerSurgeManager.OnPowerSurgeDeactivated += HandlePowerSurgeDeactivated;
        }

        SetAllLights(false);
        ApplyPowerSurgeColorToAll(powerSurgeManager != null && powerSurgeManager.isPowerSurgeActive);
        _running = false;
        _elapsed = 0f;
        _nextToExtinguish = 0;
    }

    private void OnDisable()
    {
        if (dropTarget != null)
        {
            dropTarget.OnFullyDown -= HandleFullyDown;
            dropTarget.OnReturnedUp -= HandleReturnedUp;
        }

        if (powerSurgeManager != null)
        {
            powerSurgeManager.OnPowerSurgeActivated -= HandlePowerSurgeActivated;
            powerSurgeManager.OnPowerSurgeDeactivated -= HandlePowerSurgeDeactivated;
        }
    }

    private void HandlePowerSurgeActivated()
    {
        ApplyPowerSurgeColorToAll(true);
    }

    private void HandlePowerSurgeDeactivated()
    {
        ApplyPowerSurgeColorToAll(false);
    }

    private void ApplyPowerSurgeColorToAll(bool powerSurgeOn)
    {
        for (int i = 0; i < lights.Count; i++)
        {
            BoardLight bulb = lights[i];
            if (bulb == null)
            {
                continue;
            }

            if (powerSurgeOn)
            {
                bulb.SetLitAlternativeIndex(powerSurgeAlternativeIndex);
            }
            else
            {
                bulb.ClearLitAlternativeIndex();
            }
        }
    }

    private void HandleFullyDown()
    {
        if (lights.Count == 0 || duration <= 0f)
        {
            return;
        }

        SetAllLights(true);
        ApplyPowerSurgeColorToAll(powerSurgeManager != null && powerSurgeManager.isPowerSurgeActive);
        _elapsed = 0f;
        _nextToExtinguish = 0;
        _running = true;
    }

    private void HandleReturnedUp()
    {
        _running = false;
        SetAllLights(false);
        _elapsed = 0f;
        _nextToExtinguish = 0;
    }

    private void Update()
    {
        if (!_running)
        {
            return;
        }

        if (_gameRulesManager != null && _gameRulesManager.IsShopOpen)
        {
            return;
        }

        _elapsed += Time.deltaTime;

        int count = lights.Count;
        float progress = Mathf.Clamp01(_elapsed / duration);
        int shouldBeOff = Mathf.FloorToInt(progress * count);

        while (_nextToExtinguish < shouldBeOff && _nextToExtinguish < count)
        {
            BoardLight bulb = lights[_nextToExtinguish];
            if (bulb != null)
            {
                bulb.SetLit(false);
            }
            _nextToExtinguish++;
        }

        if (_elapsed >= duration)
        {
            SetAllLights(false);
            _running = false;
        }
    }

    private void SetAllLights(bool lit)
    {
        for (int i = 0; i < lights.Count; i++)
        {
            BoardLight bulb = lights[i];
            if (bulb != null)
            {
                bulb.SetLit(lit);
            }
        }
    }
}
