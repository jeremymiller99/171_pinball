using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A designated area of the playfield that holds exactly one swappable
/// <see cref="ComponentGroupDefinition"/> at a time. Acts as the "slot" the shop
/// installs groups into, the way a single <see cref="BoardComponent"/> is the
/// unit the shop replaces in the non-modular flow.
///
/// Requires a trigger collider covering the section footprint so an empty
/// section is still clickable; a click on any child component of an installed
/// group resolves back to this section via GetComponentInParent&lt;BoardSection&gt;().
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class BoardSection : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Only groups with a matching category can be installed here.")]
    [SerializeField] private BoardSectionCategory category = BoardSectionCategory.BumperCluster;
    [Tooltip("Stable GUID for this section. Auto-assigned in the editor if empty.")]
    [SerializeField] private string sectionGuid;

    [Header("Placement")]
    [Tooltip("Parent the installed group is attached under. Defaults to this transform.")]
    [SerializeField] private Transform groupAnchor;

    [Tooltip("Group installed automatically on Start if the section starts empty, " +
             "so the board boots populated through the same install path as purchases. " +
             "Leave empty for a zone that starts empty and is filled later in the shop.")]
    [SerializeField] private ComponentGroupDefinition defaultGroup;

    [Header("Empty state")]
    [Tooltip("Optional custom marker shown while this section has no group installed. " +
             "If left null, an outline of the section footprint is generated automatically " +
             "so the empty zone is visible and can be targeted.")]
    [SerializeField] private GameObject emptyPlaceholder;

    [Tooltip("When no custom Empty Placeholder is assigned, draw an automatic outline of " +
             "the section's BoxCollider footprint while the zone is empty.")]
    [SerializeField] private bool autoGenerateEmptyVisual = true;

    [Tooltip("World-space line thickness of the auto-generated empty-zone outline. " +
             "Leave <= 0 to size it automatically from the section footprint.")]
    [SerializeField] private float emptyVisualLineWidth = 0f;

    private static readonly Color EmptyIdleColor = new Color(0.35f, 0.8f, 1f, 0.85f);
    private static readonly Color EmptyTargetColor = new Color(0.3f, 1f, 0.3f, 1f);
    private const float PlaceholderOutlineWidth = 6f;

    private Outline _placeholderOutline;
    private GameObject _autoVisual;
    private LineRenderer _autoVisualLine;
    private GameObject _groupInstance;
    private ComponentGroupDefinition _currentDef;
    private readonly List<BoardComponent> _groupComponents = new List<BoardComponent>();

    public BoardSectionCategory Category => category;
    public string SectionGuid => sectionGuid;
    public ComponentGroupDefinition CurrentDefinition => _currentDef;
    public GameObject CurrentGroupInstance => _groupInstance;

    private Transform Anchor => groupAnchor != null ? groupAnchor : transform;

    /// <summary>True when no group is currently installed in this section.</summary>
    public bool IsEmpty => _groupInstance == null;

    private void Start()
    {
        // A section may start pre-filled (defaultGroup assigned) or empty (null).
        // Either way it stays a valid, fillable target for the shop.
        if (_groupInstance == null && defaultGroup != null)
        {
            InstallGroup(defaultGroup);
        }
        else
        {
            RefreshEmptyState();
        }
    }

    /// <summary>
    /// Destroys the currently-installed group (if any) and installs the new one
    /// under this section's anchor, keeping the prefab's authored child layout.
    /// </summary>
    public bool InstallGroup(ComponentGroupDefinition def)
    {
        if (def == null)
        {
            Debug.LogWarning($"[BoardSection] InstallGroup on '{name}': null definition.");
            return false;
        }

        if (def.GroupPrefab == null)
        {
            Debug.LogError($"[BoardSection] InstallGroup on '{name}': '{def.GetSafeDisplayName()}' has no group prefab assigned.");
            return false;
        }

        if (_groupInstance != null)
        {
            Destroy(_groupInstance);
            _groupInstance = null;
        }
        _groupComponents.Clear();

        GameObject go = Instantiate(def.GroupPrefab, Anchor);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        _groupInstance = go;
        _currentDef = def;
        go.GetComponentsInChildren(true, _groupComponents);

        RefreshEmptyState();
        return true;
    }

    /// <summary>
    /// Removes the currently-installed group (if any) and returns the section to
    /// its empty state, re-showing the empty placeholder.
    /// </summary>
    public void ClearGroup()
    {
        if (_groupInstance != null)
        {
            Destroy(_groupInstance);
            _groupInstance = null;
        }
        _currentDef = null;
        _groupComponents.Clear();

        RefreshEmptyState();
    }

    /// <summary>Board components belonging to the currently-installed group.</summary>
    public IReadOnlyList<BoardComponent> GroupComponents => _groupComponents;

    // --- Selection / highlight API (mirrors BoardComponent), forwarded to the
    // installed group's components so the existing outline system is reused. ---

    public void PrewarmSelectionOutline()
    {
        foreach (BoardComponent bc in _groupComponents)
            if (bc != null) bc.PrewarmSelectionOutline();

        SetEmptyVisualHighlighted(false);
    }

    public void Select()
    {
        foreach (BoardComponent bc in _groupComponents)
            if (bc != null) bc.Select();

        SetEmptyVisualHighlighted(true);
    }

    public void DeSelect()
    {
        foreach (BoardComponent bc in _groupComponents)
            if (bc != null) bc.DeSelect();

        SetEmptyVisualHighlighted(false);
    }

    public void HighlightDragTarget()
    {
        foreach (BoardComponent bc in _groupComponents)
            if (bc != null) bc.HighlightDragTarget();

        SetEmptyVisualHighlighted(true);
    }

    public void UnhighlightDragTarget()
    {
        foreach (BoardComponent bc in _groupComponents)
            if (bc != null) bc.UnhighlightDragTarget();

        SetEmptyVisualHighlighted(false);
    }

    /// <summary>Shows the empty-zone visual only while no group is installed.</summary>
    private void RefreshEmptyState()
    {
        bool empty = IsEmpty;

        if (emptyPlaceholder != null)
        {
            emptyPlaceholder.SetActive(empty);
            return;
        }

        if (autoGenerateEmptyVisual)
        {
            if (empty) EnsureAutoVisual();
            if (_autoVisual != null) _autoVisual.SetActive(empty);
        }
    }

    /// <summary>Green while this empty zone is a valid/hovered placement target.</summary>
    private void SetEmptyVisualHighlighted(bool highlighted)
    {
        if (!IsEmpty) return;

        if (emptyPlaceholder != null)
        {
            EnsurePlaceholderOutline();
            if (_placeholderOutline != null)
            {
                _placeholderOutline.OutlineMode = Outline.Mode.OutlineVisible;
                _placeholderOutline.OutlineColor = EmptyTargetColor;
                _placeholderOutline.OutlineWidth = PlaceholderOutlineWidth;
                _placeholderOutline.enabled = highlighted;
            }
            return;
        }

        ApplyAutoVisualColor(highlighted ? EmptyTargetColor : EmptyIdleColor);
    }

    private void EnsurePlaceholderOutline()
    {
        if (_placeholderOutline != null || emptyPlaceholder == null) return;

        _placeholderOutline = emptyPlaceholder.GetComponent<Outline>();
        if (_placeholderOutline == null)
            _placeholderOutline = emptyPlaceholder.AddComponent<Outline>();
    }

    /// <summary>
    /// Builds a LineRenderer outline of the section's BoxCollider footprint so an
    /// empty zone reads as a droppable slot without any authored art.
    /// </summary>
    private void EnsureAutoVisual()
    {
        if (_autoVisual != null) return;

        var box = GetComponent<BoxCollider>();
        if (box == null) return;

        _autoVisual = new GameObject("EmptySlotVisual");
        _autoVisual.transform.SetParent(transform, false);
        _autoVisual.transform.localPosition = Vector3.zero;
        _autoVisual.transform.localRotation = Quaternion.identity;
        _autoVisual.transform.localScale = Vector3.one;

        _autoVisualLine = _autoVisual.AddComponent<LineRenderer>();
        _autoVisualLine.useWorldSpace = false;
        _autoVisualLine.loop = true;
        _autoVisualLine.positionCount = 4;
        _autoVisualLine.numCornerVertices = 2;
        _autoVisualLine.alignment = LineAlignment.View;
        _autoVisualLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _autoVisualLine.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) _autoVisualLine.material = new Material(shader);

        Vector3 c = box.center;
        Vector3 s = box.size;
        float hx = s.x * 0.5f;
        float hz = s.z * 0.5f;
        float y = c.y + s.y * 0.5f; // top face, so it sits above the board surface

        _autoVisualLine.SetPosition(0, new Vector3(c.x - hx, y, c.z - hz));
        _autoVisualLine.SetPosition(1, new Vector3(c.x + hx, y, c.z - hz));
        _autoVisualLine.SetPosition(2, new Vector3(c.x + hx, y, c.z + hz));
        _autoVisualLine.SetPosition(3, new Vector3(c.x - hx, y, c.z + hz));

        float width = emptyVisualLineWidth > 0f
            ? emptyVisualLineWidth
            : Mathf.Max(0.05f, Mathf.Min(s.x, s.z) * 0.05f);
        _autoVisualLine.widthMultiplier = width;

        ApplyAutoVisualColor(EmptyIdleColor);
    }

    private void ApplyAutoVisualColor(Color color)
    {
        if (_autoVisualLine == null) return;
        _autoVisualLine.startColor = color;
        _autoVisualLine.endColor = color;
    }

    private void Reset()
    {
        // Make the auto-added collider a trigger so it doesn't affect ball physics.
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(sectionGuid))
        {
            sectionGuid = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
