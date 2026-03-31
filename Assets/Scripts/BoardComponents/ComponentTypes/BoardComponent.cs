// Refactored with Antigravity by jjmil on 2026-03-29.
// Change: removed Portal coupling into PortalOutlineExtension.
// Change: removed unused components list and O(N^2) Awake discovery.
// Change: added virtual OnSelected/OnDeselected hooks for subclass extensibility.
// Change: added componentGuid for stable identity across scene reloads.
using UnityEngine;
using TMPro;
using System;

public class BoardComponent : MonoBehaviour, System.IComparable<BoardComponent>
{
    private const float defaultOutlineWidth = 6f;
    private const int outlineRenderQueueOffset = 100;
    private const int outlineStencilRef = 2;

    public TypeOfScore typeOfScore;
    public float amountToScore;
    public BoardComponentType componentType;
    public bool isConfirmed = false;
    public float maxPulseScale;
    public float pulseAmount;
    public Vector3 startingSize;
    [SerializeField] private int directionOfPulse = 1;
    [SerializeField] protected int ballHits = 0;
    [SerializeField] protected ScoreManager scoreManager;

    [Header("Identity")]
    [Tooltip("Stable GUID for this component. " +
        "Auto-assigned in the editor if empty.")]
    [SerializeField] private string componentGuid;

    [Header("Hit Count Popup")]
    [Tooltip("When enabled, spawns a floating hit-count " +
        "number on each ball hit.")]
    [SerializeField] protected bool enableHitCountPopup = false;
    [Tooltip("Font for the hit count popup. " +
        "If null, uses the spawner's default (Jersey 10).")]
    [SerializeField] protected TMP_FontAsset hitCountFontAsset;
    [SerializeField] protected float hitCountPopupScale = 0.7f;
    [SerializeField]
    protected Vector2 hitCountPopupOffset = new Vector2(0f, 40f);
    [Tooltip("Color for the hit count popup text. " +
        "Set per component to match its associated ball color.")]
    [SerializeField] protected Color hitCountPopupColor = Color.white;
    protected FloatingTextSpawner floatingTextSpawner;

    [Header("Outline (always visible)")]
    [SerializeField] private bool useSelectionOutline = true;
    [SerializeField]
    private Outline.Mode selectionOutlineMode =
        Outline.Mode.OutlineVisible;
    [SerializeField] private Color defaultOutlineColor = Color.black;
    [SerializeField, Range(0f, 10f)]
    private float outlineWidth = defaultOutlineWidth;

    [Header("Shop Highlight")]
    [SerializeField] private Color confirmOutlineColor = Color.white;
    [SerializeField] private Color selectionOutlineColor = Color.gray;

    [Header("Hover Highlight")]
    [SerializeField] private Color hoverOutlineColor = Color.white;

    [Header("Drag Target Highlight")]
    [SerializeField]
    private Color dragTargetOutlineColor = new Color(0.3f, 1f, 0.3f);

    private Outline selectionOutline;
    private bool _isHoverHighlighted;

    public string ComponentGuid => componentGuid;

    public Color ConfirmOutlineColor => confirmOutlineColor;

    public Color DefaultOutlineColor => defaultOutlineColor;

    public Outline.Mode SelectionOutlineMode => selectionOutlineMode;

    public float OutlineWidth => outlineWidth;

    virtual protected void Awake()
    {
        startingSize = transform.localScale;
        scoreManager = ServiceLocator.Get<ScoreManager>();

        if (useSelectionOutline)
        {
            EnsureSelectionOutline();
            if (selectionOutline != null)
            {
                ApplyDefaultOutlineSettings(selectionOutline);
                selectionOutline.enabled = true;
            }
        }
    }

