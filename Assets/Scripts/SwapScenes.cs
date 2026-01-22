using System;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SwapScenes : MonoBehaviour
{
    [SerializeField] private String sceneName;

    void Swap()
    {
        SceneManager.LoadScene(sceneName);
    }

}
