using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RoundModifierController : MonoBehaviour
{
    [Header("Level Modifiers (runtime)")]
    [Min(1)]
    [SerializeField] private int guaranteedBagSizeFallback = 7;

    [Header("Default Modifier Pools (when no challenge)")]
    [Tooltip("Modifier pools used for Quick Run / when no challenge is active.")]
    [SerializeField] private RoundModifierPool defaultAngelPool;
    [SerializeField] private RoundModifierPool defaultDevilPool;

    private const float defaultQuickRunAngelChance = 0.2f;
    private const float defaultQuickRunDevilChance = 0.2f;
    private const float defaultQuickRunNormalChance = 0.6f;

    [Header("Quick Run round-type distribution")]
    [Tooltip("Percent chances for what kind of round it is when no challenge is active.")]
    [SerializeField] private float quickRunAngelChance = defaultQuickRunAngelChance;
    [SerializeField] private float quickRunDevilChance = defaultQuickRunDevilChance;
    [SerializeField] private float quickRunNormalChance = defaultQuickRunNormalChance;

    [Header("Round Type Preview (generation)")]
    [Min(0)]
    [SerializeField] private int previewLookaheadRounds = 2;

    [Header("Round Type Preview (debug)")]
    [SerializeField] private int generatedRoundWindowStartIndex;
    [SerializeField] private int generatedRoundWindowEndIndex;

    [Serializable]
    public sealed class GeneratedRound
    {
        public RoundData roundData;
        public List<RoundModifierDefinition> unluckyDayActiveModifiers;
    }

    private readonly List<GeneratedRound> _generatedRoundWindow = new List<GeneratedRound>();
    private System.Random _levelModifierRng;
    private readonly List<RoundType> _guaranteedTypeBag = new List<RoundType>();
    private int _guaranteedTypeBagPos;

    private RoundModifierDefinition _activeModifier;
    private RoundData _currentRoundData;
    private float _effectiveGoalModifierForRound;
    private List<RoundModifierDefinition> _unluckyDayActiveModifiers;
    private int _flipperUsesRemaining = -1;

    public RoundModifierDefinition ActiveModifier => _activeModifier;
    public RoundData CurrentRoundData => _currentRoundData;
    public float EffectiveGoalModifierForRound => _effectiveGoalModifierForRound;
    public List<RoundModifierDefinition> UnluckyDayActiveModifiers => _unluckyDayActiveModifiers;
    public int RemainingFlipperUses => _flipperUsesRemaining;
    public bool HasFlipperLimit => _flipperUsesRemaining >= 0;

    /// <summary>
    /// Returns the score multiplier from the active modifier and the player's active ship (1.0 if no modifier).
    /// </summary>
    public float GetModifierScoreMultiplier()
    {
        float m = _activeModifier?.scoreMultiplier ?? 1f;
        if (GameSession.Instance != null && GameSession.Instance.ActiveShip != null)
            m *= GameSession.Instance.ActiveShip.scoreMultiplier;
        return m;
    }

    /// <summary>
    /// Returns the coin multiplier from the active modifier and the player's active ship (1.0 if no modifier).
    /// </summary>
    public float GetModifierCoinMultiplier()
    {
        float m = _activeModifier?.coinMultiplier ?? 1f;
        if (GameSession.Instance != null && GameSession.Instance.ActiveShip != null)
            m *= GameSession.Instance.ActiveShip.coinMultiplier;
        return m;
    }

    /// <summary>
    /// Returns true if the multiplier is disabled by the active modifier.
    /// </summary>
    public bool IsMultiplierDisabled() => _activeModifier?.disableMultiplier ?? false;

    private void Awake()
    {
        ServiceLocator.Register<RoundModifierController>(this);
        NormalizeQuickRunChances();
    }

    private void OnDisable()
    {
        ServiceLocator.Unregister<RoundModifierController>();
    }

    private void OnValidate()
    {
        NormalizeQuickRunChances();
    }

    public void InitLevelModifierRolling()
    {
        var session = GameSession.Instance;
        int seed = session != null ? session.Seed : Environment.TickCount;
        _levelModifierRng = new System.Random(seed);
        _guaranteedTypeBag.Clear();
        _guaranteedTypeBagPos = 0;
    }

    public void ResetAndPrimeRoundWindow(int roundIndex)
    {
        _generatedRoundWindow.Clear();
        generatedRoundWindowStartIndex = 0;
        generatedRoundWindowEndIndex = -1;
        EnsureGeneratedThrough(Mathf.Max(0, roundIndex + previewLookaheadRounds));
    }

    public void ApplyCurrentRoundFromWindow(int roundIndex, Action<int> applyBallModifierCallback)
    {
        TrimGeneratedBefore(roundIndex);
        EnsureGeneratedThrough(Mathf.Max(0, roundIndex + previewLookaheadRounds));

        if (!TryGetGeneratedRound(roundIndex, out GeneratedRound generatedRound))
        {
            generatedRound = GenerateRoundAtIndex(roundIndex);
        }

        ApplyGeneratedRound(generatedRound, roundIndex, applyBallModifierCallback);
    }

    public bool TryGetRoundType(int absoluteRoundIndex, bool runActive, out RoundType type)
    {
        type = RoundType.Normal;
        if (!runActive || absoluteRoundIndex < 0) return false;

        EnsureGeneratedThrough(absoluteRoundIndex);

        if (!TryGetGeneratedRound(absoluteRoundIndex, out GeneratedRound generatedRound)) return false;
        if (generatedRound == null || generatedRound.roundData == null) return false;

        type = generatedRound.roundData.type;
        return true;
    }

    public bool TryConsumeFlipperUse()
    {
        if (_flipperUsesRemaining < 0) return true;
        
        if (_flipperUsesRemaining > 0)
        {
            _flipperUsesRemaining--;
            return true;
        }
        
        return false; // Flipper limit exceeded
    }

    private void TrimGeneratedBefore(int absoluteRoundIndex)
    {
        int toRemove = absoluteRoundIndex - generatedRoundWindowStartIndex;
        if (toRemove <= 0) return;

        toRemove = Mathf.Clamp(toRemove, 0, _generatedRoundWindow.Count);
        if (toRemove <= 0) return;

        _generatedRoundWindow.RemoveRange(0, toRemove);
        generatedRoundWindowStartIndex = Mathf.Max(0, generatedRoundWindowStartIndex + toRemove);

        if (_generatedRoundWindow.Count == 0)
            generatedRoundWindowEndIndex = generatedRoundWindowStartIndex - 1;
        else
            generatedRoundWindowEndIndex = generatedRoundWindowStartIndex + _generatedRoundWindow.Count - 1;
    }

    private void EnsureGeneratedThrough(int inclusiveRoundIndex)
    {
        if (inclusiveRoundIndex < 0) return;

        if (_levelModifierRng == null)
            _levelModifierRng = new System.Random(Environment.TickCount);

        if (_generatedRoundWindow.Count == 0)
        {
            generatedRoundWindowStartIndex = Mathf.Max(0, generatedRoundWindowStartIndex);
            generatedRoundWindowEndIndex = generatedRoundWindowStartIndex - 1;
        }

        int nextToGenerate = generatedRoundWindowEndIndex + 1;
        if (nextToGenerate < generatedRoundWindowStartIndex)
            nextToGenerate = generatedRoundWindowStartIndex;

        for (int i = nextToGenerate; i <= inclusiveRoundIndex; i++)
        {
            GeneratedRound generatedRound = GenerateRoundAtIndex(i);
            _generatedRoundWindow.Add(generatedRound);
            generatedRoundWindowEndIndex = i;
        }
    }

    private bool TryGetGeneratedRound(int absoluteRoundIndex, out GeneratedRound generatedRound)
    {
        generatedRound = null;
        if (absoluteRoundIndex < generatedRoundWindowStartIndex) return false;

        int localIndex = absoluteRoundIndex - generatedRoundWindowStartIndex;
        if (localIndex < 0 || localIndex >= _generatedRoundWindow.Count) return false;

        generatedRound = _generatedRoundWindow[localIndex];
        return generatedRound != null;
    }

    private GeneratedRound GenerateRoundAtIndex(int absoluteRoundIndex)
    {
        RoundType type = RoundType.Normal;
        RoundModifierDefinition modifier = null;

        var session = GameSession.Instance;
        ChallengeModeDefinition challenge = session != null ? session.ActiveChallenge : null;

        if (challenge != null && challenge.HasModifierPools)
        {
            if (absoluteRoundIndex == 0 && challenge.distributionMode == RoundDistributionMode.Guaranteed)
            {
                EnsureGuaranteedTypeBag(challenge);
                EnsureCurrentGuaranteedTypeIsNormal();
            }

            type = RollModifierType(challenge);
            modifier = RollModifierFromPool(challenge, type);
            if (modifier == null && type == RoundType.Angel && challenge.devilPool != null && challenge.devilPool.ValidCount > 0)
                modifier = challenge.devilPool.GetRandomModifier(_levelModifierRng);
            if (modifier == null && type == RoundType.Devil && challenge.angelPool != null && challenge.angelPool.ValidCount > 0)
                modifier = challenge.angelPool.GetRandomModifier(_levelModifierRng);
        }
        else
        {
            RoundModifierPool angelPool = defaultAngelPool;
            RoundModifierPool devilPool = defaultDevilPool;
            bool hasAngel = angelPool != null && angelPool.ValidCount > 0;
            bool hasDevil = devilPool != null && devilPool.ValidCount > 0;

            if (hasAngel || hasDevil)
            {
                type = RollQuickRunType(hasAngel, hasDevil);
                if (type == RoundType.Angel && hasAngel)
                    modifier = angelPool.GetRandomModifier(_levelModifierRng);
                else if (type == RoundType.Devil && hasDevil)
                    modifier = devilPool.GetRandomModifier(_levelModifierRng);
            }
        }

        if (absoluteRoundIndex == 0)
        {
            type = RoundType.Normal;
            modifier = null;
        }

        var generated = new GeneratedRound
        {
            roundData = new RoundData(absoluteRoundIndex, type, modifier),
            unluckyDayActiveModifiers = null
        };

        if (modifier != null && modifier.applyTwoRandomDevilModifiers)
        {
            RoundModifierPool devilPool = (session != null && session.ActiveChallenge != null && session.ActiveChallenge.devilPool != null)
                ? session.ActiveChallenge.devilPool : defaultDevilPool;

            if (devilPool != null)
                generated.unluckyDayActiveModifiers = devilPool.GetTwoRandomModifiersExcluding(_levelModifierRng, modifier);
        }

        return generated;
    }

    private void ApplyGeneratedRound(GeneratedRound generatedRound, int roundIndex, Action<int> applyBallModifierCallback)
    {
        _activeModifier = null;

        // Reset modifier multipliers to 1 when the previous modifier ends
        if (ServiceLocator.TryGet<ScoreManager>(out var scoreManager))
        {
            scoreManager.ResetModifierMultipliers();
        }

        RoundData data = generatedRound != null ? generatedRound.roundData : null;
        if (data == null) data = new RoundData(roundIndex, RoundType.Normal, null);

        _activeModifier = data.modifier;
        _currentRoundData = data;
        _flipperUsesRemaining = (_activeModifier != null && _activeModifier.flipperUseLimit > 0)
            ? _activeModifier.flipperUseLimit : -1;

        {
            float musicState = 0f;
            if (_currentRoundData.type == RoundType.Angel) musicState = 1f;
            else if (_currentRoundData.type == RoundType.Devil) musicState = -1f;
            ServiceLocator.Get<AudioManager>()?.SetMusicState(musicState);
        }

        _effectiveGoalModifierForRound = 0f;
        _unluckyDayActiveModifiers = generatedRound?.unluckyDayActiveModifiers;

        float shipScoreMult = 1f;
        float shipCoinMult = 1f;
        float shipMultMult = 1f;
        if (GameSession.Instance != null && GameSession.Instance.ActiveShip != null)
        {
            shipScoreMult = Mathf.Max(0f, GameSession.Instance.ActiveShip.scoreMultiplier);
            shipCoinMult = Mathf.Max(0f, GameSession.Instance.ActiveShip.coinMultiplier);
            shipMultMult = Mathf.Max(0f, GameSession.Instance.ActiveShip.multMultiplier);
        }

        if (scoreManager != null && _activeModifier != null)
        {
            if (_activeModifier.applyTwoRandomDevilModifiers)
            {
                if (_unluckyDayActiveModifiers != null)
                {
                    float scoreMult = 1f;
                    float goalMod = 0f;
                    float coinMult = 1f;
                    bool disableMult = false;
                    int ballMod = 0;
                    float timeScaleMult = 1f;

                    foreach (var m in _unluckyDayActiveModifiers)
                    {
                        if (m == null) continue;
                        scoreMult *= m.scoreMultiplier;
                        goalMod += m.goalModifier;
                        coinMult *= m.coinMultiplier;
                        disableMult = disableMult || m.disableMultiplier;
                        ballMod += m.ballModifier;
                        timeScaleMult *= m.timeScaleMultiplier;
                    }

                    scoreManager.SetModifierMultipliers(
                        scoreMult * shipScoreMult,
                        (disableMult ? 0.5f : 1f) * shipMultMult,
                        coinMult * shipCoinMult,
                        timeScaleMult);
                    _effectiveGoalModifierForRound = goalMod;

                    applyBallModifierCallback?.Invoke(ballMod);
                }
                else
                {
                    scoreManager.SetModifierMultipliers(
                        _activeModifier.scoreMultiplier * shipScoreMult,
                        (_activeModifier.disableMultiplier ? 0.5f : 1f) * shipMultMult,
                        _activeModifier.coinMultiplier * shipCoinMult,
                        1f);
                    applyBallModifierCallback?.Invoke(_activeModifier.ballModifier);
                }
            }
            else
            {
                scoreManager.SetModifierMultipliers(
                    _activeModifier.scoreMultiplier * shipScoreMult,
                    (_activeModifier.disableMultiplier ? 0.5f : 1f) * shipMultMult,
                    _activeModifier.coinMultiplier * shipCoinMult,
                    _activeModifier.timeScaleMultiplier);
                applyBallModifierCallback?.Invoke(_activeModifier.ballModifier);
            }
        }
        else
        {
            if (scoreManager != null)
            {
                scoreManager.SetModifierMultipliers(
                    shipScoreMult, shipMultMult, shipCoinMult, 1f);
            }
            applyBallModifierCallback?.Invoke(0);
        }
    }

    private RoundType RollQuickRunType(bool hasAngelPool, bool hasDevilPool)
    {
        if (_levelModifierRng == null) _levelModifierRng = new System.Random(Environment.TickCount);

        float angelChance = hasAngelPool ? Mathf.Max(0f, quickRunAngelChance) : 0f;
        float devilChance = hasDevilPool ? Mathf.Max(0f, quickRunDevilChance) : 0f;
        float normalChance = Mathf.Max(0f, quickRunNormalChance);

        float sum = angelChance + devilChance + normalChance;
        if (sum <= 0f) return RoundType.Normal;

        angelChance /= sum;
        devilChance /= sum;

        double roll = _levelModifierRng.NextDouble();
        if (roll < angelChance) return RoundType.Angel;
        if (roll < (angelChance + devilChance)) return RoundType.Devil;
        return RoundType.Normal;
    }

    private void NormalizeQuickRunChances()
    {
        quickRunAngelChance = Mathf.Max(0f, quickRunAngelChance);
        quickRunDevilChance = Mathf.Max(0f, quickRunDevilChance);
        quickRunNormalChance = Mathf.Max(0f, quickRunNormalChance);

        float sum = quickRunAngelChance + quickRunDevilChance + quickRunNormalChance;
        if (sum <= 0f)
        {
            quickRunAngelChance = defaultQuickRunAngelChance;
            quickRunDevilChance = defaultQuickRunDevilChance;
            quickRunNormalChance = defaultQuickRunNormalChance;
            return;
        }

        quickRunAngelChance /= sum;
        quickRunDevilChance /= sum;
        quickRunNormalChance /= sum;
    }

    private RoundType RollModifierType(ChallengeModeDefinition challenge)
    {
        if (challenge == null) return RoundType.Normal;

        if (challenge.distributionMode == RoundDistributionMode.Guaranteed)
        {
            EnsureGuaranteedTypeBag(challenge);
            if (_guaranteedTypeBag.Count == 0) return RoundType.Normal;

            int i = Mathf.Clamp(_guaranteedTypeBagPos, 0, _guaranteedTypeBag.Count - 1);
            RoundType t = _guaranteedTypeBag[i];
            _guaranteedTypeBagPos = Mathf.Max(0, _guaranteedTypeBagPos + 1);
            return t;
        }

        if (_levelModifierRng == null) _levelModifierRng = new System.Random(Environment.TickCount);

        float angelChance = Mathf.Clamp01(challenge.angelChance);
        float devilChance = Mathf.Clamp01(challenge.devilChance);
        double roll = _levelModifierRng.NextDouble();

        if (roll < angelChance) return RoundType.Angel;
        if (roll < (angelChance + devilChance)) return RoundType.Devil;
        return RoundType.Normal;
    }

    private RoundModifierDefinition RollModifierFromPool(ChallengeModeDefinition challenge, RoundType type)
    {
        if (challenge == null) return null;
        if (_levelModifierRng == null) _levelModifierRng = new System.Random(Environment.TickCount);

        switch (type)
        {
            case RoundType.Angel: return challenge.angelPool?.GetRandomModifier(_levelModifierRng);
            case RoundType.Devil: return challenge.devilPool?.GetRandomModifier(_levelModifierRng);
            default: return null;
        }
    }

    private void EnsureGuaranteedTypeBag(ChallengeModeDefinition challenge)
    {
        if (challenge == null || _guaranteedTypeBagPos < _guaranteedTypeBag.Count) return;
        if (_levelModifierRng == null) _levelModifierRng = new System.Random(Environment.TickCount);

        _guaranteedTypeBag.Clear();
        _guaranteedTypeBagPos = 0;

        int bagSize = Mathf.Max(1, challenge.totalRounds > 0 ? challenge.totalRounds : guaranteedBagSizeFallback);
        int angels = Mathf.Clamp(challenge.guaranteedAngels, 0, bagSize);
        int devils = Mathf.Clamp(challenge.guaranteedDevils, 0, bagSize - angels);

        for (int i = 0; i < bagSize; i++) _guaranteedTypeBag.Add(RoundType.Normal);
        for (int i = 0; i < angels; i++) _guaranteedTypeBag[i] = RoundType.Angel;
        for (int i = 0; i < devils; i++) _guaranteedTypeBag[angels + i] = RoundType.Devil;

        for (int i = _guaranteedTypeBag.Count - 1; i > 0; i--)
        {
            int j = _levelModifierRng.Next(i + 1);
            (_guaranteedTypeBag[i], _guaranteedTypeBag[j]) = (_guaranteedTypeBag[j], _guaranteedTypeBag[i]);
        }
    }

    private void EnsureCurrentGuaranteedTypeIsNormal()
    {
        if (_guaranteedTypeBag == null || _guaranteedTypeBag.Count == 0) return;

        int i = Mathf.Clamp(_guaranteedTypeBagPos, 0, _guaranteedTypeBag.Count - 1);
        if (_guaranteedTypeBag[i] == RoundType.Normal) return;

        int swapIndex = _guaranteedTypeBag.FindIndex(i + 1, t => t == RoundType.Normal);
        if (swapIndex >= 0)
        {
            (_guaranteedTypeBag[i], _guaranteedTypeBag[swapIndex]) = (_guaranteedTypeBag[swapIndex], _guaranteedTypeBag[i]);
            return;
        }
        _guaranteedTypeBag[i] = RoundType.Normal;
    }
}
