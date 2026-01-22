using Unity.VisualScripting;
using UnityEngine;

public class ReplaceSegment : MonoBehaviour
{
    [SerializeField] private GameObject prefab;

    private Segment seg;


    void Awake()
    {
        seg = prefab.GetComponent<Segment>();
    }

    void Replace()
    {
        Debug.Log("clicked");
        GameObject[] segments = GameObject.FindGameObjectsWithTag("Segment");
        foreach (GameObject obj in segments) {
            if(obj.GetComponent<Segment>().type == seg.type)
            {
                Destroy(obj);
                Instantiate(prefab,GameObject.FindGameObjectWithTag("Board").transform);
                Debug.Log("replaced");
            }
        }
        
    }
}
