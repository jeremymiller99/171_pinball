// Created with Claude Code (Opus 4.8) by JJ on 2026-07-16: navigation-table
// prototype — a single selectable star on the holo map. One node == one mission.
using UnityEngine;

/// <summary>
/// One selectable star on the navigation-table holo map. Each star is a single
/// mission/destination: clicking it populates the detail canvas with that
/// mission's info and the ship options. Regions are not nodes — they are
/// overarching zones/labels in the scene — so a star only needs to know which
/// region + mission it maps to for display.
///
/// <see cref="NavigationTableController"/> owns all selection logic and styling;
/// this component only carries identity (<see cref="RegionIndex"/>,
/// <see cref="MissionIndex"/>) and applies a purely-visual state (tint + emission
/// + scale). Uses a <see cref="MaterialPropertyBlock"/> so per-node glow never
/// leaks into the shared material. For emission to actually glow, the star's
/// material must have emission enabled (a property block can set the color but
/// can't toggle the shader keyword).
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public sealed class NavStarNode : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Index of the region (overarching zone) this star sits in — display only.")]
    [SerializeField] private int regionIndex;

    [Tooltip("Index of the mission this star represents, within its region.")]
    [SerializeField] private int missionIndex;

    [Header("Visual")]
    [Tooltip("Renderer tinted/scaled for feedback. Auto-found in children if unset.")]
    [SerializeField] private Renderer targetRenderer;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock _mpb;
    private Vector3 _baseScale = Vector3.one;
    private Collider _collider;

    public int RegionIndex => regionIndex;
    public int MissionIndex => missionIndex;

    /// <summary>True while this star can be hover/click-selected.</summary>
    public bool Selectable => _collider != null && _collider.enabled;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _baseScale = transform.localScale;

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        _mpb = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Applies a visual state: <paramref name="tint"/> is the base/emission color,
    /// <paramref name="emissionIntensity"/> scales the emission (0 = dark), and
    /// <paramref name="scaleMultiplier"/> pulses the star's size for hover/commit.
    /// </summary>
    public void ApplyVisual(Color tint, float emissionIntensity, float scaleMultiplier)
    {
        if (targetRenderer != null)
        {
            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, tint);
            _mpb.SetColor(LegacyColorId, tint);
            _mpb.SetColor(EmissionColorId, tint * Mathf.Max(0f, emissionIntensity));
            targetRenderer.SetPropertyBlock(_mpb);
        }

        transform.localScale = _baseScale * Mathf.Max(0.01f, scaleMultiplier);
    }

    /// <summary>Enables/disables hit-testing for this star.</summary>
    public void SetSelectable(bool value)
    {
        if (_collider != null)
        {
            _collider.enabled = value;
        }
    }
}
