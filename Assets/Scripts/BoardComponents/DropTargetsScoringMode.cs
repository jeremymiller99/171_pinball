// Generated with Antigravity by jjmil on 2026-04-09.
// Drop‑target frenzy: bumper split‑open, portal reveal, multiplier doubling.
// Frenzy-gate SFX hook added by Claude Code (Opus 4.7) for jjmil on 2026-04-21.
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scoring mode tied to 3 drop targets. When all 3 are down, 4 bumpers
/// split open on the X‑axis to reveal a frenzy portal behind them.
/// Entering the portal doubles the current multiplier. Frenzy ends when
/// any drop target resets back up, at which point bumpers close and the
/// multiplier bonus is removed.
/// </summary>
public class DropTargetsScoringMode : MonoBehaviour
{
    private const string gameplayCoreSceneName = "GameplayCore";

    /// <summary>Fired when all 3 drop targets become down.</summary>
    public event Action OnAllTargetsDown;

    /// <summary>Fired when any target returns up.</summary>
    public event Action OnAnyTargetReturned;

    /// <summary>Fired when frenzy activates (ball entered portal).</summary>
    public event Action OnFrenzyActivated;

    /// <summary>Fired when frenzy deactivates (target returned up).</summary>
    public event Action OnFrenzyDeactivated;

    [Header("Drop Targets")]
    [SerializeField] private DropTarget[] dropTargets = new DropTarget[3];

    [Header("Board lights")]
    [Tooltip(
        "The three drop-target bulb BoardLights. Refreshes visuals " +
        "when all-down state changes.")]
    [SerializeField] private BoardLight[] dropTargetBulbLights =
        new BoardLight[3];

    [Header("Bonus (when all down)")]
    [Tooltip("Points awarded when all 3 drop targets are down.")]
    [SerializeField] private float allDownBonusPoints = 500f;
    [Tooltip(
        "Transform used for floating text spawn position " +
        "(e.g. center of targets).")]
    [SerializeField] private Transform bonusSpawnPosition;
    [Tooltip("Canvas offset for bonus popups.")]
    [SerializeField] private Vector2 popupOffset =
        new Vector2(0f, -100f);

    [Header("Frenzy Bumper Animation")]
    [Tooltip("Two bumpers on the left side that slide open.")]
    [SerializeField] private Transform[] leftBumpers =
        new Transform[2];
    [Tooltip("Two bumpers on the right side that slide open.")]
    [SerializeField] private Transform[] rightBumpers =
        new Transform[2];
    [Tooltip("X‑axis offset for left bumpers when open (negative).")]
    [SerializeField] private float leftOpenOffsetX = -1f;
    [Tooltip("X‑axis offset for right bumpers when open (positive).")]
    [SerializeField] private float rightOpenOffsetX = 1f;
    [Tooltip("Duration of the bumper open/close animation.")]
    [SerializeField] private float bumperAnimDuration = 0.5f;

    [Header("Frenzy Portals")]
    [Tooltip("The frenzy portal entrance. Hidden until bumpers open.")]
    [SerializeField] private GameObject frenzyPortalEntrance;
    [Tooltip("The frenzy portal exit. Hidden until bumpers open.")]
    [SerializeField] private GameObject frenzyPortalExit;

    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;
    [Tooltip(
        "Font for bonus popups. If null, uses spawner's default.")]
    [SerializeField] private TMP_FontAsset popupFontAsset;

    [Header("Frenzy HUD Color")]
    [Tooltip("Color applied to the multiplier HUD meter during frenzy. Should match your frenzy lights.")]
    [SerializeField] private Color frenzyHudColor = new Color(0f, 0.85f, 1f, 1f);

    public Color FrenzyHudColor => frenzyHudColor;

    private bool _allDownBonusAwardedThisCycle;
    private bool _wasAllDown;
    private Coroutine _deferredCheckRoutine;

    private bool _isFrenzyActive;
    private float _frenzyMultBonus;

    // Bumper animation state
    private Vector3[] _leftClosedPos;
    private Vector3[] _rightClosedPos;
    private Coroutine _bumperAnimRoutine;

    /// <summary>True when the frenzy multiplier doubling is active.
    /// </summary>
    public bool IsFrenzyActive => _isFrenzyActive;

    private void Awake()
    {
        EnsureRefs();
        CacheBumperClosedPositions();
        SetFrenzyPortalsActive(false);
    }

    private void OnEnable()
    {
        if (dropTargets == null) return;

        foreach (DropTarget dt in dropTargets)
        {
            if (dt != null)
            {
                dt.OnFullyDown += OnTargetFullyDown;
                dt.OnReturnedUp += OnTargetReturnedUp;
            }
        }

        _allDownBonusAwardedThisCycle = false;

        RefreshDropTargetBulbVisuals();
    }

