using UnityEngine;

public class Bumper : MonoBehaviour
{
    [SerializeField] private CameraShake camShake;
    [SerializeField] private float bounceForce = 10f;

    private void OnCollisionEnter(Collision collision)
    {
        //make sure camshake exists
        if (!camShake)
        {
            camShake = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<CameraShake>();
        }
        
        if (collision.collider.CompareTag("Ball")){
            Rigidbody rb = collision.rigidbody;

            Vector3 forceDir = (collision.transform.position - transform.position).normalized;
            rb.AddForce(forceDir * bounceForce, ForceMode.Impulse);

            camShake.Shake(0.2f, 0.1f);
        }
    }   
}
