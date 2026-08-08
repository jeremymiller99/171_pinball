// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-07 (FTUE director shell).
// Updated by Claude Code (claude-opus-5) for jjmil on 2026-08-08 (dialogue pipeline, beats 1/1a).
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runs the tutorial. One of these lives in the Board_FTUE scene and nowhere else — its presence
/// is what raises <see cref="FtueState.Active"/>, and its destruction when the board unloads is
/// what lowers it again.
///
/// The order of beats is written in code rather than authored as data. The beats are not
/// interchangeable: one waits on a dismissal, one on the ball being launched, one on a trigger
/// volume, one on three power surges. A generic step system would need a trigger enum, an action
/// enum and a switch here, so every real beat would cost a recompile anyway while being harder to
/// read. What changes often is the wording, and that lives in <see cref="FtueDialogueLine"/>
/// fields you can edit in the inspector.
///
/// Beats 1 and 1a are wired here. The rest follow in the next ticket.
/// </summary>
[DisallowMultipleComponent]
public sealed class FtueDirector : MonoBehaviour
{
    [Header("Dialogue prefabs")]
    [Tooltip("Ordinary line. Root must carry FtueDialogueView; leave its Name Input empty.")]
    [SerializeField] private FtueDialogueView dialoguePanelPrefab;

    [Tooltip("Naming panel — same component, plus a wired TMP Input Field.")]
    [SerializeField] private FtueDialogueView namingPanelPrefab;

    [Header("Speaker")]
    [Tooltip("Shown in place of a name until the player has chosen one, so the naming beat is "
        + "not spoiled by the AI already appearing to have a name.")]
    [SerializeField] private string unnamedSpeaker = "???";

    [Tooltip("Hint shown once a line has finished typing, e.g. 'press any key'.")]
    [SerializeField] private string continuePrompt = "";

    [Header("Beat 1 — welcome")]
    [SerializeField] private FtueDialogueLine introLine;

    [Header("Beat 1a — naming")]
    [SerializeField] private FtueDialogueLine namingLine;

    [Tooltip("Greyed-out text inside the name field.")]
    [SerializeField] private string namingPlaceholder = "";

    [Tooltip("Prompt on the naming panel. It confirms on Enter, not on any key.")]
    [SerializeField] private string namingConfirmPrompt = "";

    [Tooltip("Played straight after the player names it, so the AI can react in character. "
        + "{0} is the chosen name.")]
    [SerializeField] private FtueDialogueLine nameAcceptedLine;

    [Header("Beats 2-3 — launcher focus")]
    [Tooltip("Moves the camera and returns it. Put it on this same object.")]
    [SerializeField] private FtueCameraFocus cameraFocus;

    [Tooltip("Empty in this scene marking where the camera should sit while the player is being "
        + "shown the launcher. Position it where you want the CAMERA, not the rig.")]
    [SerializeField] private Transform launcherFocusPoint;

    [Tooltip("Stays on screen until the ball reaches the playfield. {1} is the launch key.")]
    [SerializeField] private FtueDialogueLine launchPromptLine;

    [Tooltip("The Launch action, read at runtime so the prompt names whatever it is bound to.")]
    [SerializeField] private InputActionReference launchAction;

    [Tooltip("Trigger volume just past the portal exit, where the ball arrives in the playfield. "
        + "The launch prompt stays up until the ball gets there, so an accidental tap cannot "
        + "close it before it has been read. Left empty, the prompt closes on launch instead.")]
    [SerializeField] private FtueBallTrigger playfieldEntryTrigger;

    [Tooltip("Board input is held for this long when the launch prompt appears, so the keypress "
        + "that dismissed the previous line cannot roll straight into a launch. Unscaled "
        + "seconds; 0 disables it.")]
    [SerializeField] private float launchInputLockoutSeconds = 0.4f;

    [Header("Beat 4 — save the ball")]
    [Tooltip("Trigger volume below the bumpers and above the flippers. Needs FtueBallTrigger.")]
    [SerializeField] private FtueBallTrigger saveTheBallTrigger;

