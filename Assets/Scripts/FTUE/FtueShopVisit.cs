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

    public IReadOnlyList<BoardComponentDefinition> Components =>
        components ?? (IReadOnlyList<BoardComponentDefinition>)Array.Empty<BoardComponentDefinition>();

    public IReadOnlyList<BallDefinition> Balls =>
        balls ?? (IReadOnlyList<BallDefinition>)Array.Empty<BallDefinition>();
}
