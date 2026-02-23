// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-17.
using System.Drawing;
using UnityEngine;
using UnityEngine.XR;

public class TenzoBall : Ball
{
    [SerializeField] private float pointsToAdd;
    [SerializeField] private float multToAdd;
    [SerializeField] private int componentHitsPerTenzo = 10;
    [SerializeField] private bool handledOnHitEffect = false;


    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (componentHits % componentHitsPerTenzo == 0 && !handledOnHitEffect)
        {
            base.AddScore(pointsToAdd, TypeOfScore.coins, pos);
            base.AddScore(multToAdd, TypeOfScore.mult, pos);
            handledOnHitEffect = true;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.collider.GetComponent<BoardComponent>())
        {
            handledOnHitEffect = false;
        }
    }
}