    [Tooltip("Pauses the game when the ball falls past the trigger. {1} and {2} are the left and "
        + "right flipper keys.")]
    [SerializeField] private FtueDialogueLine flipperLessonLine;

    [SerializeField] private InputActionReference leftFlipperAction;
    [SerializeField] private InputActionReference rightFlipperAction;

    [Header("Beat 4a — the ball comes back")]
    [Tooltip("Played when a drained ball is handed back, so the player understands it was meant "
        + "to happen. Leave empty to say nothing and just re-show the launch prompt.")]
    [SerializeField] private FtueDialogueLine ballReturnedLine;

    [Header("Level goals")]
    [Tooltip("Score needed for each level-up, replacing the normal curve for this board only. "
        + "The shipped curve starts at 1000, which is far too steep for a first-time player. "
        + "Indices past the end clamp to the last value.")]
    [SerializeField]
    private List<float> levelGoals = new List<float> { 100f, 250f, 600f, 1000f };

    [Header("Shop visits")]
    [Tooltip("What the shelf may offer on each visit, in order. The last entry is reused for any "
        + "visit beyond the list, so an empty last entry means 'nothing new from here on'. "
        + "Items listed here bypass the progression unlock check.")]
    [SerializeField] private List<FtueShopVisit> shopVisits = new List<FtueShopVisit>();

    [Header("Safety")]
    [Tooltip("Seconds to wait for the opening camera pan and ship flight before starting the "
        + "tutorial anyway. A tutorial that never starts is worse than one that starts early.")]
    [SerializeField] private float maxIntroWaitSeconds = 15f;

    [Header("Debug")]
    [Tooltip("Logs beat transitions. Useful while wiring the board; harmless to leave on.")]
    [SerializeField] private bool logStateChanges = true;

    private enum Beat
    {
        NotStarted = 0,
        Opening = 1,
        AwaitingLaunch = 2,
        BallInPlay = 3,
        FlipperLesson = 4,

        /// <summary>Phase 2 taught. Free play until the shop beats take over.</summary>
        Free = 5
    }

    private GameRulesManager cachedRules;
    private DrainHandler cachedDrain;
    private FtueDialogueView activePanel;
    private bool activePanelBlocksInput;
    private bool sequenceStarted;
    private bool flipperLessonGiven;
    private int shopVisitIndex;
    private Beat beat = Beat.NotStarted;

    private float timeScaleBeforePause = 1f;
    private bool pausedByDirector;

    private bool launchLockoutActive;
    private Coroutine launchLockoutRoutine;

    // OnEnable/OnDisable rather than Awake/OnDestroy: toggling the component off is a reasonable
    // way to disable the tutorial while working in the scene, and it should lower the flag too.
    private void OnEnable()
    {
        FtueState.Activate(this);
        FtueState.SetLevelGoals(levelGoals);
        PinballLauncher.BallLaunched += OnBallLaunched;

        if (saveTheBallTrigger != null) saveTheBallTrigger.BallEntered += OnSaveTheBallTrigger;
        if (playfieldEntryTrigger != null) playfieldEntryTrigger.BallEntered += OnPlayfieldEntered;

        Log($"Tutorial active on '{gameObject.scene.name}'.");
    }

    private void OnDisable()
    {
        // Static event: leaving this subscribed would keep a destroyed director alive and firing
        // on every launch for the rest of the session.
        PinballLauncher.BallLaunched -= OnBallLaunched;

        if (saveTheBallTrigger != null) saveTheBallTrigger.BallEntered -= OnSaveTheBallTrigger;
        if (playfieldEntryTrigger != null) playfieldEntryTrigger.BallEntered -= OnPlayfieldEntered;

        launchLockoutActive = false;
        launchLockoutRoutine = null;

        UnsubscribeFromRules();
        UnsubscribeFromDrain();
        DismissActivePanel();
        RefreshInputBlock();

        // Ahead of everything else that could go wrong: a board unloaded while a beat had the game
        // paused would leave timeScale at 0 and freeze the rest of the session.
        SetPaused(false);

        FtueState.Deactivate(this);
        Log("Tutorial released.");
    }

