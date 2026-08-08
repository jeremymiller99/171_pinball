// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-08 (FTUE per-visit shop shelf).
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the shop is allowed to offer on one tutorial visit, authored on
/// <see cref="FtueDirector"/>.
///
/// The tutorial teaches one idea per visit — a multiplier target, then pinballs — so each shelf is
/// hand-picked rather than rolled. A mission-wide allow-list cannot express this, since it applies
/// to the whole run; and it could not hand out the mult target at all, which is not in
/// <c>ProgressionConfig.starterComponents</c> and so is locked on the brand-new profile every FTUE
/// runs on. Items named here bypass that unlock check.
///
/// An empty list means "nothing of this kind", which is a real answer and not the same as leaving
/// the visit unauthored — the last visit offers nothing new on purpose.
/// </summary>
[Serializable]
public sealed class FtueShopVisit
{
    [Tooltip("Board components offered this visit. Empty means none.")]
    [SerializeField] private List<BoardComponentDefinition> components =
        new List<BoardComponentDefinition>();

    [Tooltip("Pinballs offered this visit. Empty means none.")]
    [SerializeField] private List<BallDefinition> balls = new List<BallDefinition>();

    [Tooltip("Buying anything clears the rest of the shelf, so the visit is a choice rather than "
        + "a shopping trip. Used for the Red Two / Blue Two beat, where the player is meant to "
        + "take one — nothing otherwise stops someone with enough coins buying both.")]
    [SerializeField] private bool pickOne;

    [Tooltip("Played in order when the shop opens for this visit. Kept alongside the shelf so a "
        + "visit's copy and the items it talks about cannot drift apart.")]
    [SerializeField] private List<FtueDialogueLine> openingLines = new List<FtueDialogueLine>();

    public bool PickOne => pickOne;

    public IReadOnlyList<FtueDialogueLine> OpeningLines =>
        openingLines ?? (IReadOnlyList<FtueDialogueLine>)Array.Empty<FtueDialogueLine>();

    public IReadOnlyList<BoardComponentDefinition> Components =>
        components ?? (IReadOnlyList<BoardComponentDefinition>)Array.Empty<BoardComponentDefinition>();

    public IReadOnlyList<BallDefinition> Balls =>
        balls ?? (IReadOnlyList<BallDefinition>)Array.Empty<BallDefinition>();
}
