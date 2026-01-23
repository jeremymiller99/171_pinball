using UnityEngine;
using UnityEditor;

public class Board : MonoBehaviour
{

    public GameObject[] prefabs;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        prefabs = GameObject.FindGameObjectWithTag("SegmentTracker").GetComponent<SegmentTracker>().prefabs;
        foreach (GameObject prefab in prefabs) {
            Instantiate(prefab);
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
