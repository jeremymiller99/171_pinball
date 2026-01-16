using UnityEngine;
using TMPro;

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

    private void Start()
    {
        // Keep existing defaults.
        points = 0f;
        mult = 1f;
        roundTotal = 0f;

        RefreshScoreUI();
    }

    public void AddPoints(float p)
    {
        points += p;
        if (pointsText != null)
            pointsText.text = points.ToString();
    }

    public void AddMult(float m)
    {
        mult += m;
        if (multText != null)
            multText.text = mult.ToString();
    }

    /// <summary>
    /// Bank the current ball score into the round total and reset the per-ball score state.
    /// Returns the banked amount (points * mult).
    /// </summary>
    public float BankCurrentBallScore()
    {
        float banked = points * mult;
        roundTotal += banked;

        // Reset for next ball.
        points = 0f;
        mult = 1f;

        RefreshScoreUI();
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
    }

    /// <summary>
    /// Optional: If you're using extra labels, call these from your rules/UI layer.
    /// </summary>
    public void SetGoal(float goal)
    {
        if (goalText != null)
            goalText.text = goal.ToString();
    }

    public void SetRoundIndex(int roundIndex)
    {
        if (roundIndexText != null)
            roundIndexText.text = (roundIndex + 1).ToString();
    }

    public void SetBallsRemaining(int ballsRemaining)
    {
        if (ballsRemainingText != null)
            ballsRemainingText.text = ballsRemaining.ToString();
    }

    public void SetCoins(int coins)
    {
        if (coinsText != null)
            coinsText.text = coins.ToString();
    }

    private void RefreshScoreUI()
    {
        if (pointsText != null)
            pointsText.text = points.ToString();
        if (multText != null)
            multText.text = mult.ToString();
        if (roundTotalText != null)
            roundTotalText.text = roundTotal.ToString();
    }
}