    // Polled rather than resolved once: GameRulesManager lives in GameplayCore and the board is
    // loaded additively, so it is not reliably resolvable on our first frame. Mirrors how
    // BasicTutorialController finds the same service.
    private void Update()
    {
        TrySubscribeToRules();
        TrySubscribeToDrain();
    }

    private void TrySubscribeToDrain()
    {
        DrainHandler drain = ServiceLocator.Get<DrainHandler>();
        if (drain == cachedDrain) return;

        UnsubscribeFromDrain();

        cachedDrain = drain;
        if (drain == null) return;

        drain.DrainBankCompleted += OnDrainBankCompleted;
    }

    private void UnsubscribeFromDrain()
    {
        if (cachedDrain == null) return;

        cachedDrain.DrainBankCompleted -= OnDrainBankCompleted;
        cachedDrain = null;
    }

    private void TrySubscribeToRules()
    {
        GameRulesManager rules = ServiceLocator.Get<GameRulesManager>();
        if (rules == cachedRules) return;

        UnsubscribeFromRules();

        cachedRules = rules;
        if (rules == null) return;

        rules.RoundStarted += OnRoundStarted;
        rules.ShopOpened += OnShopOpened;
        rules.ShopClosed += OnShopClosed;
    }

    private void UnsubscribeFromRules()
    {
        if (cachedRules == null) return;

        cachedRules.RoundStarted -= OnRoundStarted;
        cachedRules.ShopOpened -= OnShopOpened;
        cachedRules.ShopClosed -= OnShopClosed;
        cachedRules = null;
    }

    /// <summary>
    /// Drops the visit's pool restriction. Left in place it would also narrow mystery-ball
    /// resolution during play, which runs through the same filter.
    /// </summary>
    private void OnShopClosed()
    {
        FtueState.ClearShopOverride();
    }

    /// <summary>
    /// Applies the authored shelf for this visit. Runs before the shop canvas activates —
    /// GameRulesManager.OpenShop raises ShopOpened first, and the shelf is only built in
    /// UnifiedShopController.OnEnable — so the override is in force by the time offers generate.
    /// </summary>
    private void ApplyShopVisitPool()
    {
        if (shopVisits == null || shopVisits.Count == 0)
        {
            FtueState.ClearShopOverride();
            return;
        }

        // The last entry covers every visit past the authored ones, so a tutorial that runs long
        // keeps offering whatever was intended for the end rather than reverting to the full pool.
        int index = Mathf.Clamp(shopVisitIndex, 0, shopVisits.Count - 1);
        FtueShopVisit visit = shopVisits[index];

        FtueState.SetShopOverride(visit.Balls, visit.Components);
        shopVisitIndex++;

        Log($"Shop visit {index + 1}: {visit.Components.Count} component(s), "
            + $"{visit.Balls.Count} ball(s).");
    }

    /// <summary>
    /// Hands the camera back before the shop takes it. ShopTransitionController re-reads the rig's
    /// current position as its "home" when it opens, so leaving the camera at a focus point here
    /// would teach it the wrong home and strand the view there for the rest of the run.
    ///
    /// GameRulesManager.OpenShop raises this event before it calls into the transition controller,
    /// which is what makes snapping here land in time.
    /// </summary>
    private void OnShopOpened()
    {
        if (cameraFocus != null) cameraFocus.SnapToPlayPose();

        ApplyShopVisitPool();
    }

