using UnityEngine;
using TMPro;
using System;
using UnityEngine.SceneManagement;

public class ScoreManager : MonoBehaviour
{
    // NOTE: Keep these names/public fields so existing scripts (PointAdder/MultAdder)
    // keep working without modification. Conceptually, `points` are the current ball's points.
    public float points;
    public float mult;

    // Total banked score across the current round (sum of each drained ball's banked score).
    public float roundTotal;

    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text multText;

    // Optional UI hooks (wire in inspector if you have these labels).
    [Header("Optional extra UI")]
    [SerializeField] private TMP_Text roundIndexText;
    [SerializeField] private TMP_Text roundTotalText;
    [SerializeField] private TMP_Text goalText;
    [SerializeField] private TMP_Text ballsRemainingText;
    [SerializeField] private TMP_Text coinsText;

    [Header("Scoring Control")]
    [SerializeField] private bool scoringLocked;

    // Stored goal value for the current round (set via SetGoal).
    private float _goal;

    /// <summary>
    /// Fired whenever score-related values change (points/mult/roundTotal/goal).
    /// Useful for non-TMP UI like meters/bars that should update immediately.
    /// </summary>
    public event Action ScoreChanged;

    /// <summary>
    /// Current round goal (set by GameRulesManager via SetGoal).
    /// </summary>
    public float Goal => _goal;

    /// <summary>
    /// Live round progress total: banked round total plus current ball's (points * mult).
    /// </summary>
    public float LiveRoundTotal => roundTotal + (points * mult);

    private const string ScorePanelRootName = "Score Panel";
    private const string RoundInfoPanelRootName = "Round Info Panel";
    private const string PointsObjectName = "Points";
    private const string MultObjectName = "Mult";
    private const string RoundIndexObjectName = "Round Index";
    private const string RoundTotalObjectName = "RoundTotal";
    private const string GoalObjectName = "Goal";
    private const string BallsRemainingObjectName = "Balls Remaining";
    private const string CoinsObjectName = "Coins";

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureCoreScoreTextBindings();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        // Keep existing defaults.
        points = 0f;
        mult = 1f;
        roundTotal = 0f;
        _goal = 0f;

