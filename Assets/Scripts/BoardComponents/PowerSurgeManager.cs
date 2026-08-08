// Updated by Claude (Opus 5), for jjmil, on 2026-08-06 (surges now carry the source that
// granted them, so a listener can tell its own trigger from someone else's).
using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// What granted a Power Surge. Listeners that react to their own trigger only — the drop
/// target bank holding its gate open for the surge it started — filter on this so a surge
/// from elsewhere on the board passes them by.
/// </summary>
public enum PowerSurgeSource
{
    /// <summary>No surge has run yet. Never passed to ActivatePowerSurge.</summary>
    None = 0,
    /// <summary>The portal revealed behind the drop-target gate.</summary>
    DropTargetPortal,
    /// <summary>The alien ship, on the way out of an abduction.</summary>
    Abductor,
    /// <summary>The ball-duplicating bumper.</summary>
    ModeDuplicator
}

public class PowerSurgeManager : MonoBehaviour
{
    [FormerlySerializedAs("defaultTimeForFrenzy")]
    [SerializeField] private float defaultTimeForPowerSurge = 15f;
    [FormerlySerializedAs("currentFrenzyTime")]
    [SerializeField] private float currentPowerSurgeTime = 0f;
    [FormerlySerializedAs("frenzyLastsUntil")]
    [SerializeField] private float powerSurgeLastsUntil = 0f;
    [SerializeField] private ScoreManager scoreManager;

    [Header("Coin Reward")]
    [Tooltip("Smallest payout when a Power Surge starts. Inclusive.")]
    [SerializeField] private int coinRewardMin = 1;
    [Tooltip("Largest payout when a Power Surge starts. Inclusive.")]
    [SerializeField] private int coinRewardMax = 3;

    public event Action OnPowerSurgeActivated;
    public event Action OnPowerSurgeDeactivated;

    /// <summary>
    /// Fired every time the countdown is (re)filled — both a fresh activation and a
    /// re-entry that extends a running surge — carrying the source of that particular
    /// fill. Listeners that must stay in step with the remaining surge time (e.g. the drop
    /// targets holding the gate open) use this instead of
    /// <see cref="OnPowerSurgeActivated"/>, which only marks the state change.
    /// </summary>
    public event Action<PowerSurgeSource> OnPowerSurgeTimerRefreshed;
    [FormerlySerializedAs("isFrenzyActive")]
    public bool isPowerSurgeActive;

    /// <summary>
    /// What granted the running surge — or, once it has ended, the surge that just ended,
    /// so deactivation listeners can still tell whether it was theirs.
    /// </summary>
    public PowerSurgeSource ActiveSource { get; private set; }

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
    public void ActivatePowerSurge(
        PowerSurgeSource source, Vector3 position, float time = 0, int mult = -1)
    {
        // Paid before the early-return so every trigger earns, including re-entries that
        // only extend a running surge. The SFX/VFX below stay on the state transition.
        AwardCoins(position);

        if (isPowerSurgeActive) {
            powerSurgeLastsUntil += time > 0 ? time : defaultTimeForPowerSurge;
            // A gate portal entry takes ownership of a surge someone else started: the
            // player earned that gate cycle, so the drop targets should govern how the
            // surge runs and ends. Extensions from elsewhere leave ownership alone.
            if (source == PowerSurgeSource.DropTargetPortal)
                ActiveSource = source;
            OnPowerSurgeTimerRefreshed?.Invoke(source);
            return;
        }

        ActiveSource = source;
        isPowerSurgeActive = true;
        OnPowerSurgeActivated?.Invoke();
        SteamAchievements.UnlockFirstPowerSurge();
        powerSurgeLastsUntil = time > 0 ? time : defaultTimeForPowerSurge;
        OnPowerSurgeTimerRefreshed?.Invoke(source);
        scoreManager.AddPowerSurgeMult(mult != -1 ? mult : scoreManager.Mult);
        // Board VFX is owned by LevelUpVFXTrigger (board scene), so the
        // Power Surge particle is configured alongside the level-up effects.
        ServiceLocator.Get<LevelUpVFXTrigger>()?.SpawnPowerSurgeVFX(position);
        ServiceLocator.Get<AudioManager>()?.PlayPowerSurgeActivated(position);
    }

    // Pays out a small random purse on every Power Surge trigger — one per portal entry,
    // not one per surge, so re-entering while a surge is already running pays again.
    private void AwardCoins(Vector3 position)
    {
        int min = Mathf.Max(0, Mathf.Min(coinRewardMin, coinRewardMax));
        int max = Mathf.Max(coinRewardMin, coinRewardMax);
        if (max <= 0) return;

        // Random.Range's int overload has an exclusive upper bound, so max is inclusive.
        // System is imported here too, hence the explicit UnityEngine qualifier.
        int reward = UnityEngine.Random.Range(min, max + 1);
        if (reward <= 0) return;

        var coinController = ServiceLocator.Get<CoinController>();
        if (coinController == null) return;

        // Scaled, so round modifiers that boost coin gain apply — matching CoinAdder.
        int applied = coinController.AddCoinsScaledDeferredUi(reward);
        if (applied <= 0) return;

        // The balance moved but the HUD hasn't; the floating text drives that on arrival.
        // Without a spawner nothing would ever land, so sync it straight away instead.
        var floatingTextSpawner = ServiceLocator.Get<FloatingTextSpawner>();
        if (floatingTextSpawner == null)
        {
            coinController.ApplyDeferredCoinsUi(applied);
            return;
        }

        floatingTextSpawner.SpawnGoldText(
            position,
            "+$" + applied,
            applied,
            () => coinController.ApplyDeferredCoinsUi(applied));
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
        // ActiveSource deliberately keeps the ending surge's source through the event, so
        // handlers can tell whether the surge that just ended was theirs.
        OnPowerSurgeDeactivated?.Invoke();
        scoreManager.RemovePowerSurgeMult();
    }
}