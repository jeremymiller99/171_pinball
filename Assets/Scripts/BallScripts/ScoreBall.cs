using UnityEngine;
using System.Collections.Generic;

public class ScoreBall : Ball
{
    [Header("Score Ball")]
    [Tooltip("Multiplier applied to PointAdder values while this ball is in contact.\n" +
             "For 'Blue Two' this should be 2.")]
    [Min(0.01f)]
    [SerializeField] private float amountToMultiply = 2f;

    private readonly HashSet<PointAdder> activeAdders = new HashSet<PointAdder>();

    private void OnValidate()
    {
        if (amountToMultiply < 0.01f)
        {
            amountToMultiply = 2f;
        }
    }

    private float GetSafeInverseMultiplier()
    {
        float safe = Mathf.Max(0.01f, amountToMultiply);
        return 1f / safe;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        PointAdder adder = collision.collider.GetComponent<PointAdder>();
        if (adder != null)
        {
            ApplyToAdderIfNeeded(adder);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        PointAdder adder = collision.collider.GetComponent<PointAdder>();
        if (adder != null)
        {
            RemoveFromAdderIfNeeded(adder);
        }
    }

    void OnTriggerEnter(Collider collision)
    {
        if (collision == null)
        {
            return;
        }

        PointAdder adder = collision.GetComponent<PointAdder>();
        if (adder != null)
        {
            ApplyToAdderIfNeeded(adder);
        }
    }

    void OnTriggerExit(Collider collision)
    {
        if (collision == null)
        {
            return;
        }

        PointAdder adder = collision.GetComponent<PointAdder>();
        if (adder != null)
        {
            RemoveFromAdderIfNeeded(adder);
        }
    }

    private void OnDisable()
    {
        ClearAllAppliedAdders();
    }

    private void OnDestroy()
    {
        ClearAllAppliedAdders();
    }

    private void ApplyToAdderIfNeeded(PointAdder adder)
    {
        if (adder == null)
        {
            return;
        }

        if (!activeAdders.Add(adder))
        {
            return;
        }

        adder.multiplyPointsToAdd(amountToMultiply);
    }

    private void RemoveFromAdderIfNeeded(PointAdder adder)
    {
        if (adder == null)
        {
            return;
        }

        if (!activeAdders.Remove(adder))
        {
            return;
        }

        adder.multiplyPointsToAdd(GetSafeInverseMultiplier());
    }

    private void ClearAllAppliedAdders()
    {
        if (activeAdders.Count == 0)
        {
            return;
        }

        float inverse = GetSafeInverseMultiplier();
        foreach (PointAdder adder in activeAdders)
        {
            if (adder != null)
            {
                adder.multiplyPointsToAdd(inverse);
            }
        }

        activeAdders.Clear();
    }
}

