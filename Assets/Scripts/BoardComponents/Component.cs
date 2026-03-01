// Generated with Cursor AI (GPT-5.2), by OpenAI, 2026-02-24.
// Change: add selection outline + portal paired-exit outline.
using UnityEngine;
using System.Collections.Generic;

public class BoardComponent : MonoBehaviour
{
    private const float defaultSelectionOutlineWidth = 8f;

    public TypeOfScore typeOfScore;
    public float amountToScore;
    public GameObject leftObject;
    public GameObject rightObject;
    public GameObject upObject;
    public GameObject downObject;
    public List<GameObject> components = new List<GameObject>();
    public bool isConfirmed = false;
    public float maxPulseScale;
    public float pulseAmount;
    public Vector3 startingSize;
    [SerializeField] private int directionOfPulse = 1;

    [Header("Selection Outline")]
    [SerializeField] private bool useSelectionOutline = true;
    [SerializeField] private Outline.Mode selectionOutlineMode = Outline.Mode.OutlineAll;
    [SerializeField] private Color confirmOutlineColor = Color.white;
    [SerializeField] private Color selectionOutlineColor = Color.gray;
    [SerializeField, Range(0f, 10f)] private float selectionOutlineWidth = defaultSelectionOutlineWidth;

    private Outline selectionOutline;
    private Outline portalExitOutline;
    private Transform cachedPortalExit;

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
        }

        if (!downObject)
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
        if (!isConfirmed) return;
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

    public void PrewarmSelectionOutline()
    {
        if (!useSelectionOutline)
        {
            return;
        }

        EnsureSelectionOutline();
        if (selectionOutline == null)
        {
            return;
        }

        ApplySelectionOutlineSettings(selectionOutline);
        selectionOutline.enabled = false;

        SetPortalExitOutlineEnabled(false);
    }

    private void EnsureSelectionOutline()
    {
        if (selectionOutline != null)
        {
            return;
        }

        selectionOutline = GetComponent<Outline>();
        if (selectionOutline == null)
        {
            selectionOutline = gameObject.AddComponent<Outline>();
        }
    }

    private void SetPortalExitOutlineEnabled(bool isEnabled)
    {
        Portal portal = GetComponent<Portal>();
        if (portal == null || portal.portalExit == null)
        {
            if (portalExitOutline != null)
            {
                portalExitOutline.enabled = false;
            }

            portalExitOutline = null;
            cachedPortalExit = null;
            return;
        }

        if (cachedPortalExit != portal.portalExit)
        {
            cachedPortalExit = portal.portalExit;
            portalExitOutline = null;
        }

        if (portalExitOutline == null)
        {
            portalExitOutline = cachedPortalExit.GetComponent<Outline>();
            if (portalExitOutline == null)
            {
                portalExitOutline = cachedPortalExit.gameObject.AddComponent<Outline>();
            }
        }

        ApplySelectionOutlineSettings(portalExitOutline);
        portalExitOutline.enabled = isEnabled;
    }

    private void ApplySelectionOutlineSettings(Outline outline)
    {
        if (outline == null)
        {
            return;
        }

        outline.OutlineMode = selectionOutlineMode;
        outline.OutlineColor = selectionOutlineColor;
        outline.OutlineWidth = selectionOutlineWidth;
    }

    public void Select()
    {
        if (!useSelectionOutline || isConfirmed)
        {
            return;
        }

        EnsureSelectionOutline();
        selectionOutline.enabled = true;
        selectionOutline.OutlineMode = selectionOutlineMode;
        selectionOutline.OutlineColor = selectionOutlineColor;
        selectionOutline.OutlineWidth = selectionOutlineWidth;
        SetPortalExitOutlineEnabled(true);
    }

    public void DeSelect()
    {
        if (!isConfirmed)
        {
            selectionOutline.enabled = false;
            SetPortalExitOutlineEnabled(false);
        }
    }

    public void Confirm()
    {
        selectionOutline.OutlineColor = confirmOutlineColor;
        isConfirmed = true;
    }

    public void DeConfirm()
    {
        if (!isConfirmed) return;
        selectionOutline.OutlineColor = selectionOutlineColor;
        isConfirmed = false;
        transform.localScale = startingSize;
        selectionOutline.enabled = false;
        SetPortalExitOutlineEnabled(false);
    }
}
