// Generated with Claude Code by assistant, for jjmil, on 2026-06-04.
// Flipper anti-spam: heat builds per flip, drains over time. At max -> overheated:
// flipper input is disabled and the 5 side lights flash for a cooldown, then reset.
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Drives a row of <see cref="BoardLight"/> components as an overheat gauge for one flipper side.
/// Each flip adds <see cref="heatPerFlip"/>; heat drains at <see cref="coolingPerSecond"/> while idle.
/// As heat rises the lights fill up (0..N). When heat reaches <see cref="overheatThreshold"/> the
/// flipper on this side is disabled, all lights flash for <see cref="overheatCooldownSeconds"/>, then
/// heat resets and the lights go off. Put this on the side's lights parent (5 BoardLight children);
/// leave <see cref="lights"/> empty to auto-take one BoardLight per direct child. Any
/// <see cref="BoardLightWaveSequence"/> on the same object is disabled so it stops the old flashing.
///
/// Default tuning: ~20 flips in 5s overheats; a full bar drains in 5s when idle.
/// </summary>
[DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
public sealed class FlipperOverheat : MonoBehaviour
{
    public enum FlipperSide
    {
        Left,
        Right
    }

    [Header("Side")]
    [Tooltip("Which physical flipper this gauge tracks. Honors CrossEyedBall input swapping so heat " +
             "follows the flipper that actually moves.")]
    [SerializeField] private FlipperSide side = FlipperSide.Left;

    [Header("Lights")]
    [Tooltip("Gauge lights, low heat -> high heat order. If empty, uses first BoardLight under each " +
             "direct child in sibling order.")]
    [SerializeField] private BoardLight[] lights;

    [Tooltip("Full on/off cycle seconds for the overheated flashing.")]
    [SerializeField] [Min(0.01f)] private float overheatFlashCycleSeconds = 0.25f;

    [Header("Overheat tuning (default: ~20 flips in 5s)")]
    [Tooltip("Heat needed to overheat. With heatPerFlip = 2 this is ~10 flips of headroom plus drain.")]
    [SerializeField] [Min(0.01f)] private float overheatThreshold = 20f;

    [Tooltip("Heat added per flip (press edge).")]
    [SerializeField] [Min(0f)] private float heatPerFlip = 2f;

    [Tooltip("Heat removed per second while not overheated.")]
    [SerializeField] [Min(0f)] private float coolingPerSecond = 4f;

    [Tooltip("Forced cooldown after overheating. Lights flash this long, then reset to off.")]
    [SerializeField] [Min(0f)] private float overheatCooldownSeconds = 5f;

    // Queried by PinballFlipper to suppress input while a side is overheated.
    private static bool s_leftOverheated;
    private static bool s_rightOverheated;

    public static bool IsPhysicalSideOverheated(bool leftSide)
    {
        return leftSide ? s_leftOverheated : s_rightOverheated;
    }

    private BoardLight[] _resolvedLights;
    private float _heat;
    private bool _overheated;
    private float _cooldownRemaining;
    private float _flashBeatTimer;
    private int _lastLitCount = -1;
    private bool _prevPressed;

#if ENABLE_INPUT_SYSTEM
    private InputAction _leftFlipAction;
    private InputAction _rightFlipAction;
#endif

    private void Awake()
    {
        // Take over the lights from the old wave flasher, if present.
        BoardLightWaveSequence wave = GetComponent<BoardLightWaveSequence>();
        if (wave != null)
        {
            wave.enabled = false;
        }

        ResolveLights();

#if ENABLE_INPUT_SYSTEM
        InputActionAsset actions = InputSystem.actions;
        if (actions != null)
        {
            _leftFlipAction = actions.FindAction("LeftFlip");
            _rightFlipAction = actions.FindAction("RightFlip");
        }
#endif
    }

    private void OnEnable()
    {
        ResolveLights();
        _heat = 0f;
        _overheated = false;
        _cooldownRemaining = 0f;
        _lastLitCount = -1;
        _prevPressed = ReadPressed();
        SetSideOverheatedFlag(false);
        ApplyHeatLights(0);
    }

    private void OnDisable()
    {
        // Never leave a flipper stuck disabled because this object went away.
        SetSideOverheatedFlag(false);
    }

    private void Update()
    {
        if (_overheated)
        {
            TickCooldown();
            return;
        }

        bool pressed = ReadPressed();
        if (pressed && !_prevPressed)
        {
            _heat += heatPerFlip;
        }

        _prevPressed = pressed;

        if (_heat > 0f)
        {
            _heat = Mathf.Max(0f, _heat - coolingPerSecond * Time.deltaTime);
        }

        if (_heat >= overheatThreshold)
        {
            EnterOverheat();
        }
        else
        {
            UpdateHeatLights();
        }
    }

    private void TickCooldown()
    {
        _cooldownRemaining -= Time.deltaTime;

        // Play a coin "tick" on each flash cycle while overheated.
        _flashBeatTimer -= Time.deltaTime;
        if (_flashBeatTimer <= 0f)
        {
            ServiceLocator.Get<AudioManager>()?.PlayCoinAdd();
            _flashBeatTimer += overheatFlashCycleSeconds;
        }

        if (_cooldownRemaining <= 0f)
        {
            ExitOverheat();
        }
    }

    private void EnterOverheat()
    {
        _overheated = true;
        _heat = overheatThreshold;
        _cooldownRemaining = overheatCooldownSeconds;
        // Lights flash off-then-on; first "on" lands half a cycle in, so beat the coin to that.
        _flashBeatTimer = overheatFlashCycleSeconds * 0.5f;
        SetSideOverheatedFlag(true);

        ServiceLocator.Get<AudioManager>()?.PlayExplosion(transform.position);

        if (_resolvedLights != null)
        {
            for (int i = 0; i < _resolvedLights.Length; i++)
            {
                BoardLight light = _resolvedLights[i];
                if (light == null)
                {
                    continue;
                }

                light.SetOn();
                light.StartFlashLitVersusOff(overheatFlashCycleSeconds);
            }
        }

        _lastLitCount = -1;
    }

    private void ExitOverheat()
    {
        _overheated = false;
        _heat = 0f;
        _cooldownRemaining = 0f;
        SetSideOverheatedFlag(false);

        if (_resolvedLights != null)
        {
            for (int i = 0; i < _resolvedLights.Length; i++)
            {
                if (_resolvedLights[i] != null)
                {
                    _resolvedLights[i].SetOff();
                }
            }
        }

        _lastLitCount = 0;
        // Don't count a held button as a fresh flip the instant we recover.
        _prevPressed = ReadPressed();
    }

    private void UpdateHeatLights()
    {
        int n = _resolvedLights != null ? _resolvedLights.Length : 0;
        if (n == 0)
        {
            return;
        }

        float fraction = overheatThreshold > 0f ? _heat / overheatThreshold : 0f;
        int litCount = Mathf.Clamp(Mathf.CeilToInt(fraction * n - 0.0001f), 0, n);
        if (litCount != _lastLitCount)
        {
            ApplyHeatLights(litCount);
            // Coin tick whenever a gauge bulb turns on or off.
            ServiceLocator.Get<AudioManager>()?.PlayCoinAdd();
        }
    }

    private void ApplyHeatLights(int litCount)
    {
        if (_resolvedLights == null)
        {
            return;
        }

        for (int i = 0; i < _resolvedLights.Length; i++)
        {
            if (_resolvedLights[i] != null)
            {
                _resolvedLights[i].SetLit(i < litCount);
            }
        }

        _lastLitCount = litCount;
    }

    private bool ReadPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (_leftFlipAction == null && _rightFlipAction == null)
        {
            return false;
        }

        bool physicalLeft = side == FlipperSide.Left;
        if (CrossEyedBall.IsFlipInputSwappedForGameplay())
        {
            physicalLeft = !physicalLeft;
        }

        InputAction act = physicalLeft ? _leftFlipAction : _rightFlipAction;
        return act != null && act.IsPressed();
#else
        return false;
#endif
    }

    private void SetSideOverheatedFlag(bool value)
    {
        if (side == FlipperSide.Left)
        {
            s_leftOverheated = value;
        }
        else
        {
            s_rightOverheated = value;
        }
    }

    private void ResolveLights()
    {
        if (lights != null && lights.Length > 0)
        {
            var list = new List<BoardLight>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    list.Add(lights[i]);
                }
            }

            _resolvedLights = list.ToArray();
            return;
        }

        var fromChildren = new List<BoardLight>();
        for (int c = 0; c < transform.childCount; c++)
        {
            BoardLight bl = transform.GetChild(c).GetComponentInChildren<BoardLight>(true);
            if (bl != null)
            {
                fromChildren.Add(bl);
            }
        }

        _resolvedLights = fromChildren.ToArray();
    }

#if UNITY_EDITOR
    [ContextMenu("FlipperOverheat/Force Overheat")]
    private void ContextForceOverheat()
    {
        if (Application.isPlaying && !_overheated)
        {
            _heat = overheatThreshold;
            EnterOverheat();
        }
    }
#endif
}
