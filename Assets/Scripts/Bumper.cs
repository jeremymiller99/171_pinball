using UnityEngine;

public class Bumper : MonoBehaviour
{
    [SerializeField] private float bounceForce = 10f;
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Ball")){
            Rigidbody rb = collision.rigidbody;

            Vector3 forceDir = (collision.transform.position - transform.position).normalized;
            rb.AddForce(forceDir * bounceForce, ForceMode.Impulse);
        }
    }   
}