    private void OnDisable()
    {
        if (_deferredCheckRoutine != null)
        {
            StopCoroutine(_deferredCheckRoutine);
            _deferredCheckRoutine = null;
        }

        if (_bumperAnimRoutine != null)
        {
            StopCoroutine(_bumperAnimRoutine);
            _bumperAnimRoutine = null;
        }

        if (dropTargets != null)
        {
            foreach (DropTarget dt in dropTargets)
            {
                if (dt != null)
                {
                    dt.OnFullyDown -= OnTargetFullyDown;
                    dt.OnReturnedUp -= OnTargetReturnedUp;
                }
            }
        }

        if (_isFrenzyActive)
        {
            DeactivateFrenzy();
        }
    }

    private void OnTargetFullyDown()
    {
        if (_deferredCheckRoutine != null)
        {
            StopCoroutine(_deferredCheckRoutine);
        }

        _deferredCheckRoutine =
            StartCoroutine(DeferredCheckAllDown());
    }

    private void OnTargetReturnedUp()
    {
        if (_deferredCheckRoutine != null)
        {
            StopCoroutine(_deferredCheckRoutine);
            _deferredCheckRoutine = null;
        }

        if (_wasAllDown)
        {
            _wasAllDown = false;
            AnimateBumpers(false);
            SetFrenzyPortalsActive(false);
            OnAnyTargetReturned?.Invoke();
        }

        if (_isFrenzyActive)
        {
            DeactivateFrenzy();
        }

        _allDownBonusAwardedThisCycle = false;
    }

    private IEnumerator DeferredCheckAllDown()
    {
        yield return null;

        _deferredCheckRoutine = null;

        bool allDown = AllTargetsDown();

        if (allDown)
        {
            if (!_wasAllDown)
            {
                _wasAllDown = true;
                AnimateBumpers(true);
                SetFrenzyPortalsActive(true);
                ServiceLocator.Get<AudioManager>()?.PlayFrenzyGate(
                    bonusSpawnPosition != null
                        ? bonusSpawnPosition.position
                        : transform.position);
                OnAllTargetsDown?.Invoke();
            }

            if (!_allDownBonusAwardedThisCycle)
            {
                AwardAllDownBonus();
            }
        }
        else
        {
            if (_wasAllDown)
            {
                _wasAllDown = false;
                AnimateBumpers(false);
                SetFrenzyPortalsActive(false);
                OnAnyTargetReturned?.Invoke();
            }

            if (_isFrenzyActive)
            {
                DeactivateFrenzy();
            }

            _allDownBonusAwardedThisCycle = false;
        }

        RefreshDropTargetBulbVisuals();
    }

    private void RefreshDropTargetBulbVisuals()
    {
        if (dropTargetBulbLights == null)
        {
            return;
        }

        for (int i = 0; i < dropTargetBulbLights.Length; i++)
        {
            if (dropTargetBulbLights[i] != null)
            {
                dropTargetBulbLights[i].ReapplyVisuals();
            }
        }
    }

    private bool AllTargetsDown()
    {
        if (dropTargets == null || dropTargets.Length == 0)
            return false;

        foreach (DropTarget dt in dropTargets)
        {
            if (dt == null || !dt.IsDown) return false;
        }

        return true;
    }

    /// <summary>True when all 3 drop targets are down.</summary>
    public bool AllTargetsDownNow => AllTargetsDown();

    // ── Frenzy activation / deactivation ──────────────────────

    /// <summary>
    /// Called by <see cref="FrenzyPortal"/> when the ball enters
    /// the frenzy portal. Doubles the current multiplier.
    /// </summary>
    public void ActivateFrenzy()
    {
        if (_isFrenzyActive) return;
        if (!AllTargetsDown()) return;

        EnsureRefs();
        if (scoreManager == null) return;

        _frenzyMultBonus = scoreManager.Mult;
        scoreManager.AddFrenzyMult(_frenzyMultBonus);
        _isFrenzyActive = true;
        SteamAchievements.UnlockFirstFrenzy();
        OnFrenzyActivated?.Invoke();

        if (floatingTextSpawner != null
            && bonusSpawnPosition != null)
        {
            floatingTextSpawner.SpawnText(
                bonusSpawnPosition.position,
                "FRENZY! x2 MULT!",
                popupFontAsset,
                0.9f,
                popupOffset);
        }
    }

    private void DeactivateFrenzy()
    {
        if (!_isFrenzyActive) return;

        EnsureRefs();

        if (scoreManager != null)
        {
            scoreManager.RemoveFrenzyMult();
        }

        _frenzyMultBonus = 0f;
        _isFrenzyActive = false;
        OnFrenzyDeactivated?.Invoke();
    }

    // ── Bonus award ───────────────────────────────────────────

    private void AwardAllDownBonus()
    {
        if (allDownBonusPoints <= 0f) return;

        EnsureRefs();
        if (scoreManager == null) return;

        Transform pos = bonusSpawnPosition != null
            ? bonusSpawnPosition
            : transform;

        scoreManager.AddScore(
            allDownBonusPoints,
            TypeOfScore.points,
            pos,
            popupOffset);

        _allDownBonusAwardedThisCycle = true;
    }

