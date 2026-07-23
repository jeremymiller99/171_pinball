using UnityEngine;

/// <summary>
/// Catalyst bumper: each ball hit costs Credits and Fuels the ball. Once five
/// objects burn at the same time, the station surges - Fueling everything on
/// the board and going free - until the next launch resets it.
/// </summary>
public class GasStationComponent : Bumper
{
    [Header("Gas Station")]
    [SerializeField] private int creditCost = 10;
    [SerializeField] private int fuelPerHit = 1;
    [SerializeField] private int onFireCountForSurge = 5;
    [SerializeField] private int surgeFuelAmount = 3;
    [SerializeField] private float surgeCheckInterval = 0.25f;

    [Header("Credit Popups")]
    [SerializeField] private Color paidPopupColor = new Color(1f, 0.84f, 0.3f);
    [SerializeField] private Color brokePopupColor = new Color(1f, 0.35f, 0.3f);

    private bool _surged;
    private float _surgeCheckTimer;

    private void OnEnable()
    {
        PinballLauncher.BallLaunched += OnBallLaunched;
    }

    private void OnDisable()
    {
        PinballLauncher.BallLaunched -= OnBallLaunched;
    }

    new protected void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        Ball ball = collision.collider.GetComponent<Ball>();
        if (ball == null)
        {
            return;
        }

        if (_surged)
        {
            FireDebug.Log($"{name}: free fuel for {ball.name} (surged)");
            FireStatusUtility.GetOrAddBallStatus(ball)?.Fuel(fuelPerHit);
        }
        else if (PayForFuel())
        {
            FireDebug.Log($"{name}: -{creditCost} credits, fuels {ball.name}");
            SpawnCreditPopup($"-{creditCost}", paidPopupColor);
            FireStatusUtility.GetOrAddBallStatus(ball)?.Fuel(fuelPerHit);
        }
        else
        {
            FireDebug.Log($"{name}: {ball.name} hit, but not enough credits");
            SpawnCreditPopup($"NEED {creditCost}", brokePopupColor);
        }
    }

    // The surge condition can be met by fire spreading anywhere on the
    // board, so it is polled rather than checked only on hits here.
    private void Update()
    {
        if (_surged || !FireStatusUtility.CanTickNow())
        {
            return;
        }

        _surgeCheckTimer += Time.deltaTime;
        if (_surgeCheckTimer < surgeCheckInterval)
        {
            return;
        }

        _surgeCheckTimer = 0f;
        int burning = FireStatusUtility.CountObjectsOnFire();
        if (burning >= onFireCountForSurge)
        {
            _surged = true;
            FireDebug.Log(
                $"{name} SURGE: {burning} objects burning, fueling whole board "
                + $"x{surgeFuelAmount}, credits now free");
            FireStatusUtility.FuelAllObjectsOnBoard(surgeFuelAmount);
        }
    }

    private bool PayForFuel()
    {
        CoinController coins = ServiceLocator.Get<CoinController>();
        return coins != null && coins.TrySpendCoins(creditCost);
    }

    private void SpawnCreditPopup(string text, Color color)
    {
        if (floatingTextSpawner == null)
        {
            floatingTextSpawner = ServiceLocator.Get<FloatingTextSpawner>();
        }
        if (floatingTextSpawner == null)
        {
            return;
        }

        floatingTextSpawner.SpawnText(
            transform.position, text, hitCountFontAsset,
            hitCountPopupScale, hitCountPopupOffset, color);
    }

    private void OnBallLaunched(GameObject launched)
    {
        if (_surged)
        {
            FireDebug.Log($"{name} reset on launch, charging credits again");
        }
        _surged = false;
        _surgeCheckTimer = 0f;
    }
}
