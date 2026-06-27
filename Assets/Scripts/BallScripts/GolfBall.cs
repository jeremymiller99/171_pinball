using UnityEngine;

public class GolfBall : Ball
{
    [SerializeField] private float startingMultiplier = 2;
    [SerializeField] private float lostMultiplierPerHit = .05f;


    private void Awake()
    {
        SetBallMultiplier();
    }

    private void SetBallMultiplier()
    {
        ballPointMultiplier = startingMultiplier;
        ballMultMultiplier = startingMultiplier;
    }

    override protected void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponent<Portal>() != null)
        {
            SetBallMultiplier();
        }

        base.OnCollisionEnter(collision);
    }

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        base.AddScore(amount, typeOfScore, pos);
        ballMultMultiplier -= lostMultiplierPerHit;
        ballPointMultiplier -= lostMultiplierPerHit;
    }
}
