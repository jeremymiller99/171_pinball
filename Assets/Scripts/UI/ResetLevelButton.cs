// Generated with Cursor (GPT-5.3-codex) by OpenAI assistant for jjmil on 2026-02-26.
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dev/testing helper: resets the current level state and restarts the level from a fresh ball hand.
/// Intended to be wired to a UI Button in a debug panel.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class ResetLevelButton : MonoBehaviour
{
    [Header("Optional (auto-found if omitted)")]
    [SerializeField] private GameRulesManager gameRules;
    [SerializeField] private ScoreManager scoreManager;

    private Button button;


    private void Awake()
    {
        button = GetComponent<Button>();
    }


    private void OnEnable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.AddListener(HandleClick);

        if (gameRules == null)
        {
            gameRules = FindFirstObjectByType<GameRulesManager>();
        }

        if (scoreManager == null)
        {
            scoreManager = FindFirstObjectByType<ScoreManager>();
        }
    }


    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }


    private void HandleClick()
    {
        if (gameRules == null)
        {
            gameRules = FindFirstObjectByType<GameRulesManager>();
        }

        if (scoreManager == null)
        {
            scoreManager = FindFirstObjectByType<ScoreManager>();
        }

        if (gameRules == null)
        {
            Debug.LogWarning(
                $"{nameof(ResetLevelButton)}: Could not find {nameof(GameRulesManager)} in loaded scenes.");
            return;
        }

        if (scoreManager == null)
        {
            Debug.LogWarning(
                $"{nameof(ResetLevelButton)}: Could not find {nameof(ScoreManager)}. " +
                "Resetting ball/round state without score reset.");
        }
        else
        {
            scoreManager.ResetForNewRound();
        }

        gameRules.StartRound();
    }
}
