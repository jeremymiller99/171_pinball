// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using UnityEngine;

public class BOMI : Ball
{
    [SerializeField] private float timeNotScoring = 7.5f;
    [SerializeField] private float timeScoring = 5f;
    [SerializeField] private float currentTimer = 0f;
    [SerializeField] private bool isScoring = true;

    void Update()
    {
        currentTimer += Time.deltaTime;
        if (isScoring)
        {
            if (currentTimer >= timeScoring)
            {
                isScoring = false;
                currentTimer = 0f;
            }

        }
        else
        {
            if (currentTimer >= timeNotScoring)
            {
                isScoring = true;
                currentTimer = 0f;
            }
        }
    }

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (!isScoring) return;

        base.AddScore(amount, typeOfScore, pos);
    }
}
