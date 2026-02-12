using System.Drawing;
using UnityEngine;

public class Ball : MonoBehaviour
{
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private int amountToEmit;

    void Awake()
    {
        trailRenderer = GetComponent<TrailRenderer>();
    }

    void OnCollisionEnter(Collision collision)
    {

        if (collision.collider.GetComponent<Portal>())
        {
            trailRenderer.emitting = false;
            return; //get rid of this line if we want hitting portal to emit particles.
        }

        if (collision.collider.GetComponent<PointAdder>())
        {
            //use particle system on the collider
        }

        if (collision.collider.GetComponent<MultAdder>())
        {
            //use particle system on the collider
        }

    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.collider.GetComponent<Portal>())
        {
            trailRenderer.emitting = true;
        }
    }
}
