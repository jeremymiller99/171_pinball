using UnityEngine;
using System.Collections.Generic;

public class MultBall : Ball
{
    [Header("Mult Ball")]
    [Tooltip("Multiplier applied to MultAdder values while this ball is in contact.\n" +
             "For 'Red Two' this should be 2.")]
    [Min(0.01f)]
    [SerializeField] private float amountToMultiply = 2f;

    private readonly HashSet<MultAdder> activeAdders = new HashSet<MultAdder>();

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

        MultAdder adder = collision.collider.GetComponent<MultAdder>();
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

        MultAdder adder = collision.collider.GetComponent<MultAdder>();
        if (adder != null)
        {
            RemoveFromAdderIfNeeded(adder);
        }
    }

    void OnTriggerEnter(Collider collider)
    {
        if (collider == null)
        {
            return;
        }

        MultAdder adder = collider.GetComponent<MultAdder>();
        if (adder != null)
        {
            ApplyToAdderIfNeeded(adder);
        }
    }

    void OnTriggerExit(Collider collider)
    {
        if (collider == null)
        {
            return;
        }

        MultAdder adder = collider.GetComponent<MultAdder>();
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

    private void ApplyToAdderIfNeeded(MultAdder adder)
    {
        if (adder == null)
        {
            return;
        }

        if (!activeAdders.Add(adder))
        {
            return;
        }

        adder.multiplyMultToAdd(amountToMultiply);
    }

    private void RemoveFromAdderIfNeeded(MultAdder adder)
    {
        if (adder == null)
        {
            return;
        }

        if (!activeAdders.Remove(adder))
        {
            return;
        }

        adder.multiplyMultToAdd(GetSafeInverseMultiplier());
    }

    private void ClearAllAppliedAdders()
    {
        if (activeAdders.Count == 0)
        {
            return;
        }

        float inverse = GetSafeInverseMultiplier();
        foreach (MultAdder adder in activeAdders)
        {
            if (adder != null)
            {
                adder.multiplyMultToAdd(inverse);
            }
        }

        activeAdders.Clear();
    }
}
