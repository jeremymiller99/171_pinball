using UnityEngine;

public class PointAdder : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private float pointsToAdd;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;

    void OnCollisionEnter(Collision collision)
    {
        //make sure scoremanager and floatingtextspawner exist
        if (!scoreManager || !floatingTextSpawner)
        {
            scoreManager = GetComponentInParent<ScoreManager>();
            floatingTextSpawner = GetComponentInParent<FloatingTextSpawner>();
        }

        if (collision.collider.CompareTag("Ball"))
        {
            scoreManager.AddPoints(pointsToAdd);
            // Spawn text at the ball's position
            floatingTextSpawner?.SpawnText(collision.collider.transform.position, "+" + pointsToAdd);
        }
    }

    void OnTriggerEnter(Collider col)
    {
        //make sure scoremanager and floatingtextspawner exist
        if (!scoreManager || !floatingTextSpawner)
        {
            scoreManager = GetComponentInParent<ScoreManager>();
            floatingTextSpawner = GetComponentInParent<FloatingTextSpawner>();
        }
        
        if (col.CompareTag("Ball"))
        {
            scoreManager.AddPoints(pointsToAdd);
            // Spawn text at the ball's position
            floatingTextSpawner?.SpawnText(col.transform.position, "+" + pointsToAdd);
        }
    }

    public void multiplyPointsToAdd(float mult)
    {
        pointsToAdd *= mult;
    } 
}
