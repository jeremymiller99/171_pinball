using UnityEngine;
using System.Collections;
using TMPro;
using System.Collections.Generic;

public class AlienShip : MonoBehaviour
{
    public bool inPlay;
    [SerializeField] private string[] tagsOfComponents = {"Bumper", "Target"};
    [SerializeField] private Vector3 outOfBoundsLocation;
    [SerializeField] private Vector3 dockLocation;
    [SerializeField] private float speed;
    [SerializeField] private int minHitsRequired;
    [SerializeField] private int maxHitsRequired;
    [SerializeField] private int minSecondsToHit;
    [SerializeField] private int maxSecondsToHit;
    [SerializeField] private int minCoinsToGive;
    [SerializeField] private int maxCoinsToGive;
    [SerializeField] private string componentTagLookingFor;
    [SerializeField] private int hitsLeft;
    [SerializeField] private int secondsLeft;
    [SerializeField] private float timeSinceLastSubtraction;
    [SerializeField] private int coinsToGive;
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Canvas canvas;
    [SerializeField] private bool docked;
    [SerializeField] private bool despawning;
    [SerializeField] private List<GameObject> previousLastObjectsHit;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private GameRulesManager gameRulesManager;

    void Awake()
    {
        scoreManager = FindAnyObjectByType<ScoreManager>();
        gameRulesManager = FindAnyObjectByType<GameRulesManager>();
        text = GetComponentInChildren<TextMeshProUGUI>();
        canvas = GetComponentInChildren<Canvas>();
        canvas.gameObject.SetActive(false);
        inPlay = false;
    }

    void Start()
    {
        transform.localPosition = outOfBoundsLocation;
    }

    void Update()
    {
        if (inPlay && !docked && !despawning)
        {
            if (transform.localPosition != dockLocation)
            {
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, dockLocation, speed * Time.deltaTime);
            } else
            {
                canvas.gameObject.SetActive(true);
                SetText();
                docked = true;
            }
        }

        if (docked)
        {
            timeSinceLastSubtraction += Time.deltaTime;
            if (timeSinceLastSubtraction >= 1f)
            {
                secondsLeft--;
                timeSinceLastSubtraction = 0;
                SetText();
            }

            if (secondsLeft == 0)
            {
                DeSpawn();
            }

            for (int i = 0; i < gameRulesManager.ActiveBalls.Count; i++)
            {
                Ball ball = gameRulesManager.ActiveBalls[i].GetComponent<Ball>();
                while (previousLastObjectsHit.Count < gameRulesManager.ActiveBalls.Count)
                {
                    previousLastObjectsHit.Add(gameObject);
                }
                
                if (!ball.lastObjectHit) continue;

                if (previousLastObjectsHit[i] != ball.lastObjectHit)
                {

                    previousLastObjectsHit[i] = ball.lastObjectHit;
                    if (ball.lastObjectHit.tag == componentTagLookingFor)
                    {
                        hitsLeft--;
                        SetText();
                    }

                    if (hitsLeft == 0)
                    {
                        scoreManager.AddScore(coinsToGive, TypeOfScore.coins, canvas.transform);
                        DeSpawn();
                        return;
                    }
                }
            }
        }

        if (inPlay && despawning)
        {
            if (transform.localPosition != outOfBoundsLocation)
            {
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, outOfBoundsLocation, speed * Time.deltaTime);
            } else
            {
                inPlay = false;
                despawning = false;
            }
        }
    }

    public void Spawn()
    {
        inPlay = true;
        hitsLeft = Random.Range(minHitsRequired, maxHitsRequired);
        secondsLeft = Random.Range(minSecondsToHit, maxSecondsToHit);
        coinsToGive = Random.Range(minCoinsToGive, maxCoinsToGive);
        componentTagLookingFor = tagsOfComponents[Random.Range(0, 2)];
    }

    void SetText()
    {
        text.text =
            "I bet " + coinsToGive + " coins you can't hit " + 
            hitsLeft + " " + componentTagLookingFor + "s in " + 
            secondsLeft + " seconds!";
    }

    void DeSpawn()
    {
        docked = false;
        canvas.gameObject.SetActive(false);
        despawning = true;
    }
}