    // ── Bumper animation ──────────────────────────────────────

    private void CacheBumperClosedPositions()
    {
        _leftClosedPos = new Vector3[leftBumpers.Length];
        for (int i = 0; i < leftBumpers.Length; i++)
        {
            if (leftBumpers[i] != null)
                _leftClosedPos[i] = leftBumpers[i].localPosition;
        }

        _rightClosedPos = new Vector3[rightBumpers.Length];
        for (int i = 0; i < rightBumpers.Length; i++)
        {
            if (rightBumpers[i] != null)
                _rightClosedPos[i] =
                    rightBumpers[i].localPosition;
        }
    }

    private void AnimateBumpers(bool open)
    {
        if (_bumperAnimRoutine != null)
        {
            StopCoroutine(_bumperAnimRoutine);
        }

        _bumperAnimRoutine =
            StartCoroutine(BumperAnimRoutine(open));
    }

    private IEnumerator BumperAnimRoutine(bool open)
    {
        float duration = Mathf.Max(0.01f, bumperAnimDuration);
        float elapsed = 0f;

        // Capture current positions as animation start.
        Vector3[] leftStart = new Vector3[leftBumpers.Length];
        Vector3[] leftTarget = new Vector3[leftBumpers.Length];
        for (int i = 0; i < leftBumpers.Length; i++)
        {
            if (leftBumpers[i] == null) continue;
            leftStart[i] = leftBumpers[i].localPosition;
            leftTarget[i] = open
                ? _leftClosedPos[i]
                    + new Vector3(leftOpenOffsetX, 0f, 0f)
                : _leftClosedPos[i];
        }

        Vector3[] rightStart = new Vector3[rightBumpers.Length];
        Vector3[] rightTarget =
            new Vector3[rightBumpers.Length];
        for (int i = 0; i < rightBumpers.Length; i++)
        {
            if (rightBumpers[i] == null) continue;
            rightStart[i] = rightBumpers[i].localPosition;
            rightTarget[i] = open
                ? _rightClosedPos[i]
                    + new Vector3(rightOpenOffsetX, 0f, 0f)
                : _rightClosedPos[i];
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease-in-out for smooth animation.
            float eased = t * t * (3f - 2f * t);

            for (int i = 0; i < leftBumpers.Length; i++)
            {
                if (leftBumpers[i] != null)
                {
                    leftBumpers[i].localPosition =
                        Vector3.Lerp(
                            leftStart[i],
                            leftTarget[i],
                            eased);
                }
            }

            for (int i = 0; i < rightBumpers.Length; i++)
            {
                if (rightBumpers[i] != null)
                {
                    rightBumpers[i].localPosition =
                        Vector3.Lerp(
                            rightStart[i],
                            rightTarget[i],
                            eased);
                }
            }

            yield return null;
        }

        // Snap to final positions.
        for (int i = 0; i < leftBumpers.Length; i++)
        {
            if (leftBumpers[i] != null)
                leftBumpers[i].localPosition = leftTarget[i];
        }

        for (int i = 0; i < rightBumpers.Length; i++)
        {
            if (rightBumpers[i] != null)
                rightBumpers[i].localPosition = rightTarget[i];
        }

        _bumperAnimRoutine = null;
    }

    // ── Portal visibility ─────────────────────────────────────

    private void SetFrenzyPortalsActive(bool active)
    {
        if (frenzyPortalEntrance != null)
            frenzyPortalEntrance.SetActive(active);

        if (frenzyPortalExit != null)
            frenzyPortalExit.SetActive(active);
    }

    // ── Reference resolution ──────────────────────────────────

    private void EnsureRefs()
    {
        if (scoreManager != null
            && floatingTextSpawner != null) return;

        if (scoreManager == null)
        {
            scoreManager = FindScoreManagerInGameplayCore();
        }

        if (floatingTextSpawner == null)
        {
            floatingTextSpawner =
                ServiceLocator.Get<FloatingTextSpawner>();
        }
    }

    private static ScoreManager FindScoreManagerInGameplayCore()
    {
        ScoreManager[] all = FindObjectsByType<ScoreManager>(
            FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            ScoreManager sm = all[i];
            if (sm == null) continue;
            if (!sm.gameObject.scene.IsValid()) continue;
            if (!string.Equals(
                    sm.gameObject.scene.name,
                    gameplayCoreSceneName,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            return sm;
        }

        Scene scene = SceneManager.GetSceneByName(
            gameplayCoreSceneName);

        if (scene.IsValid() && scene.isLoaded)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var sm = roots[i]
                    .GetComponentInChildren<ScoreManager>(
                        includeInactive: true);
                if (sm != null) return sm;
            }
        }

        return ServiceLocator.Get<ScoreManager>();
    }
}
