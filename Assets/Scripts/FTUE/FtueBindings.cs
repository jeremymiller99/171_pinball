// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-07 (FTUE input prompt strings).
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Resolves the display name of an input binding so tutorial copy can say "Hold Space" without
/// the word "Space" being written into the copy. Prompts then track the bindings instead of
/// drifting from them — the FTUE brief originally said "hold Enter", which had not been true
/// since Launch moved to Space.
///
/// Feed the result to <see cref="LocalizedUI.Format"/> as an ordered argument, never by
/// concatenation: word order differs by language, and the binding is only one of the
/// substitutions a line can carry (the AI's name is the other).
/// </summary>
public static class FtueBindings
{
    /// <summary>
    /// Keyboard bindings sit at index 0 and gamepad at index 1 by project convention
    /// (STYLE_GUIDE.md, "Input System"). Verified for Launch, LeftFlip and RightFlip.
    /// </summary>
    private const int keyboardBindingIndex = 0;

    /// <summary>Shown when a binding cannot be resolved, so a prompt never renders as empty.</summary>
    private const string unknownBinding = "?";

    /// <summary>
    /// Display string for the action's keyboard binding, e.g. "Space".
    ///
    /// Deliberately always the keyboard binding rather than switching on whether a gamepad is
    /// connected. Connected is not the same as in use — a player with a controller plugged in but
    /// hands on the keyboard would be shown the wrong prompt — and answering that properly means
    /// tracking the last-used device, which is a wider change than the tutorial should make.
    /// </summary>
    public static string Display(InputActionReference reference)
    {
        return Display(reference, keyboardBindingIndex);
    }

    /// <summary>
    /// Display string for a specific binding index, for the rare prompt that needs to name a
    /// binding other than the keyboard one.
    /// </summary>
    public static string Display(InputActionReference reference, int bindingIndex)
    {
        InputAction action = reference != null ? reference.action : null;
        if (action == null)
        {
            Debug.LogWarning("[FtueBindings] No action on the supplied reference; "
                + "the prompt will read '" + unknownBinding + "'.");
            return unknownBinding;
        }

        // GetBindingDisplayString throws rather than returning empty for an index outside the
        // action's binding list, and a mis-authored index should not take the tutorial down.
        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            Debug.LogWarning($"[FtueBindings] Binding index {bindingIndex} is out of range for "
                + $"action '{action.name}' ({action.bindings.Count} bindings).");
            return unknownBinding;
        }

        string display = action.GetBindingDisplayString(bindingIndex);
        return string.IsNullOrWhiteSpace(display) ? unknownBinding : display;
    }
}
