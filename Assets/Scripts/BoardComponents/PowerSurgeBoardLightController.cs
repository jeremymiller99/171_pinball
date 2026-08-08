// Generated with Antigravity by jjmil on 2026-04-09.
using UnityEngine;

/// <summary>
/// Listens for Power Surge activation / deactivation on a
/// <see cref="DropTargetsScoringMode"/> and switches every
/// <see cref="BoardLight"/> tagged <c>"Default Board Light"</c>
/// to its first alternative lit color (index 0) during Power Surge,
/// then clears the alternative on deactivation.
/// </summary>
public class PowerSurgeBoardLightController : MonoBehaviour
{
    private const string boardLightTag = "Default Board Light";
    private const int powerSurgeAlternativeIndex = 0;

    [Header("References")]
    [Tooltip("The DropTargetsScoringMode that fires Power Surge events.")]
    [SerializeField]
    private PowerSurgeManager powerSurgeManager;

    private BoardLight[] _cachedLights;

    private void Awake()
    {
        powerSurgeManager = ServiceLocator.Get<PowerSurgeManager>();
    }

    private void OnEnable()
    {
        if (powerSurgeManager != null)
        {
            powerSurgeManager.OnPowerSurgeActivated +=
                HandlePowerSurgeActivated;
            powerSurgeManager.OnPowerSurgeDeactivated +=
                HandlePowerSurgeDeactivated;
        }
    }

    private void OnDisable()
    {
        if (powerSurgeManager != null)
        {
            powerSurgeManager.OnPowerSurgeActivated -=
                HandlePowerSurgeActivated;
            powerSurgeManager.OnPowerSurgeDeactivated -=
                HandlePowerSurgeDeactivated;
        }
    }

    private void HandlePowerSurgeActivated()
    {
        // Devil round owns the lights (steady red) and must win over Power Surge.
        if (DevilRoundLights.Locked) return;

        RefreshCache();

        foreach (BoardLight light in _cachedLights)
        {
            if (light == null) continue;
            light.SetLitAlternativeIndex(powerSurgeAlternativeIndex);
            light.ReapplyVisuals();
        }
    }

    private void HandlePowerSurgeDeactivated()
    {
        // Devil round owns the lights; don't clear its red when Power Surge ends.
        if (DevilRoundLights.Locked) return;

        if (_cachedLights == null) return;

        foreach (BoardLight light in _cachedLights)
        {
            if (light == null) continue;
            light.ClearLitAlternativeIndex();
            light.ReapplyVisuals();
        }
    }

    private void RefreshCache()
    {
        GameObject[] tagged =
            GameObject.FindGameObjectsWithTag(boardLightTag);

        _cachedLights = new BoardLight[tagged.Length];

        for (int i = 0; i < tagged.Length; i++)
        {
            _cachedLights[i] =
                tagged[i].GetComponent<BoardLight>();
        }
    }
}
