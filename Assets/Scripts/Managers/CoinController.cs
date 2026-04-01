// Updated with Cursor (Composer) by assistant on 2026-03-31.
using System;
using UnityEngine;

/// <summary>
/// Owns the run-time coin balance, modifier-scaled awards, spend checks, and HUD sync.
/// </summary>
[DisallowMultipleComponent]
public sealed class CoinController : MonoBehaviour
{
    [SerializeField] private int coins;

    private RoundModifierController ModifierController => ServiceLocator.Get<RoundModifierController>();

    public int Coins => coins;

    public event Action<int> CoinsChanged;

    private void Awake()
    {
        ServiceLocator.Register<CoinController>(this);
    }

    private void OnDisable()
    {
        ServiceLocator.Unregister<CoinController>();
    }

    private void RaiseCoinsChanged()
    {
        CoinsChanged?.Invoke(coins);
    }

    /// <summary>
    /// Sets balance at run start (no coin pickup audio).
    /// </summary>
    public void SetRunStartingBalance(int amount)
    {
        coins = Mathf.Max(0, amount);
        ServiceLocator.Get<ScoreUIController>()?.SetCoins(coins);
        RaiseCoinsChanged();
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0) return true;
        if (coins < amount)
        {
            ServiceLocator.Get<AudioManager>()?.PlayFailedPurchase();
            return false;
        }

        coins -= amount;
        ServiceLocator.Get<ScoreUIController>()?.SetCoins(coins);
        RaiseCoinsChanged();
        return true;
    }

    public void AddCoinsUnscaled(int amount)
    {
        if (amount <= 0) return;
        coins += amount;
        ServiceLocator.Get<ScoreUIController>()?.SetCoins(coins);
        ServiceLocator.Get<AudioManager>()?.PlayStaggeredCoinSounds(amount);
        RaiseCoinsChanged();
    }

    public void AddCoins(int amount) => AddCoinsScaled(amount);

    public int AddCoinsScaled(int amount)
    {
        int applied = amount;
        if (amount > 0)
        {
            float coinMultiplier = ModifierController != null
                ? ModifierController.GetModifierCoinMultiplier()
                : 1f;
            if (!Mathf.Approximately(coinMultiplier, 1f))
                applied = Mathf.FloorToInt(applied * coinMultiplier);
        }

        coins += applied;
        ServiceLocator.Get<ScoreUIController>()?.SetCoins(coins);
        ServiceLocator.Get<AudioManager>()?.PlayStaggeredCoinSounds(applied);
        RaiseCoinsChanged();
        return applied;
    }

    public int AddCoinsScaledDeferredUi(int amount)
    {
        int applied = amount;
        if (amount > 0)
        {
            float coinMultiplier = ModifierController != null
                ? ModifierController.GetModifierCoinMultiplier()
                : 1f;
            if (!Mathf.Approximately(coinMultiplier, 1f))
                applied = Mathf.FloorToInt(applied * coinMultiplier);
        }

        coins += applied;
        RaiseCoinsChanged();
        return applied;
    }

    public void ApplyDeferredCoinsUi(int applied)
    {
        ServiceLocator.Get<ScoreUIController>()?.ApplyDeferredCoinsUi(applied);
    }
}
