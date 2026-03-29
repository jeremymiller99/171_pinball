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
            return true;
        }
        return false;
    }

    public bool ReplaceBallInLoadout(int index, BallDefinition newDef)
    {
        if (newDef == null || newDef.Prefab == null) return false;
        if (index < 0 || index >= _ballLoadout.Count) return false;

        _ballLoadout[index] = newDef;
        return true;
    }

    public bool TryRemoveBallFromLoadoutAt(int index, out BallDefinition removed)
    {
        removed = null;
        if (index < 0 || index >= _ballLoadout.Count) return false;

        removed = _ballLoadout[index];
        _ballLoadout.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Automatically consumes the active ball (or the first available ball) when it drains without save.
    /// We do not care about strict slot tracking anymore — we just pop from the list.
    /// </summary>
    public void ConsumeActiveBallFromLoadout(int slotHint = -1)
    {
        if (_ballLoadout.Count == 0) return;

        if (slotHint >= 0 && slotHint < _ballLoadout.Count)
        {
            _ballLoadout.RemoveAt(slotHint);
            return;
        }

        // Just pop the top one if slot missing
        _ballLoadout.RemoveAt(0);
    }

    public bool SwapBallLoadoutSlots(int a, int b)
    {
        if (a < 0 || b < 0 || a >= _ballLoadout.Count || b >= _ballLoadout.Count) return false;
        if (a == b) return true;

        (_ballLoadout[a], _ballLoadout[b]) = (_ballLoadout[b], _ballLoadout[a]);
        return true;
    }

    public void InitializeForNewRun()
    {
        _ballLoadout.Clear();
        maxBalls = Mathf.Max(1, startingMaxBalls);

        if (startingBallLoadoutDefinitions != null && startingBallLoadoutDefinitions.Count > 0)
        {
            for (int i = 0; i < startingBallLoadoutDefinitions.Count && i < maxBalls; i++)
            {
                BallDefinition def = startingBallLoadoutDefinitions[i];
                if (def != null && def.Prefab != null) _ballLoadout.Add(def);
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
                        runtimeIcon: BallDefinitionUtilities.TryGetPrefabSpriteIcon(prefab),
                        runtimePrefab: prefab,
                        runtimePrice: 0);
                }
                _ballLoadout.Add(def);
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
                runtimeIcon: BallDefinitionUtilities.TryGetPrefabSpriteIcon(fallbackPrefab),
                runtimePrefab: fallbackPrefab,
                runtimePrice: 0);
        }

        for (int i = 0; i < count; i++)
        {
            if (_ballLoadout.Count < maxBalls)
                _ballLoadout.Add(def);
        }
    }

    private void RemoveBallsFromLoadout(int count)
    {
        if (count <= 0) return;
        
        int toRemove = Mathf.Min(count, _ballLoadout.Count);
        for (int i = 0; i < toRemove; i++)
        {
            _ballLoadout.RemoveAt(_ballLoadout.Count - 1);
        }
    }
}