    /// <summary>
    /// Beat 3. The ball is away: drop the prompt and hand the camera back. Also covers the player
    /// who launches before the prompt has finished typing.
    /// </summary>
    private void OnBallLaunched(GameObject launched)
    {
        if (cameraFocus != null) cameraFocus.ReturnToPlayPose();

        if (beat != Beat.AwaitingLaunch) return;

        beat = Beat.BallInPlay;

        // The prompt deliberately survives the launch — OnPlayfieldEntered takes it down once the
        // ball is actually in play. Without a trigger wired there is nothing else to close it, so
        // fall back to closing here rather than leaving it up forever.
        if (playfieldEntryTrigger == null)
        {
            DismissActivePanel();
            RefreshInputBlock();
        }

        // Armed per launch, and only while the lesson is still owed. A ball that drains straight
        // down an outlane never reaches the volume, so the beat has to survive the retry.
        if (!flipperLessonGiven && saveTheBallTrigger != null) saveTheBallTrigger.Arm();

        Log("Ball launched.");
    }

    /// <summary>
    /// Beat 4. The ball is on its way down to the flippers. Freeze it there and teach the save.
    /// </summary>
    private void OnSaveTheBallTrigger()
    {
        if (flipperLessonGiven) return;
        if (beat != Beat.BallInPlay) return;

        flipperLessonGiven = true;
        beat = Beat.FlipperLesson;

        // Paused rather than merely gated: the lesson is about a ball that is currently falling,
        // and it stops being a lesson if the ball drains while it is being read.
        SetPaused(true);

        ShowLine(flipperLessonLine, OnFlipperLessonDismissed,
            FtueBindings.Display(leftFlipperAction),
            FtueBindings.Display(rightFlipperAction));

        Log("Flipper lesson.");
    }

    private void OnFlipperLessonDismissed()
    {
        SetPaused(false);
        beat = Beat.Free;
        Log("Flipper lesson complete; free play.");
    }

    /// <summary>
    /// Beat 4a. The ball drained and the tutorial handed it straight back. Say so — otherwise the
    /// most likely reading is that something went wrong — then put the launch prompt back up.
    /// </summary>
    private void OnDrainBankCompleted()
    {
        if (beat == Beat.NotStarted || beat == Beat.Opening) return;

        // The lesson is still owed if they drained before ever reaching the trigger; re-arming
        // happens on the next launch, so make sure the stale arming does not linger.
        if (!flipperLessonGiven && saveTheBallTrigger != null) saveTheBallTrigger.Disarm();

        // No pause here. The drain routine is mid-coroutine and part of it runs on scaled time, so
        // freezing the game underneath it risks stalling the hand-back this line is announcing.
        ShowLine(ballReturnedLine, BeginLaunchBeat);
    }

    private void OnRoundStarted()
    {
        // RoundStarted fires again after every shop visit; the opening beats are once per run.
        if (sequenceStarted) return;

        sequenceStarted = true;
        StartCoroutine(RunOpeningSequence());
    }

    private IEnumerator RunOpeningSequence()
    {
        yield return WaitForIntroToFinish();

        // Captured only now: the opening pan has landed, so this is the pose the player will
        // actually associate with normal play, and the pose every focus move returns to.
        if (cameraFocus != null) cameraFocus.CapturePlayPose();

        ShowLine(introLine, ShowNamingBeat);
    }

