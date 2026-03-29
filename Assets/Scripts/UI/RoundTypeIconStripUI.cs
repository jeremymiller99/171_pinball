// Generated with Cursor (GPT-5.2) by OpenAI assistant for jjmil on 2026-02-24.
using System.Collections.Generic;
using UnityEngine;

public sealed class RoundTypeIconStripUI : MonoBehaviour
{
    [Header("Icon Prefabs")]
    [SerializeField] private GameObject normalIconPrefab;
    [SerializeField] private GameObject angelIconPrefab;
    [SerializeField] private GameObject devilIconPrefab;

    [Header("Layout")]
    [Tooltip("Optional. If not set, uses this transform.")]
    [SerializeField] private Transform iconContainer;

    [Min(0)]
    [SerializeField] private int lookaheadRounds = 2;

    [Tooltip("Scale multiplier for the current (left-most) icon.")]
    [Min(0f)]
    [SerializeField] private float currentIconScaleMultiplier = 1.25f;

    [Tooltip("Scale multiplier for upcoming icons.")]
    [Min(0f)]
    [SerializeField] private float futureIconScaleMultiplier = 1f;

    [Header("Behavior")]
    [Tooltip("If true, disables any pre-existing placeholder children at runtime.")]
    [SerializeField] private bool disablePlaceholdersAtRuntime = true;

    [Header("Runtime (debug)")]
    [SerializeField] private GameRulesManager rulesManager;

    private sealed class Slot
    {
        public GameObject normal;
        public GameObject angel;
        public GameObject devil;
        public Vector3 normalBaseScale;
        public Vector3 angelBaseScale;
        public Vector3 devilBaseScale;
    }

    private readonly List<Slot> _slots = new List<Slot>();
    private bool _subscribed;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveRulesManager();
        EnsureBuilt();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveRulesManager()
    {
        if (rulesManager != null)
        {
            return;
        }

        rulesManager = ServiceLocator.Get<GameRulesManager>();
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        if (rulesManager == null)
        {
            return;
        }

        rulesManager.LevelChanged -= OnLevelChanged;
        rulesManager.LevelChanged += OnLevelChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        if (rulesManager != null)
        {
            rulesManager.LevelChanged -= OnLevelChanged;
        }

        _subscribed = false;
    }

    private void OnLevelChanged()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveRulesManager();
        EnsureBuilt();

        if (_slots.Count == 0)
        {
            return;
        }

        int currentRoundIndex = rulesManager != null ? rulesManager.RoundIndex : 0;
        int count = Mathf.Max(1, lookaheadRounds + 1);
        count = Mathf.Min(count, _slots.Count);

        for (int i = 0; i < count; i++)
        {
            int absoluteRoundIndex = Mathf.Max(0, currentRoundIndex + i);

            RoundType type = RoundType.Normal;
            if (rulesManager != null)
            {
                rulesManager.TryGetRoundType(absoluteRoundIndex, out type);
            }

            float scaleMul = i == 0 ? currentIconScaleMultiplier : futureIconScaleMultiplier;
            ApplyToSlot(_slots[i], type, Mathf.Max(0f, scaleMul));
        }
    }

    private void EnsureBuilt()
    {
        if (_slots.Count > 0)
        {
            return;
        }

        Transform container = iconContainer != null ? iconContainer : transform;
        if (container == null)
        {
            return;
        }

        if (disablePlaceholdersAtRuntime)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                if (child != null)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        int count = Mathf.Max(1, lookaheadRounds + 1);
        for (int i = 0; i < count; i++)
        {
            GameObject normal = normalIconPrefab != null ? Instantiate(normalIconPrefab, container) : null;
            GameObject angel = angelIconPrefab != null ? Instantiate(angelIconPrefab, container) : null;
            GameObject devil = devilIconPrefab != null ? Instantiate(devilIconPrefab, container) : null;

            if (normal != null) normal.name = $"RoundType_{i}_Normal";
            if (angel != null) angel.name = $"RoundType_{i}_Angel";
            if (devil != null) devil.name = $"RoundType_{i}_Devil";

            var slot = new Slot
            {
                normal = normal,
                angel = angel,
                devil = devil,
                normalBaseScale = normal != null ? normal.transform.localScale : Vector3.one,
                angelBaseScale = angel != null ? angel.transform.localScale : Vector3.one,
                devilBaseScale = devil != null ? devil.transform.localScale : Vector3.one
            };

            if (slot.normal != null) slot.normal.SetActive(false);
            if (slot.angel != null) slot.angel.SetActive(false);
            if (slot.devil != null) slot.devil.SetActive(false);

            _slots.Add(slot);
        }
    }

    private static void ApplyToSlot(Slot slot, RoundType type, float scaleMultiplier)
    {
        if (slot == null)
        {
            return;
        }

        if (slot.normal != null)
        {
            slot.normal.transform.localScale = slot.normalBaseScale;
            slot.normal.SetActive(false);
        }

        if (slot.angel != null)
        {
            slot.angel.transform.localScale = slot.angelBaseScale;
            slot.angel.SetActive(false);
        }

        if (slot.devil != null)
        {
            slot.devil.transform.localScale = slot.devilBaseScale;
            slot.devil.SetActive(false);
        }

        GameObject active = null;
        Vector3 baseScale = Vector3.one;
        switch (type)
        {
            case RoundType.Angel:
                active = slot.angel != null ? slot.angel : slot.normal;
                baseScale = slot.angel != null ? slot.angelBaseScale : slot.normalBaseScale;
                break;
            case RoundType.Devil:
                active = slot.devil != null ? slot.devil : slot.normal;
                baseScale = slot.devil != null ? slot.devilBaseScale : slot.normalBaseScale;
                break;
            default:
                active = slot.normal;
                baseScale = slot.normalBaseScale;
                break;
        }

        if (active != null)
        {
            active.SetActive(true);
            active.transform.localScale = baseScale * scaleMultiplier;
        }
    }
}

