using UnityEngine;
using UnityEditor;

public class MultiBall : MonoBehaviour
{
    public bool readyToSplit = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //Ball splits into two if it hits a target
    void OnCollisionEnter(Collision collision)
    {
        if(readyToSplit && collision.collider.GetComponent<MultAdder>())
        {
            readyToSplit = false;
            GameObject newBall = GameObjectUtility.DuplicateGameObject(gameObject);
        }
    }
}
