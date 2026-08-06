using System;
using UnityEngine;
using UnityEngine.Serialization;

public class PowerSurgeManager : MonoBehaviour
{
    [FormerlySerializedAs("defaultTimeForFrenzy")]
    [SerializeField] private float defaultTimeForPowerSurge = 15f;
    [FormerlySerializedAs("currentFrenzyTime")]
    [SerializeField] private float currentPowerSurgeTime = 0f;
    [FormerlySerializedAs("frenzyLastsUntil")]
    [SerializeField] private float powerSurgeLastsUntil = 0f;
    [SerializeField] private ScoreManager scoreManager;

    public event Action OnPowerSurgeActivated;
    public event Action OnPowerSurgeDeactivated;
    [FormerlySerializedAs("isFrenzyActive")]
    public bool isPowerSurgeActive;

    // Reference-counted timer pause. While > 0, the Power Surge countdown is frozen
    // (e.g. a ball is held inside the portal during its teleport delay) so the
    // mode can't expire and yank the exit portal out from under that ball.
    private int _timerPauseCount = 0;
    public bool IsTimerPaused => _timerPauseCount > 0;

    private void Awake()
    {
        ServiceLocator.Register(this);
        scoreManager = ServiceLocator.Get<ScoreManager>();
    }

    // Activates Power Surge mode, defaults to doubling current mult.
    // position is the world point of activation (e.g. the portal or
    // abducted object); the particle is raised on Z so it sits above it.
    public void ActivatePowerSurge(Vector3 position, float time = 0, int mult = -1)
    {
        if (isPowerSurgeActive) {
            powerSurgeLastsUntil += time > 0 ? time : defaultTimeForPowerSurge;
            return;
        }

        isPowerSurgeActive = true;
        OnPowerSurgeActivated?.Invoke();
        SteamAchievements.UnlockFirstPowerSurge();
        powerSurgeLastsUntil = time > 0 ? time : defaultTimeForPowerSurge;
        scoreManager.AddPowerSurgeMult(mult != -1 ? mult : scoreManager.Mult);
        // Board VFX is owned by LevelUpVFXTrigger (board scene), so the
        // Power Surge particle is configured alongside the level-up effects.
        ServiceLocator.Get<LevelUpVFXTrigger>()?.SpawnPowerSurgeVFX(position);
        ServiceLocator.Get<AudioManager>()?.PlayPowerSurgeActivated(position);
    }

    // Freeze/unfreeze the Power Surge countdown. Calls must be balanced; use the
    // matching ResumeTimer for every PauseTimer.
    public void PauseTimer()
    {
        _timerPauseCount++;
    }

    public void ResumeTimer()
    {
        if (_timerPauseCount > 0)
        {
            _timerPauseCount--;
        }
    }

    private void Update()
    {
        if (!isPowerSurgeActive) return;

        // Hold the countdown while paused (ball in a portal delay, etc.).
        if (_timerPauseCount > 0) return;

        currentPowerSurgeTime += Time.deltaTime;

        if (currentPowerSurgeTime >= powerSurgeLastsUntil)
        {
            currentPowerSurgeTime = 0f;
            powerSurgeLastsUntil = 0f;
            DeactivatePowerSurge();
        }
    }

    public void DeactivatePowerSurge()
    {
        if (!isPowerSurgeActive) return;

        isPowerSurgeActive = false;
        // Reset the countdown so a later ActivatePowerSurge starts from a clean
        // slate. The timer-expiry path above already zeroes these, but external
        // callers (e.g. the drop-target gate closing) don't go through it.
        currentPowerSurgeTime = 0f;
        powerSurgeLastsUntil = 0f;
        OnPowerSurgeDeactivated?.Invoke();
        scoreManager.RemovePowerSurgeMult();
    }
}