// from the hand during that level (tracked until next StartRound).
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BallLoadoutController : MonoBehaviour
{
    [Header("Capacity")]
    [SerializeField] private int startingMaxBalls = 5;

    [Header("Starting Hand")]
    [Tooltip("Optional: starting ball prefabs for the player's hand/loadout. If empty, falls back to spawner's default.")]
    [SerializeField] private List<GameObject> startingBallLoadout = new List<GameObject>();

    [Tooltip("Preferred: starting ball definitions for the player's hand/loadout.")]
    [SerializeField] private List<BallDefinition> startingBallLoadoutDefinitions = new List<BallDefinition>();

    [Header("Debug (read-only)")]
    [SerializeField] private int maxBalls;
    
    // The active deck, kept tightly packed (no null gaps).
    private readonly List<BallDefinition> _ballLoadout = new List<BallDefinition>();

    /// <summary>
    /// Parallel to <see cref="_ballLoadout"/>: true if the ball in this slot has been
    /// permanently amped up by an <see cref="AmpUpBall"/> used in front of it.
    /// An amped ball gains a 1/4 chance per component hit to award +0.1 mult.
    /// </summary>
    private readonly List<bool> _ampedUpBySlot = new List<bool>();

    /// <summary>
    /// Parallel to <see cref="_ballLoadout"/>: extra coin payout for
    /// <see cref="PiggyBankBall"/> (added each round while in loadout).
    /// </summary>
    private readonly List<int> _piggyBankExtraSellBySlot = new List<int>();

    /// <summary>
    /// Parallel to loadout: for Piggy Bank slots, true if this level's +$3 growth may still apply
    /// (ball has not been launched from the hand this level). Cleared each
    /// <see cref="GameRulesManager.StartRound"/>.
    /// </summary>
    private readonly List<bool> _piggyBankHandGrowthEligibleThisLevel = new List<bool>();

    public int MaxBalls => maxBalls;
    public int BallLoadoutCount => _ballLoadout.Count;

    private void Awake()
    {
        ServiceLocator.Register<BallLoadoutController>(this);
        maxBalls = Mathf.Max(1, startingMaxBalls);
    }

    private void OnDisable()
    {
        ServiceLocator.Unregister<BallLoadoutController>();
    }

    /// <summary>
    /// Returns a copy of the current ball definitions in the hand.
    /// Safe to enumerate without risking external mutation.
    /// </summary>
    public List<BallDefinition> GetBallLoadoutSnapshot()
    {
        return new List<BallDefinition>(_ballLoadout);
    }

    /// <summary>
    /// Returns the instantiated prefabs mapping to the current loadout definitions.
    /// </summary>
    public List<GameObject> GetBallLoadoutPrefabSnapshot()
    {
        var prefabs = new List<GameObject>(_ballLoadout.Count);
        for (int i = 0; i < _ballLoadout.Count; i++)
        {
            BallDefinition def = _ballLoadout[i];
            prefabs.Add(def != null ? def.Prefab : null);
        }
        return prefabs;
    }

    /// <summary>
    /// Adds permanent slots to the max capacity, optionally filling them with standard balls immediately.
    /// </summary>
    public void AddMaxBalls(int delta)
    {
        if (delta > 0)
        {
            maxBalls += delta;
            TryAddFallbackBallsToLoadout(delta);
        }
        else if (delta < 0)
        {
            maxBalls = Mathf.Max(1, maxBalls + delta);
            RemoveBallsFromLoadout(-delta);
        }
    }

    public bool AddBallToLoadout(BallDefinition def)
    {
        if (def == null || def.Prefab == null) return false;
        
        if (_ballLoadout.Count < maxBalls)
        {
            _ballLoadout.Add(def);
            _ampedUpBySlot.Add(false);
            _piggyBankExtraSellBySlot.Add(0);
            _piggyBankHandGrowthEligibleThisLevel.Add(true);
            return true;
        }
        return false;
    }

    public bool InsertBallIntoLoadout(int index, BallDefinition def)
    {
        if (def == null || def.Prefab == null) return false;
        if (index < 0 || index > _ballLoadout.Count) return false;

        if (_ballLoadout.Count < maxBalls)
        {
            _ballLoadout.Insert(index, def);
            _ampedUpBySlot.Insert(index, false);
            _piggyBankExtraSellBySlot.Insert(index, 0);
            _piggyBankHandGrowthEligibleThisLevel.Insert(index, true);
            return true;
        }
        return false;
    }

    public bool MoveBallInLoadout(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _ballLoadout.Count) return false;
        if (toIndex < 0 || toIndex >= _ballLoadout.Count) return false;
        if (fromIndex == toIndex) return true;

        BallDefinition def = _ballLoadout[fromIndex];
        _ballLoadout.RemoveAt(fromIndex);
        _ballLoadout.Insert(toIndex, def);

        EnsureRuntimeSlotParallelListsMatchLoadout();
        if (fromIndex >= 0 && fromIndex < _ampedUpBySlot.Count)
        {
            bool amped = _ampedUpBySlot[fromIndex];
            _ampedUpBySlot.RemoveAt(fromIndex);
            _ampedUpBySlot.Insert(toIndex, amped);
        }

        if (fromIndex >= 0 && fromIndex < _piggyBankExtraSellBySlot.Count)
        {
            int piggy = _piggyBankExtraSellBySlot[fromIndex];
            _piggyBankExtraSellBySlot.RemoveAt(fromIndex);
            _piggyBankExtraSellBySlot.Insert(toIndex, piggy);
        }

        if (fromIndex >= 0 && fromIndex < _piggyBankHandGrowthEligibleThisLevel.Count)
        {
            bool eligible = _piggyBankHandGrowthEligibleThisLevel[fromIndex];
            _piggyBankHandGrowthEligibleThisLevel.RemoveAt(fromIndex);
            _piggyBankHandGrowthEligibleThisLevel.Insert(toIndex, eligible);
        }

        return true;
    }

    public bool ReplaceBallInLoadout(int index, BallDefinition newDef)
    {
        if (newDef == null || newDef.Prefab == null) return false;
        if (index < 0 || index >= _ballLoadout.Count) return false;

        _ballLoadout[index] = newDef;
        EnsureRuntimeSlotParallelListsMatchLoadout();
        if (index >= 0 && index < _ampedUpBySlot.Count)
        {
            _ampedUpBySlot[index] = false;
        }

        if (index >= 0 && index < _piggyBankExtraSellBySlot.Count)
        {
            _piggyBankExtraSellBySlot[index] = 0;
        }

        if (index >= 0 && index < _piggyBankHandGrowthEligibleThisLevel.Count)
        {
            _piggyBankHandGrowthEligibleThisLevel[index] = true;
        }

        return true;
    }

    public bool TryRemoveBallFromLoadoutAt(int index, out BallDefinition removed)
    {
        removed = null;
        if (index < 0 || index >= _ballLoadout.Count) return false;

        EnsureRuntimeSlotParallelListsMatchLoadout();
        removed = _ballLoadout[index];
        _ballLoadout.RemoveAt(index);
        RemoveRuntimeSlotParallelDataAt(index);

        return true;
    }

    /// <summary>
    /// Automatically consumes the active ball (or the first available ball) when it drains without save.
    /// We do not care about strict slot tracking anymore — we just pop from the list.
    /// </summary>
    public void ConsumeActiveBallFromLoadout(int slotHint = -1)
    {
        if (_ballLoadout.Count == 0) return;

        EnsureRuntimeSlotParallelListsMatchLoadout();
        if (slotHint >= 0 && slotHint < _ballLoadout.Count)
        {
            _ballLoadout.RemoveAt(slotHint);
            RemoveRuntimeSlotParallelDataAt(slotHint);
            return;
        }

        // Just pop the top one if slot missing
        _ballLoadout.RemoveAt(0);
        RemoveRuntimeSlotParallelDataAt(0);
    }

    public bool SwapBallLoadoutSlots(int a, int b)
    {
        if (a < 0 || b < 0 || a >= _ballLoadout.Count || b >= _ballLoadout.Count) return false;
        if (a == b) return true;

        (_ballLoadout[a], _ballLoadout[b]) = (_ballLoadout[b], _ballLoadout[a]);
        EnsureRuntimeSlotParallelListsMatchLoadout();
        if (a < _ampedUpBySlot.Count
            && b < _ampedUpBySlot.Count)
        {
            (_ampedUpBySlot[a], _ampedUpBySlot[b]) =
                (_ampedUpBySlot[b], _ampedUpBySlot[a]);
        }

        if (a < _piggyBankExtraSellBySlot.Count
            && b < _piggyBankExtraSellBySlot.Count)
        {
            (_piggyBankExtraSellBySlot[a], _piggyBankExtraSellBySlot[b]) =
                (_piggyBankExtraSellBySlot[b], _piggyBankExtraSellBySlot[a]);
        }

        if (a < _piggyBankHandGrowthEligibleThisLevel.Count
            && b < _piggyBankHandGrowthEligibleThisLevel.Count)
        {
            (_piggyBankHandGrowthEligibleThisLevel[a], _piggyBankHandGrowthEligibleThisLevel[b]) =
                (_piggyBankHandGrowthEligibleThisLevel[b], _piggyBankHandGrowthEligibleThisLevel[a]);
        }

        return true;
    }

    /// <summary>
    /// When an <see cref="AmpUpBall"/> is used, marks the loadout slot immediately
    /// behind it as permanently amped up. Call before
    /// <see cref="ConsumeActiveBallFromLoadout"/> so the "behind" index is still valid.
    /// </summary>
    public void ApplyAmpUpToSlotBehind(int ampSlotHint)
    {
        if (_ballLoadout.Count < 2)
        {
            return;
        }

        int ampIndex = ampSlotHint >= 0 && ampSlotHint < _ballLoadout.Count
            ? ampSlotHint
            : 0;
        int behindIndex = ampIndex + 1;
        if (behindIndex >= _ballLoadout.Count)
        {
            return;
        }

        EnsureRuntimeSlotParallelListsMatchLoadout();
        _ampedUpBySlot[behindIndex] = true;
    }

    public bool GetAmpedUpForSlot(int loadoutSlotIndex)
    {
        EnsureRuntimeSlotParallelListsMatchLoadout();
        if (loadoutSlotIndex < 0
            || loadoutSlotIndex >= _ampedUpBySlot.Count)
        {
            return false;
        }

        return _ampedUpBySlot[loadoutSlotIndex];
    }

    /// <summary>
    /// Each level advanced, Piggy Bank slots that stayed off the table (never launched from
    /// hand this level) gain extra sell payout (see ball rules).
    /// </summary>
    public void ApplyPiggyBankRoundSellGrowth()
    {
        EnsureRuntimeSlotParallelListsMatchLoadout();
        const int delta = 3;
        for (int i = 0; i < _ballLoadout.Count; i++)
        {
            BallDefinition def = _ballLoadout[i];
            if (def == null || !IsPiggyBankDefinition(def))
            {
                continue;
            }

            if (i < _piggyBankHandGrowthEligibleThisLevel.Count
                && _piggyBankHandGrowthEligibleThisLevel[i])
            {
                _piggyBankExtraSellBySlot[i] += delta;
            }
        }
    }

    /// <summary>
    /// Call at the start of each playable round segment (e.g. <see cref="GameRulesManager.StartRound"/>).
    /// Piggy Banks may earn the next level-up +$3 only if they are not launched from hand before then.
    /// </summary>
    public void ResetPiggyBankHandGrowthEligibilityForNewLevel()
    {
        EnsureRuntimeSlotParallelListsMatchLoadout();
        for (int i = 0; i < _ballLoadout.Count; i++)
        {
            if (IsPiggyBankDefinition(_ballLoadout[i]))
            {
                _piggyBankHandGrowthEligibleThisLevel[i] = true;
            }
        }
    }

    /// <summary>
    /// When a ball is promoted from the hand queue to the table, its loadout slot forfeits
    /// this level's Piggy growth if it is a Piggy Bank.
    /// </summary>
    public void NotifyPiggyBankLaunchedFromHand(int loadoutSlotIndex)
    {
        EnsureRuntimeSlotParallelListsMatchLoadout();
        if (loadoutSlotIndex < 0 || loadoutSlotIndex >= _ballLoadout.Count)
        {
            return;
        }

        if (!IsPiggyBankDefinition(_ballLoadout[loadoutSlotIndex]))
        {
            return;
        }

        _piggyBankHandGrowthEligibleThisLevel[loadoutSlotIndex] = false;
    }

    /// <summary>
    /// Shop sell formula plus stored Piggy growth for that loadout slot.
    /// </summary>
    public int GetPiggyBankSellPayoutForSlot(int slotHint)
    {
        EnsureRuntimeSlotParallelListsMatchLoadout();
        int slot = slotHint >= 0 ? slotHint : 0;
        if (slot < 0 || slot >= _ballLoadout.Count)
        {
            return 0;
        }

        BallDefinition def = _ballLoadout[slot];
        if (def == null || !IsPiggyBankDefinition(def))
        {
            return 0;
        }

        int baseSell = (Mathf.Max(0, def.Price) + 1) / 2;
        int extra = slot < _piggyBankExtraSellBySlot.Count
            ? _piggyBankExtraSellBySlot[slot]
            : 0;
        return baseSell + extra;
    }

    public void InitializeForNewRun()
    {
        _ballLoadout.Clear();
        _ampedUpBySlot.Clear();
        _piggyBankExtraSellBySlot.Clear();
        _piggyBankHandGrowthEligibleThisLevel.Clear();

        PlayerShipDefinition activeShip = null;
        if (GameSession.Instance != null && GameSession.Instance.ActiveShip != null)
        {
            activeShip = GameSession.Instance.ActiveShip;
        }

        if (activeShip != null)
        {
            maxBalls = Mathf.Max(1, activeShip.startingMaxBalls);
            
            if (activeShip.startingHand != null && activeShip.startingHand.Count > 0)
            {
                for (int i = 0; i < activeShip.startingHand.Count && i < maxBalls; i++)
                {
                    BallDefinition def = activeShip.startingHand[i];
                    if (def != null && def.Prefab != null)
                    {
                        _ballLoadout.Add(def);
                        _ampedUpBySlot.Add(false);
                        _piggyBankExtraSellBySlot.Add(0);
                        _piggyBankHandGrowthEligibleThisLevel.Add(true);
                    }
                }
            }
        }
        else
        {
            maxBalls = Mathf.Max(1, startingMaxBalls);

            if (startingBallLoadoutDefinitions != null && startingBallLoadoutDefinitions.Count > 0)
            {
                for (int i = 0; i < startingBallLoadoutDefinitions.Count && i < maxBalls; i++)
                {
                    BallDefinition def = startingBallLoadoutDefinitions[i];
                    if (def != null && def.Prefab != null)
                    {
                        _ballLoadout.Add(def);
                        _ampedUpBySlot.Add(false);
                        _piggyBankExtraSellBySlot.Add(0);
                        _piggyBankHandGrowthEligibleThisLevel.Add(true);
                    }
                }
            }
            else if (startingBallLoadout != null && startingBallLoadout.Count > 0)
            {
                for (int i = 0; i < startingBallLoadout.Count && i < maxBalls; i++)
                {
                    GameObject prefab = startingBallLoadout[i];
                    if (prefab == null) continue;

                    BallDefinition def = BallDefinitionUtilities.TryGetDefinitionFromPrefab(prefab);
                    if (def == null)
                    {
                        def = BallDefinition.CreateRuntime(
                            runtimeId: prefab.name,
                            runtimeDisplayName: prefab.name,
                            runtimeDescription: "",
                            runtimeRarity: BallRarity.Common,
                            runtimeElementType: ElementType.None,
                            runtimeIcon: BallDefinitionUtilities.TryGetPrefabSpriteIcon(prefab),
                            runtimePrefab: prefab,
                            runtimePrice: 0);
                    }
                    _ballLoadout.Add(def);
                    _ampedUpBySlot.Add(false);
                    _piggyBankExtraSellBySlot.Add(0);
                    _piggyBankHandGrowthEligibleThisLevel.Add(true);
                }
            }
        }

        // Pad out to full capacity with standard balls
        int deficit = maxBalls - _ballLoadout.Count;
        if (deficit > 0)
        {
            TryAddFallbackBallsToLoadout(deficit);
        }
    }

    private void TryAddFallbackBallsToLoadout(int count)
    {
        if (count <= 0) return;

        if (!ServiceLocator.TryGet<BallSpawner>(out var spawner)) return;

        GameObject fallbackPrefab = spawner.DefaultBallPrefab;
        if (fallbackPrefab == null) return;

        BallDefinition def = BallDefinitionUtilities.TryGetDefinitionFromPrefab(fallbackPrefab);
        if (def == null)
        {
            def = BallDefinition.CreateRuntime(
                runtimeId: fallbackPrefab.name,
                runtimeDisplayName: fallbackPrefab.name,
                runtimeDescription: "",
                runtimeRarity: BallRarity.Common,
                runtimeElementType: ElementType.None,
                runtimeIcon: BallDefinitionUtilities.TryGetPrefabSpriteIcon(fallbackPrefab),
                runtimePrefab: fallbackPrefab,
                runtimePrice: 0);
        }

        for (int i = 0; i < count; i++)
        {
            if (_ballLoadout.Count < maxBalls)
            {
                _ballLoadout.Add(def);
                _ampedUpBySlot.Add(false);
                _piggyBankExtraSellBySlot.Add(0);
                _piggyBankHandGrowthEligibleThisLevel.Add(true);
            }
        }
    }

    private void RemoveBallsFromLoadout(int count)
    {
        if (count <= 0) return;
        
        int toRemove = Mathf.Min(count, _ballLoadout.Count);
        for (int i = 0; i < toRemove; i++)
        {
            _ballLoadout.RemoveAt(_ballLoadout.Count - 1);
            if (_ampedUpBySlot.Count > 0)
            {
                _ampedUpBySlot.RemoveAt(
                    _ampedUpBySlot.Count - 1);
            }

            if (_piggyBankExtraSellBySlot.Count > 0)
            {
                _piggyBankExtraSellBySlot.RemoveAt(
                    _piggyBankExtraSellBySlot.Count - 1);
            }

            if (_piggyBankHandGrowthEligibleThisLevel.Count > 0)
            {
                _piggyBankHandGrowthEligibleThisLevel.RemoveAt(
                    _piggyBankHandGrowthEligibleThisLevel.Count - 1);
            }
        }
    }

    private static bool IsPiggyBankDefinition(BallDefinition def)
    {
        return def != null && def.Id == PiggyBankBall.DefinitionId;
    }

    private void EnsureRuntimeSlotParallelListsMatchLoadout()
    {
        while (_ampedUpBySlot.Count < _ballLoadout.Count)
        {
            _ampedUpBySlot.Add(false);
        }

        while (_piggyBankExtraSellBySlot.Count < _ballLoadout.Count)
        {
            _piggyBankExtraSellBySlot.Add(0);
        }

        while (_piggyBankHandGrowthEligibleThisLevel.Count < _ballLoadout.Count)
        {
            _piggyBankHandGrowthEligibleThisLevel.Add(true);
        }

        while (_ampedUpBySlot.Count > _ballLoadout.Count
               && _ampedUpBySlot.Count > 0)
        {
            _ampedUpBySlot.RemoveAt(
                _ampedUpBySlot.Count - 1);
        }

        while (_piggyBankExtraSellBySlot.Count > _ballLoadout.Count
               && _piggyBankExtraSellBySlot.Count > 0)
        {
            _piggyBankExtraSellBySlot.RemoveAt(
                _piggyBankExtraSellBySlot.Count - 1);
        }

        while (_piggyBankHandGrowthEligibleThisLevel.Count > _ballLoadout.Count
               && _piggyBankHandGrowthEligibleThisLevel.Count > 0)
        {
            _piggyBankHandGrowthEligibleThisLevel.RemoveAt(
                _piggyBankHandGrowthEligibleThisLevel.Count - 1);
        }
    }

    private void RemoveRuntimeSlotParallelDataAt(int index)
    {
        if (index >= 0 && index < _ampedUpBySlot.Count)
        {
            _ampedUpBySlot.RemoveAt(index);
        }

        if (index >= 0 && index < _piggyBankExtraSellBySlot.Count)
        {
            _piggyBankExtraSellBySlot.RemoveAt(index);
        }

        if (index >= 0 && index < _piggyBankHandGrowthEligibleThisLevel.Count)
        {
            _piggyBankHandGrowthEligibleThisLevel.RemoveAt(index);
        }
    }
}
