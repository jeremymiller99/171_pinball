using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class BoardComponent : MonoBehaviour
{
    public TypeOfScore typeOfScore;
    public float amountToScore;
    public GameObject leftObject;
    public GameObject rightObject;
    public GameObject upObject;
    public GameObject downObject;
    public List<GameObject> components = new List<GameObject>();
    public bool isSelected = false;
    public float maxPulseScale;
    public float pulseAmount;
    public Vector3 startingSize;
    [SerializeField] private int directionOfPulse = 1;

    void Awake()
    {
        startingSize = transform.localScale;
        BoardComponent[] boardComponents = FindObjectsByType<BoardComponent>(FindObjectsSortMode.InstanceID);
        foreach (BoardComponent boardComponent in boardComponents)
        {
            GameObject newObject = boardComponent.gameObject;
            if (!components.Contains(newObject) && gameObject != newObject) {
                components.Add(newObject);
            }
        }

        FindLeft();
        FindRight();
        FindUp();
        FindDown();

        if (!leftObject)
        {
            leftObject = rightObject;
        } else if (!rightObject)
        {
            rightObject = leftObject;
        }

        if (!upObject)
        {
            upObject = downObject;
        } else if (!downObject)
        {
            downObject = upObject;
        }
    }

    void FindLeft()
    {
        foreach (GameObject obj in components)
        {
            Vector3 newPos = obj.transform.position;
            Vector3 currentPos = transform.position;
            if (newPos.x >= currentPos.x) continue;
            if (!leftObject)
            {
                leftObject = obj;
                continue;
            }

            Vector3 oldPos = leftObject.transform.position;
            if (Vector3.Distance(currentPos, oldPos) > Vector3.Distance(currentPos, newPos))
            {
                leftObject = obj;
            }
        }
    }

    void FindRight()
    {
        foreach (GameObject obj in components)
        {
            Vector3 newPos = obj.transform.position;
            Vector3 currentPos = transform.position;
            if (newPos.x <= currentPos.x) continue;
            if (!rightObject)
            {
                rightObject = obj;
                continue;
            }

            Vector3 oldPos = rightObject.transform.position;
            if (Vector3.Distance(currentPos, oldPos) > Vector3.Distance(currentPos, newPos))
            {
                rightObject = obj;
            }
        }
    }

    void FindUp()
    {
        foreach (GameObject obj in components)
        {
            Vector3 newPos = obj.transform.position;
            Vector3 currentPos = transform.position;
            if (newPos.z <= currentPos.z) continue;
            if (!upObject)
            {
                upObject = obj;
                continue;
            }

            Vector3 oldPos = upObject.transform.position;
            if (Vector3.Distance(currentPos, oldPos) > Vector3.Distance(currentPos, newPos))
            {
                upObject = obj;
            }
        }
    }

    void FindDown()
    {
        foreach (GameObject obj in components)
        {
            Vector3 newPos = obj.transform.position;
            Vector3 currentPos = transform.position;
            if (newPos.z >= currentPos.z) continue;
            if (!downObject)
            {
                downObject = obj;
                continue;
            }

            Vector3 oldPos = downObject.transform.position;
            if (Vector3.Distance(currentPos, oldPos) > Vector3.Distance(currentPos, newPos))
            {
                downObject = obj;
            }
        }
    }

    public void FixedUpdate()
    {
        if (!isSelected) return;
        if (directionOfPulse == 1)
        {
            transform.localScale = Vector3.MoveTowards(transform.localScale, startingSize * maxPulseScale, pulseAmount);
            if (transform.localScale == startingSize * maxPulseScale)
            {
                directionOfPulse *= -1;
            }
        } else
        {
            transform.localScale = Vector3.MoveTowards(transform.localScale, startingSize * (1 / maxPulseScale), pulseAmount);
            if (transform.localScale == startingSize * (1 / maxPulseScale))
            {
                directionOfPulse *= -1;
            }
        }
    }

    public void Select()
    {
        isSelected = true;
    }
    public void DeSelect()
    {
        isSelected = false;
        transform.localScale = startingSize;
    }
}
