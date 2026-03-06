using System.IO;
using UnityEngine;

public class CasinoComponent : BoardComponent
{
    [Header("Casino")]
    [SerializeField] private float minScoreToAdd;
    [SerializeField] private float maxScoreToAdd;
    [SerializeField] private int ballHitsToGamble;
    [SerializeField] private int decimalPlace = 1;

    new protected void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponent<Ball>())
        {
            ballHits++;
            if (ballHits % ballHitsToGamble == 0)
            {
                AddScore();
            }

        }
    }
    override public void AddScore()
    {
        scoreManager.AddScore(Mathf.Floor(Random.Range(minScoreToAdd, maxScoreToAdd) * decimalPlace) / decimalPlace,
        typeOfScore, transform);
    }
}
