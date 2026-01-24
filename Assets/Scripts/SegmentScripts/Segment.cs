using Unity.Mathematics;
using UnityEngine;


public class Segment : MonoBehaviour
{
    public enum segmentType
    {
        topLeft,
        right
    }
    [SerializeField] private Vector3 startingSpot;

    public segmentType type;

    void Awake()
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag("Segment");

        foreach(GameObject obj in objs)
        {
            if (obj.GetComponent<Segment>().type == type && obj != gameObject)
            {
                Destroy(gameObject);
            }
        }
        DontDestroyOnLoad(gameObject);
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Transform board = GameObject.FindGameObjectWithTag("Board").transform;
        transform.SetLocalPositionAndRotation(startingSpot + board.transform.position, board.transform.rotation);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