        EnsureCoreScoreTextBindings();
        RefreshScoreUI();
        ScoreChanged?.Invoke();
    }

    public void AddPoints(float p)
    {
        if (scoringLocked) return;
        EnsureCoreScoreTextBindings();
        points += p;
        if (pointsText != null)
            pointsText.text = points.ToString();
        ScoreChanged?.Invoke();
    }

    public void AddMult(float m)
    {
        if (scoringLocked) return;
        EnsureCoreScoreTextBindings();
        mult += m;
        if (multText != null)
            multText.text = mult.ToString();
        ScoreChanged?.Invoke();
    }

    public void SetScoringLocked(bool locked)
    {
        scoringLocked = locked;
    }

    /// <summary>
    /// Bank the current ball score into the round total and reset the per-ball score state.
    /// Returns the banked amount (points * mult).
    /// </summary>
    public float BankCurrentBallScore()
    {
        return BankCurrentBallScore(1f);
    }

    /// <summary>
    /// Bank the current ball score into the round total, multiplied by <paramref name="bankMultiplier"/>,
    /// then reset the per-ball score state.
    /// Returns the banked amount (points * mult * bankMultiplier).
    /// </summary>
    public float BankCurrentBallScore(float bankMultiplier)
    {
        float m = bankMultiplier;
        if (m <= 0f) m = 1f;

        float banked = points * mult * m;
        roundTotal += banked;

        // Reset for next ball.
        points = 0f;
        mult = 1f;

        RefreshScoreUI();
        ScoreChanged?.Invoke();
        return banked;
    }

    /// <summary>
    /// Reset round and per-ball scoring back to defaults.
    /// </summary>
    public void ResetForNewRound()
    {
        roundTotal = 0f;
        points = 0f;
        mult = 1f;

        RefreshScoreUI();
        ScoreChanged?.Invoke();
    }

    /// <summary>
    /// Optional: If you're using extra labels, call these from your rules/UI layer.
    /// </summary>
    public void SetGoal(float goal)
    {
        _goal = goal;
        EnsureCoreScoreTextBindings();
        if (goalText != null)
            goalText.text = goal.ToString();
        ScoreChanged?.Invoke();
    }

    public void SetRoundIndex(int roundIndex)
    {
        EnsureCoreScoreTextBindings();
        if (roundIndexText != null)
            roundIndexText.text = (roundIndex + 1).ToString();
    }

    public void SetBallsRemaining(int ballsRemaining)
    {
        EnsureCoreScoreTextBindings();
        if (ballsRemainingText != null)
            ballsRemainingText.text = ballsRemaining.ToString();
    }

    public void SetCoins(int coins)
    {
        EnsureCoreScoreTextBindings();
        if (coinsText != null)
            coinsText.text = coins.ToString();
    }

    private void RefreshScoreUI()
    {
        EnsureCoreScoreTextBindings();
        if (pointsText != null)
            pointsText.text = points.ToString();
        if (multText != null)
            multText.text = mult.ToString();
        if (roundTotalText != null)
            roundTotalText.text = roundTotal.ToString();
        if (goalText != null)
            goalText.text = _goal.ToString();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // In additive-scene setups, the Score UI may live in a different scene than this manager.
        // Re-resolve references whenever a new scene is loaded.
        EnsureCoreScoreTextBindings();
        RefreshScoreUI();
    }

    private void EnsureCoreScoreTextBindings()
    {
        bool scorePanelBound = IsLiveSceneText(pointsText) && IsLiveSceneText(multText);
        bool roundInfoBound = IsLiveSceneText(roundIndexText) && IsLiveSceneText(roundTotalText) 
                              && IsLiveSceneText(goalText) && IsLiveSceneText(ballsRemainingText) 
                              && IsLiveSceneText(coinsText);

        if (scorePanelBound && roundInfoBound)
            return;

        // Prefer binding within a Score Panel root if present.
        if (!scorePanelBound)
        {
            GameObject scorePanel = GameObject.Find(ScorePanelRootName);
            if (scorePanel != null)
            {
                if (!IsLiveSceneText(pointsText))
                    pointsText = FindTmpTextInChildrenByName(scorePanel.transform, PointsObjectName);
                if (!IsLiveSceneText(multText))
                    multText = FindTmpTextInChildrenByName(scorePanel.transform, MultObjectName);
            }
        }

        // Prefer binding within Round Info Panel root if present.
        if (!roundInfoBound)
        {
            GameObject roundInfoPanel = GameObject.Find(RoundInfoPanelRootName);
            if (roundInfoPanel != null)
            {
                if (!IsLiveSceneText(roundIndexText))
                    roundIndexText = FindTmpTextInChildrenByName(roundInfoPanel.transform, RoundIndexObjectName);
                if (!IsLiveSceneText(roundTotalText))
                    roundTotalText = FindTmpTextInChildrenByName(roundInfoPanel.transform, RoundTotalObjectName);
                if (!IsLiveSceneText(goalText))
                    goalText = FindTmpTextInChildrenByName(roundInfoPanel.transform, GoalObjectName);
                if (!IsLiveSceneText(ballsRemainingText))
                    ballsRemainingText = FindTmpTextInChildrenByName(roundInfoPanel.transform, BallsRemainingObjectName);
                if (!IsLiveSceneText(coinsText))
                    coinsText = FindTmpTextInChildrenByName(roundInfoPanel.transform, CoinsObjectName);
            }
        }

        // Recheck after panel-based search.
        scorePanelBound = IsLiveSceneText(pointsText) && IsLiveSceneText(multText);
        roundInfoBound = IsLiveSceneText(roundIndexText) && IsLiveSceneText(roundTotalText) 
                         && IsLiveSceneText(goalText) && IsLiveSceneText(ballsRemainingText) 
                         && IsLiveSceneText(coinsText);

        if (scorePanelBound && roundInfoBound)
            return;

        // Fallback: search all loaded-scene TMP_Text objects (including inactive).
        // Resources.FindObjectsOfTypeAll includes assets/prefabs too, so filter by valid scene.
        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < allTexts.Length; i++)
        {
            TMP_Text t = allTexts[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            if (!t.gameObject.activeInHierarchy) continue;

            string n = t.gameObject.name;
            
            // Score Panel elements
            if (!IsLiveSceneText(pointsText) && string.Equals(n, PointsObjectName, StringComparison.OrdinalIgnoreCase))
                pointsText = t;
            else if (!IsLiveSceneText(multText) && string.Equals(n, MultObjectName, StringComparison.OrdinalIgnoreCase))
                multText = t;
            // Round Info Panel elements
            else if (!IsLiveSceneText(roundIndexText) && string.Equals(n, RoundIndexObjectName, StringComparison.OrdinalIgnoreCase))
                roundIndexText = t;
            else if (!IsLiveSceneText(roundTotalText) && string.Equals(n, RoundTotalObjectName, StringComparison.OrdinalIgnoreCase))
                roundTotalText = t;
            else if (!IsLiveSceneText(goalText) && string.Equals(n, GoalObjectName, StringComparison.OrdinalIgnoreCase))
                goalText = t;
            else if (!IsLiveSceneText(ballsRemainingText) && string.Equals(n, BallsRemainingObjectName, StringComparison.OrdinalIgnoreCase))
                ballsRemainingText = t;
            else if (!IsLiveSceneText(coinsText) && string.Equals(n, CoinsObjectName, StringComparison.OrdinalIgnoreCase))
                coinsText = t;
        }
    }

    private static TMP_Text FindTmpTextInChildrenByName(Transform root, string childName)
    {
        if (root == null) return null;

        // Look for an exact name match (case-insensitive) and grab TMP on that object.
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            if (t == null) continue;
            if (!t.gameObject.activeInHierarchy) continue;
            if (string.Equals(t.gameObject.name, childName, StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return null;
    }

    private static bool IsLiveSceneText(TMP_Text t)
    {
        if (t == null) return false;
        if (!t.gameObject.scene.IsValid()) return false;
        if (!t.gameObject.activeInHierarchy) return false;
        return true;
    }
}
