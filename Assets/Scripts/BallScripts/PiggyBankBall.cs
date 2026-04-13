
using UnityEngine;

/// <summary>
/// Mint-class ball: breaks on the first scoring board hit, pays coins equal to shop sell
/// value plus stored growth from <see cref="BallLoadoutController"/>, then leaves the loadout.
/// </summary>
public sealed class PiggyBankBall : Ball
{
    public const string DefinitionId = "PiggyBank";

    [SerializeField] private BallSpawner ballSpawner;

    private bool popped;
    private bool wasUsed;

    private void Awake()
    {
        ballSpawner = ServiceLocator.Get<BallSpawner>();
        popped = false;
        wasUsed = false;
    }

    protected override void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (popped)
        {
            return;
        }

        popped = true;
        wasUsed = true;

        var loadout = ServiceLocator.Get<BallLoadoutController>();
        var marker = GetComponent<BallHandSlotMarker>();
        int slotHint = marker != null ? marker.SlotIndex : -1;
        int payout = loadout != null ? loadout.GetPiggyBankSellPayoutForSlot(slotHint) : 0;

        if (payout > 0)
        {
            ServiceLocator.Get<CoinController>()?.AddCoinsUnscaled(payout);
            if (floatingTextSpawner == null)
            {
                floatingTextSpawner = ServiceLocator.Get<FloatingTextSpawner>();
            }

            if (floatingTextSpawner != null)
            {
                Vector3 p = pos != null ? pos.position : transform.position;
                floatingTextSpawner.SpawnGoldText(p, "+$", payout);
            }
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

    private void OnDestroy()
    {
        if (!wasUsed || ballSpawner == null)
        {
            return;
        }

        var loadout = ServiceLocator.Get<BallLoadoutController>();
        if (loadout != null)
        {
            var marker = GetComponent<BallHandSlotMarker>();
            int slotHint = marker != null ? marker.SlotIndex : -1;
            loadout.ConsumeActiveBallFromLoadout(slotHint);
        }

        var rules = ServiceLocator.Get<GameRulesManager>();
        rules?.RefreshBallsRemaining();

        if (rules != null && rules.BallsRemaining > 0)
        {
            ballSpawner.ActivateNextBall();
        }
        else
        {
            rules?.ShowRoundFailed();
        }
    }
}
