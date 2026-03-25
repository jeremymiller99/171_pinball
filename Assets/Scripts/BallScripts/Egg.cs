// Updated with Cursor (GPT-5.2) by OpenAI assistant for jjmil on 2026-02-24.

using UnityEngine;

public class EggBall : Ball
{
    [SerializeField] private float nextBallPointMultiplier;
    [SerializeField] private float nextBallMultMultiplier;
    [SerializeField] private int nextBallCoinMultiplier;
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private bool applyOnUseOnly = true;

    private bool wasUsed;
    [Header("Stacking (runtime)")]
    [SerializeField, Tooltip("Runtime: multiplies this egg's next-ball effect when boosted by a previous egg.")]
    private float stackedNextBallPointMultiplier = 1f;
    
    [SerializeField, Tooltip("Runtime: multiplies this egg's next-ball effect when boosted by a previous egg.")]
    private float stackedNextBallMultMultiplier = 1f;
    
    [SerializeField, Tooltip("Runtime: multiplies this egg's next-ball effect when boosted by a previous egg.")]
    private int stackedNextBallCoinMultiplier = 1;
    
    [SerializeField, Tooltip("Runtime: guards against multiple pops in the same frame.")]
    private bool popped;
    

    new protected void Awake()
    {
        base.Awake();
        ballSpawner = FindFirstObjectByType<BallSpawner>();
        stackedNextBallPointMultiplier = 1f;
        stackedNextBallMultMultiplier = 1f;
        stackedNextBallCoinMultiplier = 1;
        popped = false;
    }

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (popped)
        {
            return;
        }

        popped = true;
        wasUsed = true;

        if (ballSpawner != null && ballSpawner.HandCount > 0)
        {
            GetEffectiveNextBallFactors(out float pointFactor, out _, out _);
            TrySpawnEggPopup(pointFactor, pos != null ? pos.position : transform.position);
        }

        if (ballSpawner != null)
        {
            ballSpawner.DespawnBall(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected override bool ShouldScoreBoardComponent(BoardComponent component)
    {
        return component != null && component.GetComponent<Portal>() == null;
    }

    protected override void OnDestroy()
    {
        if (!(applyOnUseOnly && !wasUsed)
            && ballSpawner != null
            && ballSpawner.HandCount > 0)
        {
            GameObject nextBallObject = ballSpawner.ActivateNextBall();
            if (nextBallObject != null)
            {
                Ball nextBall = nextBallObject.GetComponent<Ball>();
                if (nextBall != null)
                {
                    GetEffectiveNextBallFactors(out float pointFactor, out float multFactor, out int coinFactor);

                    nextBall.pointMultiplier *= pointFactor;
                    nextBall.multMultiplier *= multFactor;
                    nextBall.coinMultiplier *= coinFactor;

                    if (nextBall is EggBall nextEgg)
                    {
                        nextEgg.StackEggEffect(pointFactor, multFactor, coinFactor);
                    }
                }
            }
        }

        base.OnDestroy();
    }

    public void StackEggEffect(float pointFactor, float multFactor, int coinFactor)
    {
        float p = pointFactor <= 0f ? 1f : pointFactor;
        float m = multFactor <= 0f ? 1f : multFactor;
        int c = coinFactor <= 0 ? 1 : coinFactor;

        stackedNextBallPointMultiplier *= p;
        stackedNextBallMultMultiplier *= m;
        stackedNextBallCoinMultiplier *= c;
    }

    private void GetEffectiveNextBallFactors(out float pointFactor, out float multFactor, out int coinFactor)
    {
        float basePoint = nextBallPointMultiplier <= 0f ? 1f : nextBallPointMultiplier;
        float baseMult = nextBallMultMultiplier <= 0f ? 1f : nextBallMultMultiplier;
        int baseCoin = nextBallCoinMultiplier <= 0 ? 1 : nextBallCoinMultiplier;

        float stackPoint = stackedNextBallPointMultiplier <= 0f ? 1f : stackedNextBallPointMultiplier;
        float stackMult = stackedNextBallMultMultiplier <= 0f ? 1f : stackedNextBallMultMultiplier;
        int stackCoin = stackedNextBallCoinMultiplier <= 0 ? 1 : stackedNextBallCoinMultiplier;

        pointFactor = basePoint * stackPoint;
        multFactor = baseMult * stackMult;
        coinFactor = baseCoin * stackCoin;
    }

    private void TrySpawnEggPopup(float pointFactor, Vector3 worldPos)
    {
        if (floatingTextSpawner == null)
        {
            floatingTextSpawner = FindFirstObjectByType<FloatingTextSpawner>();
        }

        if (floatingTextSpawner == null)
        {
            return;
        }

        string text = "x" + FormatFactor(pointFactor);
        floatingTextSpawner.SpawnMultText(worldPos, text, pointFactor);
    }

    private static string FormatFactor(float value)
    {
        const float epsilon = 0.001f;
        int rounded = Mathf.RoundToInt(value);
        if (Mathf.Abs(value - rounded) <= epsilon)
        {
            return rounded.ToString();
        }

        return value.ToString("0.##");
    }
}
