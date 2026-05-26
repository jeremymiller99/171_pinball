using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class Abductor : MonoBehaviour
{
    private enum AbductionState
    {
        Idle,
        GoingToAbduction,
        Abducting,
        Fighting,
        ReturningAbductedObject,
        Leaving
    }

    [SerializeField] private AbductionState abductionState;
    [SerializeField] private Vector3 startingPosition;
    [SerializeField] private FrenzyManager frenzyManager;
    [SerializeField] private bool activatedFrenzy;

    [Header("Going To Abduction")]
    [SerializeField] private Transform abductionPosition;
    [SerializeField] private float abductionSpeed;

    [Header("Abduction Process")]
    [SerializeField] private GameObject objectToAbduct;
    [SerializeField] private float abductingObjectSpeed;
    [SerializeField] private Transform targetAbductionPosition;
    [SerializeField] private float abductionShrinkingSpeed;

    [Header("Fighting")]
    [SerializeField] private GameObject fightingPositionParent;
    [SerializeField] private List<Vector3> fightingPositions;
    [SerializeField] private int fightingPositionIndex = 0;
    [SerializeField] private float fightingMovementSpeed;
    [SerializeField] private int health;

    [Header("Returning Abducted Object")]
    [SerializeField] private Vector3 returningObjectPosition;
    [SerializeField] private Vector3 abductedObjectSize;

    private void Awake()
    {
        frenzyManager = FindAnyObjectByType<FrenzyManager>();
    }

    private void Start()
    {
        startingPosition = transform.position;
        gameObject.SetActive(false);
        abductionState = AbductionState.Idle;
        foreach (Transform child in fightingPositionParent.transform)
        {
            fightingPositions.Add(child.position);
        }
    }

    private void Update()
    {
        switch (abductionState)
        {
            case AbductionState.GoingToAbduction:
                transform.position = Vector3.MoveTowards(transform.position, abductionPosition.position, abductionSpeed * Time.deltaTime);

                if (transform.position == abductionPosition.position)
                {
                    abductedObjectSize = objectToAbduct.transform.localScale;
                    returningObjectPosition = objectToAbduct.transform.position;
                    abductionState = AbductionState.Abducting;
                }
                break;
            case AbductionState.Abducting:
                objectToAbduct.transform.position = Vector3.MoveTowards(objectToAbduct.transform.position, targetAbductionPosition.position, abductingObjectSpeed * Time.deltaTime);
                objectToAbduct.transform.localScale -= Time.deltaTime * abductionShrinkingSpeed * Vector3.one;
                if (objectToAbduct.transform.position == targetAbductionPosition.position)
                {
                    objectToAbduct.GetComponentInChildren<Renderer>().enabled = false;
                    objectToAbduct.GetComponentInChildren<Collider>().enabled = false;
                    abductionState = AbductionState.Fighting;
                    fightingPositionIndex = 0;
                    transform.LookAt(fightingPositions[fightingPositionIndex]);
                }
                break;
            case AbductionState.Fighting:
                transform.position = Vector3.MoveTowards(transform.position, fightingPositions[fightingPositionIndex], fightingMovementSpeed * Time.deltaTime);
                if (transform.position == fightingPositions[fightingPositionIndex])
                {
                    fightingPositionIndex++;
                    if (fightingPositionIndex >= fightingPositions.Count)
                    {
                        fightingPositionIndex = 0;
                    }
                    transform.LookAt(fightingPositions[fightingPositionIndex]);
                }
                break;
            case AbductionState.ReturningAbductedObject:
                transform.position = Vector3.MoveTowards(transform.position, abductionPosition.position, abductionSpeed * Time.deltaTime);
                if (transform.position == abductionPosition.position)
                {
                    transform.LookAt(startingPosition);
                    objectToAbduct.GetComponentInChildren<Renderer>().enabled = true;
                    objectToAbduct.GetComponentInChildren<Collider>().enabled = true;
                    objectToAbduct.GetComponent<OnDropAbduction>().gettingAbducted = false;
                    objectToAbduct.transform.position = Vector3.MoveTowards(objectToAbduct.transform.position, returningObjectPosition, abductingObjectSpeed * Time.deltaTime);
                    objectToAbduct.transform.localScale += Time.deltaTime * abductionShrinkingSpeed * Vector3.one;
                }

                if (objectToAbduct.transform.position == returningObjectPosition)
                {
                    objectToAbduct.transform.localScale = abductedObjectSize;
                    abductionState = AbductionState.Leaving;
                }
                break;
            case AbductionState.Leaving:
                if (!activatedFrenzy)
                {
                    frenzyManager.ActivateFrenzy(objectToAbduct.transform.position, Vector3.zero);
                    activatedFrenzy = true;
                }

                transform.position = Vector3.MoveTowards(transform.position, startingPosition, abductionSpeed * Time.deltaTime);
                break;

        }
    }

    public void StartAbduction()
    {
        gameObject.SetActive(true);
        abductionState = AbductionState.GoingToAbduction;
        activatedFrenzy = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponent<Ball>())
        {
            if (abductionState == AbductionState.Fighting)
            {
                health--;
                Rigidbody rb = collision.rigidbody;
                Vector3 bumperCenter = transform.position;

                ServiceLocator.Get<AudioManager>()?.PlayBumperHit(bumperCenter);

                Vector3 forceDir = (collision.transform.position - bumperCenter).normalized;
                if (health <= 0)
                {
                    abductionState = AbductionState.ReturningAbductedObject;
                    transform.LookAt(abductionPosition);
                }
            }
        }
    }
}
