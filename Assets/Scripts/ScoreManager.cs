using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public float points;
    public float mult;

    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text multText;

    void Start()
    {
        points = 0f;
        mult = 1f;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void AddPoints(float p)
    {
        points += p;
        pointsText.text = points.ToString();
    }

    public void AddMult(float m)
    {
        mult += m;
        multText.text = mult.ToString();
    }
}
