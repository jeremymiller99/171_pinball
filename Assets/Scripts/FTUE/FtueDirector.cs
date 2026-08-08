// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-07 (FTUE director shell).
// Updated by Claude Code (claude-opus-5) for jjmil on 2026-08-08 (dialogue pipeline, beats 1/1a).
using System;
using System.Collections;
using UnityEngine;

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

    [Header("Safety")]
    [Tooltip("Seconds to wait for the opening camera pan and ship flight before starting the "
        + "tutorial anyway. A tutorial that never starts is worse than one that starts early.")]
    [SerializeField] private float maxIntroWaitSeconds = 15f;

    [Header("Debug")]
    [Tooltip("Logs beat transitions. Useful while wiring the board; harmless to leave on.")]
    [SerializeField] private bool logStateChanges = true;

    private GameRulesManager cachedRules;
    private FtueDialogueView activePanel;
    private bool sequenceStarted;

    // OnEnable/OnDisable rather than Awake/OnDestroy: toggling the component off is a reasonable
    // way to disable the tutorial while working in the scene, and it should lower the flag too.
    private void OnEnable()
    {
        FtueState.Activate(this);
        PinballLauncher.BallLaunched += OnBallLaunched;
        Log($"Tutorial active on '{gameObject.scene.name}'.");
    }

    private void OnDisable()
    {
        // Static event: leaving this subscribed would keep a destroyed director alive and firing
        // on every launch for the rest of the session.
        PinballLauncher.BallLaunched -= OnBallLaunched;

        UnsubscribeFromRules();
        DismissActivePanel();
        RefreshInputBlock();

        FtueState.Deactivate(this);
        Log("Tutorial released.");
    }

    // Polled rather than resolved once: GameRulesManager lives in GameplayCore and the board is
    // loaded additively, so it is not reliably resolvable on our first frame. Mirrors how
    // BasicTutorialController finds the same service.
    private void Update()
    {
        TrySubscribeToRules();
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
    }

    private void UnsubscribeFromRules()
    {
        if (cachedRules == null) return;

        cachedRules.RoundStarted -= OnRoundStarted;
        cachedRules.ShopOpened -= OnShopOpened;
        cachedRules = null;
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
    }

    /// <summary>
    /// The ball is away, so the launcher no longer needs the camera. Also covers the player who
    /// launches before the prompt has finished being read.
    /// </summary>
    private void OnBallLaunched(GameObject launched)
    {
        if (cameraFocus != null) cameraFocus.ReturnToPlayPose();
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

        // Beat 2's camera half. The prompt that goes with it, and the rest of the beats, arrive in
        // the next ticket; the camera comes back on its own when the ball is launched.
        if (cameraFocus != null) cameraFocus.FocusOn(launcherFocusPoint);

        Log("Opening beats complete; focusing the launcher.");
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
        RefreshInputBlock();

        return activePanel;
    }

    private void DismissActivePanel()
    {
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
        bool shouldBlock = activePanel != null;

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
