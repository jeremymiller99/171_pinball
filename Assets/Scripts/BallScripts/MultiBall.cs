using UnityEngine;

public class MultiBall : Ball
{
    [SerializeField] private GameObject prefab;
    public bool readyToSplit = true;

    //Ball splits into two if it hits a target
    void OnCollisionEnter(Collision collision)
    {
        if(readyToSplit && collision.collider.GetComponent<MultAdder>())
        {
            readyToSplit = false;
            GameObject newBall = Instantiate(prefab);
            newBall.GetComponent<MultiBall>().readyToSplit = false;
        }
    }
}
