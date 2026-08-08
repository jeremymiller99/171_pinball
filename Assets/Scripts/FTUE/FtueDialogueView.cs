// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-08 (FTUE narrator dialogue view).
using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Put this on the ROOT of a narrator dialogue prefab and wire the slots below in the inspector.
/// <see cref="FtueDirector"/> instantiates the prefab and calls <see cref="Bind"/> or
/// <see cref="BindTextEntry"/>; you own all layout, art and fonts in the prefab.
///
/// One component, two prefabs: an ordinary line, and the naming panel that also shows an input
/// field. Leave <c>nameInput</c> empty on the ordinary one.
///
/// An ordinary line types itself out, then waits. There is no continue button: the first input
/// skips the typewriter and fills the box, the second dismisses the line. A button may still be
/// wired if a panel wants one, but nothing requires it.
///
/// The naming panel deliberately does NOT advance on any input — the player is about to type, and
/// the first keystroke would dismiss the panel. It types out, focuses the field, and waits for
/// Enter (or a wired button).
///
/// Text arrives already localized and already formatted — the director builds it through
/// <see cref="LocalizedUI.Format"/> so the AI's name and any input-binding names are ordered
/// arguments rather than concatenation. Nothing here should build a sentence.
/// </summary>
[DisallowMultipleComponent]
public sealed class FtueDialogueView : MonoBehaviour
{
    /// <summary>
    /// Longest name the player may give the AI. It is rendered in every subsequent line, so a
    /// long one wrecks the panel layout. The input field enforces it as the player types; the
    /// profile layer re-checks it, since this is a UX affordance rather than the invariant.
    /// </summary>
    public const int nameCharacterLimit = 16;

    private enum Phase
    {
        Idle = 0,
        Typing = 1,
        AwaitingAdvance = 2,
        AwaitingTextEntry = 3
    }

    [Header("Text slots — filled at runtime with localized, pre-formatted strings")]
    [Tooltip("Shows who is talking. Set to the AI's player-chosen name.")]
    [SerializeField] private TextMeshProUGUI speakerLabel;

    [Tooltip("The line itself. Revealed a character at a time.")]
    [SerializeField] private TextMeshProUGUI bodyLabel;

    [Header("Continue prompt")]
    [Tooltip("Optional 'press any key' hint. Hidden while the line is still typing and shown "
        + "once it has finished, so it never invites input the panel is not ready for.")]
    [SerializeField] private TextMeshProUGUI continuePromptLabel;

    [Tooltip("Optional. Any input already advances an ordinary line, so this is only needed if a "
        + "panel wants a clickable button as well.")]
    [SerializeField] private Button continueButton;

    [Header("Typewriter")]
    [Tooltip("Characters revealed per second. 0 or less shows the whole line immediately.")]
    [SerializeField] private float charactersPerSecond = 40f;

    [Header("Text entry — naming panel only")]
    [Tooltip("Leave EMPTY on the ordinary dialogue prefab. Only the naming panel needs this.")]
    [SerializeField] private TMP_InputField nameInput;

    // Cleared as soon as they fire, so a click and a key press in the same frame cannot
    // double-advance a beat.
    private Action pendingAdvance;
    private Action<string> pendingSubmit;

    private Phase phase = Phase.Idle;
    private int totalCharacters;
    private float revealedCharacters;

    // The press that opened a panel, or the press that skipped the typewriter, must not also be
    // read as the press that dismisses it. Input is ignored on the frame the phase changed.
    private int phaseEnteredFrame = -1;

    /// <summary>True while the line is still revealing itself.</summary>
    public bool IsTyping => phase == Phase.Typing;

    /// <summary>Shows an ordinary line. <paramref name="onAdvance"/> fires once.</summary>
    public void Bind(string speaker, string body, string continuePrompt, Action onAdvance)
    {
        pendingAdvance = onAdvance;
        pendingSubmit = null;

        if (nameInput != null) nameInput.gameObject.SetActive(false);

        ApplyHeader(speaker, continuePrompt);
        WireContinueButton(RequestAdvance);
        BeginTypewriter(body);
    }

    /// <summary>
    /// Shows the naming panel. <paramref name="onSubmit"/> fires once with the trimmed contents of
    /// the field, which may be empty — deciding what an empty name means belongs with the profile,
    /// not with a view.
    /// </summary>
    public void BindTextEntry(string speaker, string body, string placeholder,
        string confirmLabel, Action<string> onSubmit)
    {
        pendingAdvance = null;
        pendingSubmit = onSubmit;

        ApplyHeader(speaker, confirmLabel);
        WireContinueButton(Submit);

        if (nameInput == null)
        {
            Debug.LogError($"[{nameof(FtueDialogueView)}] BindTextEntry called on a prefab with no "
                + "input field wired. Falling back to an ordinary line so the tutorial cannot "
                + "dead-end; the player will not be able to enter a name.", this);
            BeginTypewriter(body);
            return;
        }

        // Hidden until the line has finished typing, so the player is not invited to type into a
        // field while the panel is still talking.
        nameInput.gameObject.SetActive(false);
        nameInput.characterLimit = nameCharacterLimit;
        nameInput.text = string.Empty;

        if (nameInput.placeholder is TextMeshProUGUI placeholderLabel)
        {
            placeholderLabel.text = placeholder ?? string.Empty;
        }

        nameInput.onSubmit.RemoveAllListeners();
        nameInput.onSubmit.AddListener(_ => Submit());

        BeginTypewriter(body);
    }

