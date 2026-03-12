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
    [SerializeField] private GameObject[] modelPrefabs;
    private GameObject currentModelInstance;
    private int currentTagIndex;

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
                AudioManager.Instance.StopAlienShipRumble();
                AudioManager.Instance.PlayAlienArrival(currentTagIndex);
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
                AudioManager.Instance.StopAlienShipRumble();
            }
        }
    }

    public void Spawn()
    {
        transform.localRotation = Quaternion.identity;
        inPlay = true;
        coinsToGive = Random.Range(minCoinsToGive, maxCoinsToGive);
        currentTagIndex = Random.Range(0, tagsOfComponents.Length);
        componentTagLookingFor = tagsOfComponents[currentTagIndex];

        // Hit goal range for aliens (Target/Portal) is half of what it is for Bumpers; seconds stay the same.
        bool isBumper = componentTagLookingFor == "Bumper";
        int hitMin = isBumper ? minHitsRequired : Mathf.Max(1, minHitsRequired / 2);
        int hitMax = isBumper ? maxHitsRequired : Mathf.Max(hitMin, maxHitsRequired / 2);
        hitsLeft = Random.Range(hitMin, hitMax);
        secondsLeft = Random.Range(minSecondsToHit, maxSecondsToHit);

        if (modelPrefabs != null && modelPrefabs.Length > 0)
        {
            if (currentModelInstance != null)
            {
                Destroy(currentModelInstance);
                currentModelInstance = null;
            }

            GameObject prefab = modelPrefabs[Random.Range(0, modelPrefabs.Length)];
            MeshRenderer defaultRenderer = GetComponent<MeshRenderer>();

            if (prefab != null)
            {
                currentModelInstance = Instantiate(prefab, transform);
                currentModelInstance.transform.localPosition = Vector3.zero;
                currentModelInstance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                currentModelInstance.transform.localScale = Vector3.one * 2f;

                if (defaultRenderer != null)
                {
                    defaultRenderer.enabled = false;
                }
            }
            else if (defaultRenderer != null)
            {
                defaultRenderer.enabled = true;
            }
        }
        else
        {
            if (currentModelInstance != null)
            {
                Destroy(currentModelInstance);
                currentModelInstance = null;
            }

            MeshRenderer defaultRenderer = GetComponent<MeshRenderer>();
            if (defaultRenderer != null)
            {
                defaultRenderer.enabled = true;
            }
        }

        AudioManager.Instance.StartAlienShipRumble();
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
        transform.localRotation *= Quaternion.Euler(0f, 180f, 0f);
        despawning = true;
        AudioManager.Instance.PlayAlienDeparture();
        AudioManager.Instance.StartAlienShipRumble();
    }
}
