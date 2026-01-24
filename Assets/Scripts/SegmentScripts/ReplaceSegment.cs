using System;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        GameObject[] segments = GameObject.FindGameObjectsWithTag("Segment");
        foreach (GameObject obj in segments) {
            if(obj.GetComponent<Segment>().type == seg.type)
            {
                obj.tag = "Untagged";
                Destroy(obj);
            }
        }
        Debug.Log(GameObject.FindGameObjectsWithTag("Segment").Length);
        PrefabUtility.InstantiatePrefab(prefab);
        Debug.Log(GameObject.FindGameObjectsWithTag("Segment").Length);
        
    }
}
