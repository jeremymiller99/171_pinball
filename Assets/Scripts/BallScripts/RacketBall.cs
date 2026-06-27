using Unity.VisualScripting;
using UnityEngine;

public class RacketBall : Ball
{
    [SerializeField] private float multToAddOnPaddleHit = 0.1f;
    [SerializeField] private PinballFlipper prevFlipper;

    new void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);
        PinballFlipper hitFlipper = collision.collider.GetComponentInParent<PinballFlipper>();
        if (hitFlipper != null && prevFlipper != hitFlipper)
        {
            prevFlipper = hitFlipper;
            AddScore(multToAddOnPaddleHit, TypeOfScore.mult, transform);
        }
    }
}
