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
             "so the board boots populated through the same install path as purchases.")]
    [SerializeField] private ComponentGroupDefinition defaultGroup;

    private GameObject _groupInstance;
    private ComponentGroupDefinition _currentDef;
    private readonly List<BoardComponent> _groupComponents = new List<BoardComponent>();

    public BoardSectionCategory Category => category;
    public string SectionGuid => sectionGuid;
    public ComponentGroupDefinition CurrentDefinition => _currentDef;
    public GameObject CurrentGroupInstance => _groupInstance;

    private Transform Anchor => groupAnchor != null ? groupAnchor : transform;

    private void Start()
    {
        if (_groupInstance == null && defaultGroup != null)
        {
            InstallGroup(defaultGroup);
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

        return true;
    }

    /// <summary>Board components belonging to the currently-installed group.</summary>
    public IReadOnlyList<BoardComponent> GroupComponents => _groupComponents;

    // --- Selection / highlight API (mirrors BoardComponent), forwarded to the
    // installed group's components so the existing outline system is reused. ---

    public void PrewarmSelectionOutline()
    {
        foreach (BoardComponent bc in _groupComponents)
            if (bc != null) bc.PrewarmSelectionOutline();
    }

    public void Select()
    {
        foreach (BoardComponent bc in _groupComponents)
            if (bc != null) bc.Select();
    }

    public void DeSelect()
    {
        foreach (BoardComponent bc in _groupComponents)
            if (bc != null) bc.DeSelect();
    }

    public void HighlightDragTarget()
    {
        foreach (BoardComponent bc in _groupComponents)
            if (bc != null) bc.HighlightDragTarget();
    }

    public void UnhighlightDragTarget()
    {
        foreach (BoardComponent bc in _groupComponents)
            if (bc != null) bc.UnhighlightDragTarget();
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
