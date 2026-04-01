// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using UnityEngine;

public class MultAdder : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private float multToAdd;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;

    private float baseMultToAdd;
    private int upgradeCount;
    private float multMultiplier = 1f;

    public float MultToAdd => GetEffectiveMultToAdd();

    private void Awake()
    {
        baseMultToAdd = multToAdd;

        EnsureRefs();
    }

    private float GetEffectiveMultToAdd()
    {
        return (baseMultToAdd * (1 + upgradeCount)) * multMultiplier;
    }

    private void EnsureRefs()
    {
        if (scoreManager == null)
        {
            scoreManager = ServiceLocator.Get<ScoreManager>();
        }

        if (floatingTextSpawner == null)
        {
            floatingTextSpawner = ServiceLocator.Get<FloatingTextSpawner>();
        }
    }

    public void multiplyMultToAdd(float mult)
    {
        multMultiplier *= mult;
    }

    public void UpgradeAddBaseValue()
    {
        upgradeCount++;
    }
}
