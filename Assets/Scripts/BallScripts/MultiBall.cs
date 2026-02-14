using UnityEngine;

public class MultiBall : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private BallSpawner ballSpawner;
    public bool readyToSplit = true;


    void Awake()
    {
        ballSpawner = FindFirstObjectByType<BallSpawner>();
    }
    //Ball splits into two if it hits a target
    void OnCollisionEnter(Collision collision)
    {
        if(readyToSplit && collision.collider.GetComponent<MultAdder>())
        {
            readyToSplit = false;
            GameObject newBall = Instantiate(prefab);
            newBall.GetComponent<MultiBall>().readyToSplit = false;
            ballSpawner.ActiveBalls.Add(newBall);
        }
    }
}