    /// <summary>
    /// What any input does: fill the line if it is still typing, dismiss it if it is not. Public
    /// so the director can drive it from elsewhere. Safe to call at any time.
    /// </summary>
    public void RequestAdvance()
    {
        if (phase == Phase.Typing)
        {
            CompleteTypewriter();
            return;
        }

        if (phase == Phase.AwaitingAdvance) Advance();
    }

    /// <summary>Dismisses the line immediately, typewriter or not. Safe to call twice.</summary>
    public void Advance()
    {
        Action callback = pendingAdvance;
        pendingAdvance = null;
        phase = Phase.Idle;
        callback?.Invoke();
    }

    /// <summary>Submits the entered name. Safe to call twice.</summary>
    public void Submit()
    {
        Action<string> callback = pendingSubmit;
        pendingSubmit = null;

        if (callback == null) return;

        phase = Phase.Idle;
        string entered = nameInput != null ? nameInput.text : string.Empty;
        callback.Invoke((entered ?? string.Empty).Trim());
    }

    private void Update()
    {
        if (phase == Phase.Typing) TickTypewriter();

        // The naming panel owns the keyboard once its field is focused, so any-input advancing is
        // deliberately not offered in that phase.
        if (phase != Phase.Typing && phase != Phase.AwaitingAdvance) return;

        if (Time.frameCount <= phaseEnteredFrame) return;
        if (!WasAdvancePressed()) return;

        RequestAdvance();
    }

    private void BeginTypewriter(string body)
    {
        string text = body ?? string.Empty;

        if (bodyLabel == null)
        {
            EnterAwaitingPhase();
            return;
        }

        bodyLabel.text = text;

        // The mesh has to be rebuilt before characterCount reflects the new text, and the count
        // must exclude rich-text tags -- which is exactly what maxVisibleCharacters counts too.
        bodyLabel.ForceMeshUpdate();
        totalCharacters = bodyLabel.textInfo.characterCount;

        if (charactersPerSecond <= 0f || totalCharacters <= 0)
        {
            CompleteTypewriter();
            return;
        }

        revealedCharacters = 0f;
        bodyLabel.maxVisibleCharacters = 0;
        SetPhase(Phase.Typing);
        ShowContinuePrompt(false);
    }

    private void TickTypewriter()
    {
        // Unscaled: the director pauses the game for most beats, and a typewriter driven by
        // scaled time would sit frozen mid-sentence.
        revealedCharacters += charactersPerSecond * Time.unscaledDeltaTime;

        if (revealedCharacters >= totalCharacters)
        {
            CompleteTypewriter();
            return;
        }

        if (bodyLabel != null)
        {
            bodyLabel.maxVisibleCharacters = Mathf.FloorToInt(revealedCharacters);
        }
    }

    private void CompleteTypewriter()
    {
        if (bodyLabel != null)
        {
            // int.MaxValue rather than the counted total: TMP treats it as "no limit", so this is
            // correct even if the count was never established.
            bodyLabel.maxVisibleCharacters = int.MaxValue;
        }

        EnterAwaitingPhase();
    }

    private void EnterAwaitingPhase()
    {
        bool wantsTextEntry = pendingSubmit != null && nameInput != null;
        SetPhase(wantsTextEntry ? Phase.AwaitingTextEntry : Phase.AwaitingAdvance);
        ShowContinuePrompt(true);

        if (!wantsTextEntry) return;

        nameInput.gameObject.SetActive(true);
        nameInput.Select();
        nameInput.ActivateInputField();
    }

    private void SetPhase(Phase next)
    {
        phase = next;
        phaseEnteredFrame = Time.frameCount;
    }

    private void ApplyHeader(string speaker, string promptText)
    {
        if (speakerLabel != null) speakerLabel.text = speaker ?? string.Empty;

        if (continuePromptLabel != null && !string.IsNullOrEmpty(promptText))
        {
            continuePromptLabel.text = promptText;
        }

        ShowContinuePrompt(false);
    }

    private void ShowContinuePrompt(bool visible)
    {
        if (continuePromptLabel != null) continuePromptLabel.gameObject.SetActive(visible);
    }

    private void WireContinueButton(Action onClick)
    {
        if (continueButton == null) return;

        // RemoveAllListeners first also clears the audio listener added on the previous bind,
        // so re-showing a pooled panel does not stack click sounds.
        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(() => onClick());
        ServiceLocator.Get<AudioManager>()?.WireButtonAudio(continueButton);
    }

    /// <summary>
    /// Any key, mouse button or face/shoulder button counts. Escape is excluded on purpose: it
    /// belongs to the pause menu, and swallowing it here would leave the player unable to pause
    /// while a line is on screen.
    /// </summary>
    private static bool WasAdvancePressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame) return false;
            if (keyboard.anyKey.wasPressedThisFrame) return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null
            && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
        {
            return true;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad == null) return false;

        return gamepad.buttonSouth.wasPressedThisFrame
            || gamepad.buttonEast.wasPressedThisFrame
            || gamepad.buttonWest.wasPressedThisFrame
            || gamepad.buttonNorth.wasPressedThisFrame;
    }
}
