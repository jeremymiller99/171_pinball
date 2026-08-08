// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-08 (FTUE authored copy holder).
using System;
using UnityEngine;

/// <summary>
/// One authored line of narrator copy, edited in the inspector on <see cref="FtueDirector"/>.
///
/// The sequence of beats lives in code because the beats are not interchangeable — one waits on a
/// dismissal, one on a launch, one on a trigger volume, one on three power surges. What genuinely
/// changes often is the wording, and that is what this makes editable without a recompile.
///
/// <b>Placeholders.</b> <c>{0}</c> is always the AI's name; the director supplies it for every
/// line. Anything a specific beat needs — an input binding name, a count — follows from
/// <c>{1}</c>. Never build these by concatenation: word order differs by language, which is the
/// whole reason <see cref="LocalizedUI.Format"/> takes ordered arguments.
/// </summary>
[Serializable]
public sealed class FtueDialogueLine
{
    [Tooltip("Key in the Gameplay string table. Leave empty while drafting — the text below is "
        + "used as-is until the key exists.")]
    [SerializeField] private string localizationKey;

    [Tooltip("English source text, and the fallback whenever the key is missing. Use {0} for the "
        + "AI's name and {1}+ for anything else the beat passes in.")]
    [TextArea(2, 6)]
    [SerializeField] private string englishText;

    /// <summary>True when there is nothing to show, so the director can skip the beat.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(englishText)
        && string.IsNullOrWhiteSpace(localizationKey);

    /// <summary>
    /// The localized, formatted line. <see cref="LocalizedUI.Format"/> already falls back twice if
    /// a translator breaks the placeholders, so a bad table entry degrades to the English source
    /// rather than throwing mid-tutorial.
    /// </summary>
    public string Resolve(params object[] args)
    {
        return LocalizedUI.Format(localizationKey, englishText ?? string.Empty, args);
    }
}
