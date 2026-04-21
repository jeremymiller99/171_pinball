using UnityEngine;

/// <summary>
/// Striker ball: each scoring <see cref="TypeOfScore.points"/> board hit awards points using the
/// current <see cref="Ball.PointMultiplier"/>, then increases that multiplier by a fixed increment
/// (default +1) so later point hits pay more.
/// </summary>
public sealed class SnowballBall : Ball
{
    public const string DefinitionId = "Snowball";

    [Tooltip("Added to PointMultiplier after each scoring points-type board hit.")]
    [SerializeField] private float pointMultiplierGrowthPerPointsHit = 1f;

    private void OnValidate()
    {
        pointMultiplierGrowthPerPointsHit = Mathf.Max(0f, pointMultiplierGrowthPerPointsHit);
    }

    protected override void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        base.AddScore(amount, typeOfScore, pos);

        if (typeOfScore != TypeOfScore.points)
        {
            return;
        }

        if (Mathf.Approximately(amount, 0f))
        {
            return;
        }

        PointMultiplier += pointMultiplierGrowthPerPointsHit;
    }
}
