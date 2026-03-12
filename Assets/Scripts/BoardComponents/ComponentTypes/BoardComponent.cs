// Generated with Cursor AI (GPT-5.2), by OpenAI, 2026-02-24.
// Change: add selection outline + portal paired-exit outline.
using UnityEngine;
using System.Collections.Generic;
using System;

public class BoardComponent : MonoBehaviour, System.IComparable<BoardComponent>
{
    private const float defaultSelectionOutlineWidth = 8f;

    public TypeOfScore typeOfScore;
    public float amountToScore;
    public BoardComponentType componentType;
    public List<GameObject> components = new List<GameObject>();
    public bool isConfirmed = false;
    public float maxPulseScale;
    public float pulseAmount;
    public Vector3 startingSize;
    [SerializeField] private int directionOfPulse = 1;
    [SerializeField] protected int ballHits = 0;
    [SerializeField] protected ScoreManager scoreManager;

    [Header("Selection Outline")]
    [SerializeField] private bool useSelectionOutline = true;
    [SerializeField] private Outline.Mode selectionOutlineMode = Outline.Mode.OutlineAll;
    [SerializeField] private Color confirmOutlineColor = Color.white;
    [SerializeField] private Color selectionOutlineColor = Color.gray;
    [SerializeField, Range(0f, 10f)] private float selectionOutlineWidth = defaultSelectionOutlineWidth;

    private Outline selectionOutline;
    private Outline portalExitOutline;
    private Transform cachedPortalExit;

    virtual protected void Awake()
    {
        startingSize = transform.localScale;
        scoreManager = FindAnyObjectByType<ScoreManager>();
        BoardComponent[] boardComponents = FindObjectsByType<BoardComponent>(FindObjectsSortMode.InstanceID);
        foreach (BoardComponent boardComponent in boardComponents)
        {
            GameObject newObject = boardComponent.gameObject;
            if (!components.Contains(newObject) && gameObject != newObject) {
                components.Add(newObject);
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
        isConfirmed = true;
        selectionOutline.enabled = true;
        selectionOutline.OutlineMode = selectionOutlineMode;
        selectionOutline.OutlineColor = confirmOutlineColor;
        selectionOutline.OutlineWidth = selectionOutlineWidth;
        SetPortalExitOutlineEnabled(true);
    }

    public void DeSelect()
    {
        isConfirmed = false;
        transform.localScale = startingSize;
        EnsureSelectionOutline();
        selectionOutline.enabled = false;
        SetPortalExitOutlineEnabled(false);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Ball>())
        {
            ballHits++;
        }
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponent<Ball>())
        {
            ballHits++;
        }
    }

    virtual public void AddScore()
    {
        scoreManager.AddScore(amountToScore, typeOfScore, transform);
    }

    public int CompareTo(BoardComponent otherComponent)
    {
        if (otherComponent.transform.position.z > transform.position.z)
        {
            return 1;
        } else if (otherComponent.transform.position.z < transform.position.z)
        {
            return -1;
        } else if (otherComponent.transform.position.x > transform.position.x)
        {
            return -1;
        } else
        {
            return 1;
        }
    }

}
