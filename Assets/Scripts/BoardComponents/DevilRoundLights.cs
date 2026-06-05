// Generated with Claude Code (Opus 4.8) by jjmil on 2026-06-04.
using UnityEngine;

/// <summary>
/// Drives every board light tagged <c>"Default Board Light"</c> through the
/// devil-round visual sequence:
/// <list type="number">
/// <item>A red on/off blink warning (all lights pulse) while the warning plays
/// and the devil card is up. The lights are externally locked during this phase
/// so per-frame gameplay drivers (flipper overheat, launcher meter) can't break
/// the pulse.</item>
/// <item>After the card is dismissed the lights go back to their normal on/off
/// gameplay behaviour, just rendered red — exactly like frenzy turns them blue.
/// The red comes from each <see cref="BoardLight"/>'s alternative lit / emission
/// color <see cref="DevilAlternativeIndex"/> (element 1; element 0 is the blue
/// frenzy color).</item>
/// </list>
/// While <see cref="Locked"/> is true the <see cref="FrenzyBoardLightController"/>
/// leaves the lights alone so the devil red always wins (even if frenzy is
/// triggered mid devil round).
/// </summary>
public static class DevilRoundLights
{
    private const string BoardLightTag = "Default Board Light";

    // Red is authored as alternative lit/emission color element 1 on each
    // BoardLight (element 0 is the blue frenzy color).
    private const int DevilAlternativeIndex = 1;

    private static BoardLight[] _lights;

    // Pre-warning lit state, captured because the warning force-lights every light
    // to make them all pulse. Used to restore lights that have no gameplay driver
    // when we hand control back. Null once the warning phase is over.
    private static bool[] _originalLit;

    /// <summary>True while the devil round owns the board lights (warning blink or red gameplay).</summary>
    public static bool Locked { get; private set; }

    /// <summary>Cache the board lights and start the red on/off blink warning (all lights pulse).</summary>
    public static void BeginWarningFlash(float fullCycleSeconds)
    {
        RefreshCache();
        Locked = true;

        for (int i = 0; i < _lights.Length; i++)
        {
            BoardLight light = _lights[i];
            if (light == null) continue;

            _originalLit[i] = light.IsLit;

            // Set the look while unlocked, then lock so per-frame gameplay drivers
            // (flipper overheat, launcher meter) can't clobber the blink.
            light.SetExternalControlLocked(false);
            // Unscaled so the blink keeps animating while the card pauses the game.
            light.SetFlashUsesUnscaledTime(true);
            light.SetLitAlternativeIndex(DevilAlternativeIndex);
            light.SetLit(true);
            light.StartFlashLitVersusOff(fullCycleSeconds);
            light.SetExternalControlLocked(true);
        }
    }

    /// <summary>
    /// Stop the warning blink and hand the lights back to normal gameplay, but keep
    /// them red: the red alternative stays selected and the per-light lock is
    /// released so drivers turn them on/off as usual — just red, like frenzy's blue.
    /// Frenzy stays gated (<see cref="Locked"/>) so the red still wins.
    /// </summary>
    public static void GoNormalRed()
    {
        if (_lights == null) return;

        for (int i = 0; i < _lights.Length; i++)
        {
            BoardLight light = _lights[i];
            if (light == null) continue;

            // Honor any lit change gameplay asked for during the blink; otherwise
            // restore the pre-warning state (undoing the warning's force-lit). The
            // red alternative stays selected, so whatever is lit shows red.
            bool hadPending = light.HasPendingExternalLit;
            light.SetFlashUsesUnscaledTime(false);
            light.SetExternalControlLocked(false);
            if (!hadPending && _originalLit != null && i < _originalLit.Length)
            {
                light.SetLit(_originalLit[i]);
            }
        }

        _originalLit = null;
    }

    /// <summary>Clear the devil look and release the lights back to normal gameplay.</summary>
    public static void End()
    {
        Locked = false;

        if (_lights == null) return;

        bool endedDuringWarning = _originalLit != null;

        for (int i = 0; i < _lights.Length; i++)
        {
            BoardLight light = _lights[i];
            if (light == null) continue;

            bool hadPending = light.HasPendingExternalLit;
            light.SetFlashUsesUnscaledTime(false);
            light.SetExternalControlLocked(false);

            // Only restore the pre-warning state if we never reached the red-gameplay
            // phase (ended during the warning). Otherwise gameplay already owns the
            // lit states and the captured snapshot is stale.
            if (endedDuringWarning && !hadPending && i < _originalLit.Length)
            {
                light.SetLit(_originalLit[i]);
            }

            light.ClearLitAlternativeIndex();
            light.ReapplyVisuals();
        }

        _lights = null;
        _originalLit = null;
    }

    private static void RefreshCache()
    {
        GameObject[] tagged = GameObject.FindGameObjectsWithTag(BoardLightTag);
        _lights = new BoardLight[tagged.Length];
        _originalLit = new bool[tagged.Length];
        for (int i = 0; i < tagged.Length; i++)
        {
            // Match FrenzyBoardLightController: the BoardLight sits on the tagged
            // object; fall back to a child search so none are missed.
            BoardLight light = tagged[i].GetComponent<BoardLight>();
            if (light == null) light = tagged[i].GetComponentInChildren<BoardLight>(true);
            _lights[i] = light;
        }
    }
}
