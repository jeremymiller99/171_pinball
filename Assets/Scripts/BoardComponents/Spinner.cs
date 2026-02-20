using System.Drawing;
using Unity.Mathematics;
using UnityEngine;

public class Spinner : MonoBehaviour
{
    [SerializeField] private float prevAngle = 0;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private float amountToAdd;
    [SerializeField] private float maxAngle = 300;
    [SerializeField] private float minAngle = 100;

    void Awake()
    {
        scoreManager = FindFirstObjectByType<ScoreManager>();
    }
    void FixedUpdate()
    {
        
        float newAngle = transform.rotation.eulerAngles.z;
        if (prevAngle == 0)
        {
            prevAngle = newAngle;
            return;
        }

        if ((newAngle <= minAngle && prevAngle >= maxAngle) || (newAngle >= maxAngle && prevAngle <= minAngle))
        {
            scoreManager.AddScore(amountToAdd, TypeOfScore.points, transform);
        }
        prevAngle = newAngle;
    }
}
