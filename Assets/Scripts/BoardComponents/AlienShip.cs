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
                ServiceLocator.Get<AudioManager>()?.StopAlienShipRumble();
                ServiceLocator.Get<AudioManager>()?.PlayAlienArrival(currentTagIndex);
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
                ServiceLocator.Get<AudioManager>()?.StopAlienShipRumble();
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

        // Hit goal range varies by component type: Bumper uses serialized range; Target 1-5; Portal 1-3.
        int hitMin, hitMax;
        if (componentTagLookingFor == "Bumper")
        {
            hitMin = minHitsRequired;
            hitMax = maxHitsRequired;
        }
        else if (componentTagLookingFor == "Target")
        {
            hitMin = 1;
            hitMax = 5;
        }
        else if (componentTagLookingFor == "Portal")
        {
            hitMin = 1;
            hitMax = 3;
        }
        else
        {
            hitMin = Mathf.Max(1, minHitsRequired / 2);
            hitMax = Mathf.Max(hitMin, maxHitsRequired / 2);
        }
        hitsLeft = Random.Range(hitMin, hitMax + 1);
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

        ServiceLocator.Get<AudioManager>()?.StartAlienShipRumble();
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
        ServiceLocator.Get<AudioManager>()?.PlayAlienDeparture();
        ServiceLocator.Get<AudioManager>()?.StartAlienShipRumble();
    }
}
