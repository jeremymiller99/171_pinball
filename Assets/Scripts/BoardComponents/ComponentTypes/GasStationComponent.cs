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

        if (_surged || PayForFuel())
        {
            FireStatusUtility.GetOrAddBallStatus(ball)?.Fuel(fuelPerHit);
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
        if (FireStatusUtility.CountObjectsOnFire() >= onFireCountForSurge)
        {
            _surged = true;
            FireStatusUtility.FuelAllObjectsOnBoard(surgeFuelAmount);
        }
    }

    private bool PayForFuel()
    {
        CoinController coins = ServiceLocator.Get<CoinController>();
        return coins != null && coins.TrySpendCoins(creditCost);
    }

    private void OnBallLaunched(GameObject launched)
    {
        _surged = false;
        _surgeCheckTimer = 0f;
    }
}
