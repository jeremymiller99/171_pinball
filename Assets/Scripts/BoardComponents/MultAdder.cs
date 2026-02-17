// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using UnityEngine;
using FMODUnity;
using System.Globalization;

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
#if UNITY_2022_2_OR_NEWER
            scoreManager = FindFirstObjectByType<ScoreManager>();
#else
            scoreManager = FindObjectOfType<ScoreManager>();
#endif
        }

        if (floatingTextSpawner == null)
        {
#if UNITY_2022_2_OR_NEWER
            floatingTextSpawner = FindFirstObjectByType<FloatingTextSpawner>();
#else
            floatingTextSpawner = FindObjectOfType<FloatingTextSpawner>();
#endif
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Ball"))
        {
            if (scoreManager == null) EnsureRefs();
            FMODUnity.RuntimeManager.PlayOneShot("event:/collide_mult");
            if (scoreManager == null)
                return;

            int token = scoreManager.PointsAndMultUiToken;
            float applied = scoreManager.AddMultDeferredUi(GetEffectiveMultToAdd());
            if (applied <= 0f)
                return;

            // Spawn red mult text at the ball's position; only increment HUD when the popup arrives.
            if (floatingTextSpawner != null)
            {
                floatingTextSpawner.SpawnMultText(
                    collision.collider.transform.position,
                    "x" + FormatMultiplier(applied),
                    applied,
                    () => scoreManager.ApplyDeferredMultUi(applied, token));
            }
            else
            {
                scoreManager.ApplyDeferredMultUi(applied, token);
            }
        }
    }

    void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Ball"))
        {
            if (scoreManager == null) EnsureRefs();
            if (scoreManager == null)
                return;

            int token = scoreManager.PointsAndMultUiToken;
            float applied = scoreManager.AddMultDeferredUi(GetEffectiveMultToAdd());
            if (applied <= 0f)
                return;

            // Spawn red mult text at the ball's position; only increment HUD when the popup arrives.
            if (floatingTextSpawner != null)
            {
                floatingTextSpawner.SpawnMultText(
                    col.transform.position,
                    "x" + FormatMultiplier(applied),
                    applied,
                    () => scoreManager.ApplyDeferredMultUi(applied, token));
            }
            else
            {
                scoreManager.ApplyDeferredMultUi(applied, token);
            }
        }
    }

    private static string FormatMultiplier(float value)
    {
        float rounded = Mathf.Round(value * 100f) / 100f;
        return rounded.ToString("0.##", CultureInfo.InvariantCulture);
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
