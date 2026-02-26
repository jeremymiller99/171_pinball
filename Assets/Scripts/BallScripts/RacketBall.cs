using UnityEngine;

public class RacketBall : Ball
{
    [SerializeField] private float multToAddOnPaddleHit = 0.1f;

    new void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);
        if (collision.collider.GetComponentInParent<PinballFlipper>() != null)
        {
            AddScore(multToAddOnPaddleHit, TypeOfScore.mult, transform);
        }
    }
}
