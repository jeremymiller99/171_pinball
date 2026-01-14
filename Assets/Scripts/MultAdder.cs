using UnityEngine;

public class MultAdder : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;

    [SerializeField] private float multToAdd;
    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Ball"))
        {
            scoreManager.AddMult(multToAdd);
        }
    }
}
