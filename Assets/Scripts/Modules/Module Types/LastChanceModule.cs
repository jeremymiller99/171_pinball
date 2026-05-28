using UnityEngine;

public class LastChanceModule : MonoBehaviour
{
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private RoundModifierController modifierController;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private bool currentRoundIsDevil;
    [SerializeField] private float levelUpMultiplier;

    private void Awake()
    {
        gameRulesManager = ServiceLocator.Get<GameRulesManager>();
        modifierController = ServiceLocator.Get<RoundModifierController>();
        ballSpawner = ServiceLocator.Get<BallSpawner>();
        scoreManager = ServiceLocator.Get<ScoreManager>();
        gameRulesManager.LevelChanged += CheckLevelChange;
    }

    private void CheckLevelChange()
    {
        if (currentRoundIsDevil)
        {
            currentRoundIsDevil = false;
            if (ballSpawner.HandCount == 0)
            {
                scoreManager.AddScore(gameRulesManager.coinsPerLevelUp * levelUpMultiplier, TypeOfScore.coins, transform);
            }
        }

        if (modifierController.CurrentRoundData.type == RoundType.Devil)
        {
            currentRoundIsDevil = true;
        }
        else
        {
            currentRoundIsDevil = false;
        }
    }

    private void OnDestroy()
    {
        gameRulesManager.LevelChanged -= CheckLevelChange;
    }
}
