using UnityEngine;

public class MultAdder : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private float multToAdd;
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
            scoreManager.AddMult(multToAdd);
            // Spawn text at the ball's position
            floatingTextSpawner?.SpawnText(collision.collider.transform.position, "x" + multToAdd);
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
            scoreManager.AddMult(multToAdd);
            // Spawn text at the ball's position
            floatingTextSpawner?.SpawnText(col.transform.position, "x" + multToAdd);
        }
    }

    public void multiplyMultToAdd(float mult)
    {
        multToAdd *= mult;
    }
}
