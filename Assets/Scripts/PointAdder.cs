using UnityEngine;

public class PointAdder : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;

    [SerializeField] private float pointsToAdd;
    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Ball"))
        {
            scoreManager.AddPoints(pointsToAdd);
        }
    }
}
