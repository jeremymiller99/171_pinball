using System;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ReplaceSegment : MonoBehaviour
{
    [SerializeField] private GameObject prefab;

    private Segment seg;

    private SegmentTracker tracker;


    void Awake()
    {
        tracker = GameObject.FindGameObjectWithTag("SegmentTracker").GetComponent<SegmentTracker>();
        seg = prefab.GetComponent<Segment>();
    }

    void Replace()
    {
        GameObject[] segments = GameObject.FindGameObjectsWithTag("Segment");
        foreach (GameObject obj in segments) {
            if(obj.GetComponent<Segment>().type == seg.type)
            {
                Destroy(obj);
                Instantiate(prefab);
                tracker.RefreshPrefabs();
                return;
            }
        }
        PrefabUtility.InstantiatePrefab(prefab);
        tracker.RefreshPrefabs();
        
    }
}
