// Generated with Antigravity by jjmil on 2026-04-09.
using UnityEngine;

/// <summary>
/// Listens for frenzy activation / deactivation on a
/// <see cref="DropTargetsScoringMode"/> and switches every
/// <see cref="BoardLight"/> tagged <c>"Default Board Light"</c>
/// to its first alternative lit color (index 0) during frenzy,
/// then clears the alternative on deactivation.
/// </summary>
public class FrenzyBoardLightController : MonoBehaviour
{
    private const string boardLightTag = "Default Board Light";
    private const int frenzyAlternativeIndex = 0;

    [Header("References")]
    [Tooltip("The DropTargetsScoringMode that fires frenzy events.")]
    [SerializeField]
    private DropTargetsScoringMode scoringMode;

    private BoardLight[] _cachedLights;

    private void OnEnable()
    {
        if (scoringMode != null)
        {
            scoringMode.OnFrenzyActivated +=
                HandleFrenzyActivated;
            scoringMode.OnFrenzyDeactivated +=
                HandleFrenzyDeactivated;
        }
    }

    private void OnDisable()
    {
        if (scoringMode != null)
        {
            scoringMode.OnFrenzyActivated -=
                HandleFrenzyActivated;
            scoringMode.OnFrenzyDeactivated -=
                HandleFrenzyDeactivated;
        }
    }

    private void HandleFrenzyActivated()
    {
        RefreshCache();

        foreach (BoardLight light in _cachedLights)
        {
            if (light == null) continue;
            light.SetLitAlternativeIndex(frenzyAlternativeIndex);
            light.ReapplyVisuals();
        }
    }

    private void HandleFrenzyDeactivated()
    {
        if (_cachedLights == null) return;

        foreach (BoardLight light in _cachedLights)
        {
            if (light == null) continue;
            light.ClearLitAlternativeIndex();
            light.ReapplyVisuals();
        }
    }

    private void RefreshCache()
    {
        GameObject[] tagged =
            GameObject.FindGameObjectsWithTag(boardLightTag);

        _cachedLights = new BoardLight[tagged.Length];

        for (int i = 0; i < tagged.Length; i++)
        {
            _cachedLights[i] =
                tagged[i].GetComponent<BoardLight>();
        }
    }
}
