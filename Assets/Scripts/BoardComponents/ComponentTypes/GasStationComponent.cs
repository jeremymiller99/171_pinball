using UnityEngine;

/// <summary>
/// Catalyst bumper: each ball hit costs 1 Credit and Fuels the ball. If the station is
/// itself ignited (in any way), gasoline sprays across the board — every object is Fueled
/// once — and from then on it Fuels balls for free. Fire-based activations (burn ticks)
/// have no ball to Fuel, so they only score.
/// </summary>
public class GasStationComponent : Bumper
{
    [Header("Gas Station")]
    [SerializeField] private int creditCost = 1;
    [SerializeField] private int fuelPerHit = 1;
    [Tooltip("Fuel applied to every object on the board when the station is ignited.")]
    [SerializeField] private int sprayFuelAmount = 3;

    [Header("Credit Popups")]
    [SerializeField] private Color paidPopupColor = new Color(1f, 0.84f, 0.3f);
    [SerializeField] private Color brokePopupColor = new Color(1f, 0.35f, 0.3f);

    private ComponentFireStatus _fireStatus;
    private bool _sprayed;

    new protected void Awake()
    {
        base.Awake();

        // Ensure the station can catch fire and react the moment it does.
        _fireStatus = FireStatusUtility.GetOrAddComponentStatus(this);
        if (_fireStatus != null)
        {
            _fireStatus.Ignited += OnIgnited;
        }
    }

    private void OnEnable()
    {
        PinballLauncher.BallLaunched += OnBallLaunched;
    }

    private void OnDisable()
    {
        PinballLauncher.BallLaunched -= OnBallLaunched;
    }

    private void OnDestroy()
    {
        if (_fireStatus != null)
        {
            _fireStatus.Ignited -= OnIgnited;
        }
    }

    new protected void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        Ball ball = collision.collider.GetComponent<Ball>();
        if (ball == null)
        {
            return;
        }

        if (_sprayed)
        {
            FireDebug.Log($"{name}: free fuel for {ball.name} (sprayed)");
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

    // The moment the station catches fire, gasoline sprays across the whole board and
    // fueling goes free from then on.
    private void OnIgnited()
    {
        if (_sprayed)
        {
            return;
        }
        _sprayed = true;

        FireDebug.Log(
            $"{name} ignited: spraying the board with fuel x{sprayFuelAmount}, fuel now free");
        FireStatusUtility.FuelAllObjectsOnBoard(sprayFuelAmount);
    }

    // Launching the next ball resets the price of gas: the station charges Credits again
    // (and can spray afresh if it is ignited once more).
    private void OnBallLaunched(GameObject launched)
    {
        if (_sprayed)
        {
            FireDebug.Log($"{name}: reset on launch, charging credits again");
        }
        _sprayed = false;
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
}
