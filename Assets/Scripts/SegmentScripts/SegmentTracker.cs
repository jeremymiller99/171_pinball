using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class SegmentTracker : MonoBehaviour
{
    //tracks Segment prefabs across scenes

    public GameObject[] prefabs;

    void Awake()
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag("SegmentTracker");

        if (objs.Length > 1)
        {
            Destroy(this.gameObject);
        }
        DontDestroyOnLoad(gameObject);
    }

    public void RefreshPrefabs()
    {
        prefabs = GameObject.FindGameObjectsWithTag("Segment");
    }




}