    /// <summary>
    /// Holds the opening line until the camera pan and the ship's entry flight are done.
    /// RunFlowController keeps board input blocked for exactly that window, so its gate doubles as
    /// the "cinematic still playing" signal without needing a reference across scenes.
    /// </summary>
    private IEnumerator WaitForIntroToFinish()
    {
        float waited = 0f;

        while (GameplayInputGate.IsBlocked && waited < maxIntroWaitSeconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (waited >= maxIntroWaitSeconds)
        {
            Debug.LogWarning($"[{nameof(FtueDirector)}] Board input was still blocked after "
                + $"{maxIntroWaitSeconds}s; starting the tutorial anyway.", this);
        }
    }

    private void ShowNamingBeat()
    {
        if (namingLine.IsEmpty || namingPanelPrefab == null)
        {
            Log("No naming beat authored; skipping.");
            OnOpeningSequenceFinished();
            return;
        }

        ShowNaming(namingLine, OnNameSubmitted);
    }

    private void OnNameSubmitted(string enteredName)
    {
        // FtueNarrator owns what a valid name is, including the fallback when this is blank.
        FtueNarrator.SetName(enteredName);
        Log("Narrator named.");

        ShowLine(nameAcceptedLine, OnOpeningSequenceFinished);
    }

    private void OnOpeningSequenceFinished()
    {
        DismissActivePanel();
        RefreshInputBlock();
        BeginLaunchBeat();
    }

    /// <summary>
    /// Beat 2. Camera to the launcher, prompt up, and board input deliberately left open — the
    /// whole point is that the player can pull the plunger while reading it.
    /// </summary>
    private void BeginLaunchBeat()
    {
        beat = Beat.AwaitingLaunch;

        if (cameraFocus != null) cameraFocus.FocusOn(launcherFocusPoint);

        ShowPersistentLine(launchPromptLine, FtueBindings.Display(launchAction));

        // The prompt comes down when the ball reaches the playfield rather than the moment it is
        // launched, so it stays readable through the lane and the portal.
        if (playfieldEntryTrigger != null) playfieldEntryTrigger.Arm();

        StartLaunchLockout();
        Log("Awaiting launch.");
    }

    /// <summary>
    /// Holds board input for a moment as the prompt appears. Launch shares its key with "any input
    /// dismisses a line", so without this the same tap that closed the previous line rolls
    /// straight into a plunger pull and the prompt is gone before it can be read.
    ///
    /// Scoped to this beat on purpose. A blanket lockout after every line would kill the flippers
    /// for a moment right after the flipper lesson, with the ball already falling.
    /// </summary>
    private void StartLaunchLockout()
    {
        if (launchInputLockoutSeconds <= 0f) return;

        if (launchLockoutRoutine != null) StopCoroutine(launchLockoutRoutine);
        launchLockoutRoutine = StartCoroutine(LaunchLockoutRoutine());
    }

    private IEnumerator LaunchLockoutRoutine()
    {
        launchLockoutActive = true;
        RefreshInputBlock();

        // Unscaled: this window has to elapse even if a beat left the game paused.
        yield return new WaitForSecondsRealtime(launchInputLockoutSeconds);

        launchLockoutActive = false;
        launchLockoutRoutine = null;
        RefreshInputBlock();
    }

    /// <summary>
    /// The ball is out of the lane and into play, so the launch prompt has done its job.
    /// </summary>
    private void OnPlayfieldEntered()
    {
        if (beat != Beat.BallInPlay && beat != Beat.AwaitingLaunch) return;

        DismissActivePanel();
        RefreshInputBlock();
        Log("Ball reached the playfield.");
    }

    /// <summary>
    /// Shows an ordinary line and runs <paramref name="onAdvance"/> once the player dismisses it.
    /// <c>{0}</c> is always the speaker's name; <paramref name="extraArgs"/> fill <c>{1}</c> on.
    /// </summary>
    private void ShowLine(FtueDialogueLine line, Action onAdvance, params object[] extraArgs)
    {
        if (line == null || line.IsEmpty)
        {
            // An unauthored line must not strand the player on a panel that never appears.
            onAdvance?.Invoke();
            return;
        }

        FtueDialogueView panel = SpawnPanel(dialoguePanelPrefab);
        if (panel == null)
        {
            onAdvance?.Invoke();
            return;
        }

        string speaker = ResolveSpeakerName();
        panel.Bind(speaker, line.Resolve(BuildArgs(speaker, extraArgs)), continuePrompt,
            () =>
            {
                DismissActivePanel();
                RefreshInputBlock();
                onAdvance?.Invoke();
            });
    }

    private void ShowNaming(FtueDialogueLine line, Action<string> onSubmit)
    {
        FtueDialogueView panel = SpawnPanel(namingPanelPrefab);
        if (panel == null)
        {
            onSubmit?.Invoke(string.Empty);
            return;
        }

        string speaker = ResolveSpeakerName();
        panel.BindTextEntry(speaker, line.Resolve(BuildArgs(speaker)), namingPlaceholder,
            namingConfirmPrompt,
            entered =>
            {
                DismissActivePanel();
                RefreshInputBlock();
                onSubmit?.Invoke(entered);
            });
    }

    /// <summary>
    /// Shows a line that stays up until a later beat replaces it, and leaves board input alone so
    /// the player can act on what it is telling them.
    /// </summary>
    private void ShowPersistentLine(FtueDialogueLine line, params object[] extraArgs)
    {
        if (line == null || line.IsEmpty) return;

        FtueDialogueView panel = SpawnPanel(dialoguePanelPrefab);
        if (panel == null) return;

        activePanelBlocksInput = false;
        RefreshInputBlock();

        string speaker = ResolveSpeakerName();
        panel.BindPersistent(speaker, line.Resolve(BuildArgs(speaker, extraArgs)));
    }

    /// <summary>
    /// Freezes the board for a beat that has to be read before the ball moves again. Paired with
    /// the restore in OnDisable, because a board unloaded while paused would otherwise leave
    /// timeScale at 0 and freeze the rest of the session.
    /// </summary>
    private void SetPaused(bool paused)
    {
        if (paused == pausedByDirector) return;

        if (paused)
        {
            timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            pausedByDirector = true;
            return;
        }

        // A stored 0 would mean something else had already paused when the beat began; restoring
        // it verbatim would leave the game frozen with nothing left to un-freeze it.
        Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
        pausedByDirector = false;
    }

    private FtueDialogueView SpawnPanel(FtueDialogueView prefab)
    {
        if (prefab == null)
        {
            Debug.LogError($"[{nameof(FtueDirector)}] No dialogue prefab assigned; that beat "
                + "cannot be shown. Wire the prefabs on this component.", this);
            return null;
        }

        DismissActivePanel();

        // Parented to the director so it belongs to the board scene and is destroyed when the
        // board unloads. The prefab root carries its own Screen Space - Overlay canvas, which
        // ignores the parent transform, so nesting it here does not affect how it renders.
        activePanel = Instantiate(prefab, transform);

        // Modal by default; ShowPersistentLine clears this straight after.
        activePanelBlocksInput = true;
        RefreshInputBlock();

        return activePanel;
    }

    private void DismissActivePanel()
    {
        activePanelBlocksInput = false;

        if (activePanel == null) return;

        Destroy(activePanel.gameObject);
        activePanel = null;
    }

    /// <summary>
    /// Board input stands down while a line is on screen. Recomputed from every reason at once
    /// rather than toggled per site: the gate keys on the owner, so a second Block from this
    /// director would be released by the first Unblock.
    /// </summary>
    private void RefreshInputBlock()
    {
        // A persistent prompt is on screen precisely while the player is meant to be playing, so
        // it must not stand board input down the way a modal line does. The launch lockout is the
        // one exception: a brief hold while the prompt appears.
        bool shouldBlock = launchLockoutActive
            || (activePanel != null && activePanelBlocksInput);

        if (shouldBlock) GameplayInputGate.Block(this);
        else GameplayInputGate.Unblock(this);
    }

    private string ResolveSpeakerName()
    {
        return FtueNarrator.HasPlayerChosenName ? FtueNarrator.DisplayName : unnamedSpeaker;
    }

    private static object[] BuildArgs(string speaker, params object[] extraArgs)
    {
        if (extraArgs == null || extraArgs.Length == 0)
        {
            return new object[] { speaker };
        }

        var args = new object[extraArgs.Length + 1];
        args[0] = speaker;
        Array.Copy(extraArgs, 0, args, 1, extraArgs.Length);

        return args;
    }

    private void Log(string message)
    {
        if (!logStateChanges) return;

        Debug.Log($"[{nameof(FtueDirector)}] {message}", this);
    }
}
