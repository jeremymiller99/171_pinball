// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using System;
using UnityEngine;

[Serializable]
public sealed class ProfileStats
{
    [Tooltip("Total points scored across all rounds played (banked points on drain).")]
    public double totalPointsScored;

    [Tooltip("Total runs completed.")]
    public int totalBoardWins;

    public void AddPoints(double points)
    {
        if (points <= 0d)
        {
            return;
        }

        totalPointsScored += points;
    }

    public void RecordRunCompleted()
    {
        totalBoardWins += 1;
    }
}