    public void FixedUpdate()
    {
        if (!isConfirmed) return;

        if (directionOfPulse == 1)
        {
            transform.localScale = Vector3.MoveTowards(
                transform.localScale,
                startingSize * maxPulseScale,
                pulseAmount);

            if (transform.localScale ==
                startingSize * maxPulseScale)
            {
                directionOfPulse *= -1;
            }
        }
        else
        {
            transform.localScale = Vector3.MoveTowards(
                transform.localScale,
                startingSize * (1 / maxPulseScale),
                pulseAmount);

            if (transform.localScale ==
                startingSize * (1 / maxPulseScale))
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

        ApplyDefaultOutlineSettings(selectionOutline);
        selectionOutline.enabled = true;

        NotifyListeners(
            l => l.OnBoardComponentPrewarmed());
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

    private void ApplyDefaultOutlineSettings(Outline outline)
    {
        if (outline == null)
        {
            return;
        }

        outline.RenderQueueOffset = outlineRenderQueueOffset;
        outline.StencilRef = outlineStencilRef;
        outline.OutlineMode = selectionOutlineMode;
        outline.OutlineColor = defaultOutlineColor;
        outline.OutlineWidth = outlineWidth;
    }

    private void ApplyHighlightOutlineSettings(
        Outline outline, Color color)
    {
        if (outline == null)
        {
            return;
        }

        outline.RenderQueueOffset = outlineRenderQueueOffset;
        outline.StencilRef = outlineStencilRef;
        outline.OutlineMode = selectionOutlineMode;
        outline.OutlineColor = color;
        outline.OutlineWidth = outlineWidth;
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
        ApplyHighlightOutlineSettings(
            selectionOutline, confirmOutlineColor);

        NotifyListeners(
            l => l.OnBoardComponentSelected());
    }

    public void DeSelect()
    {
        isConfirmed = false;
        transform.localScale = startingSize;
        EnsureSelectionOutline();
        ApplyDefaultOutlineSettings(selectionOutline);
        selectionOutline.enabled = true;

        NotifyListeners(
            l => l.OnBoardComponentDeselected());
    }

    private void NotifyListeners(
        System.Action<IBoardComponentSelectionListener> action)
    {
        var listeners =
            GetComponents<IBoardComponentSelectionListener>();

        for (int i = 0; i < listeners.Length; i++)
        {
            action(listeners[i]);
        }
    }

    public void HighlightHover()
    {
        if (!useSelectionOutline || isConfirmed)
        {
            return;
        }

        EnsureSelectionOutline();
        _isHoverHighlighted = true;
        ApplyHighlightOutlineSettings(
            selectionOutline, hoverOutlineColor);
    }

    public void UnhighlightHover()
    {
        if (!useSelectionOutline || isConfirmed)
        {
            _isHoverHighlighted = false;
            return;
        }

        _isHoverHighlighted = false;
        EnsureSelectionOutline();
        ApplyDefaultOutlineSettings(selectionOutline);
    }

    /// <summary>
    /// Stronger highlight applied to the specific component
    /// under the cursor during a drag-to-replace or placement
    /// hover. Works even when isConfirmed.
    /// </summary>
    public void HighlightDragTarget()
    {
        EnsureSelectionOutline();
        if (selectionOutline != null)
        {
            ApplyHighlightOutlineSettings(
                selectionOutline, dragTargetOutlineColor);
        }
    }

    /// <summary>
    /// Reverts <see cref="HighlightDragTarget"/>. Restores the
    /// appropriate outline state depending on whether the
    /// component is currently Selected.
    /// </summary>
    public void UnhighlightDragTarget()
    {
        EnsureSelectionOutline();
        if (isConfirmed)
        {
            ApplyHighlightOutlineSettings(
                selectionOutline, confirmOutlineColor);
        }
        else
        {
            ApplyDefaultOutlineSettings(selectionOutline);
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Ball>())
        {
            ballHits++;
            if (enableHitCountPopup)
                SpawnBoardHitCountPopup(ballHits, 0);
        }
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponent<Ball>())
        {
            ballHits++;
            if (enableHitCountPopup)
                SpawnBoardHitCountPopup(ballHits, 0);
        }
    }

    virtual public void AddScore()
    {
        scoreManager.AddScore(
            amountToScore, typeOfScore, transform);
    }

    protected void SpawnBoardHitCountPopup(
        int current, int total)
    {
        if (floatingTextSpawner == null)
            floatingTextSpawner =
                ServiceLocator.Get<FloatingTextSpawner>();
        if (floatingTextSpawner == null) return;

        string text = current.ToString();
        floatingTextSpawner.SpawnText(
            transform.position,
            text,
            hitCountFontAsset,
            hitCountPopupScale,
            hitCountPopupOffset,
            hitCountPopupColor);
    }

    public int CompareTo(BoardComponent otherComponent)
    {
        if (otherComponent.transform.position.z >
            transform.position.z)
        {
            return 1;
        }
        else if (otherComponent.transform.position.z <
            transform.position.z)
        {
            return -1;
        }
        else if (otherComponent.transform.position.x >
            transform.position.x)
        {
            return -1;
        }
        else
        {
            return 1;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(componentGuid))
        {
            componentGuid = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
