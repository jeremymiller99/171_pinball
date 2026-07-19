using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum StarMapDetailLevel
{
    /// <summary>Zoomed out: clusters shown as bordered territories, no individual stars.</summary>
    Regions,
    /// <summary>Zoomed in on one territory: its individual stars and their links.</summary>
    Stars,
}

/// <summary>
/// Builds a two-level holographic star map into a world-space canvas at runtime.
///
/// LEVEL 1 (Regions) — cluster centres are scattered with Poisson-disk sampling,
/// then a Voronoi partition carves the map into one territory per cluster. The
/// cells tile the area with no gaps or overlaps, so they read like countries on
/// a map. Clicking a territory drills into it.
///
/// LEVEL 2 (Stars) — the chosen territory's individual stars, placed by a second
/// much finer Poisson pass and filtered to a disc around the cluster centre with
/// a core-to-edge density falloff. Filtering one global sample set (rather than
/// sampling each cluster separately) keeps the minimum-spacing guarantee across
/// territory borders, so neighbouring clusters never produce overlapping stars.
///
/// Links are intra-territory only: travel between clusters happens by backing
/// out to the region view, not by hopping across the gap between them.
///
/// Everything is seeded, so a given seed always rebuilds the identical map.
///
/// Drop this on a RectTransform inside the Nav Table Canvas and press Play.
/// Needs no art assets: node sprites are generated procedurally.
/// </summary>
public class StarMapGenerator : MonoBehaviour
{
    [Header("Viewport / Content")]
    [Tooltip("The visible window. Defaults to this object's RectTransform. Gets a RectMask2D so the map clips at its edge.")]
    [SerializeField] RectTransform _viewport;
    [Tooltip("Size of the whole star field. The region view zooms out to frame all of it.")]
    [SerializeField] Vector2 _contentSize = new Vector2(828f, 714f);
    [Tooltip("Inset from the content edges. Territories tile the area inside this inset.")]
    [SerializeField] float _padding = 20f;

    [Header("Zoom")]
    [Tooltip("Zoom used when drilled into a single territory. 1 = content's native scale. The region view's zoom is computed automatically to frame the whole map.")]
    [SerializeField] float _starViewZoom = 1f;
    [Tooltip("Extra margin around the map in the zoomed-out region view.")]
    [Range(0.5f, 1f)] [SerializeField] float _regionViewFill = 0.95f;

    [Header("Clusters / Territories")]
    [SerializeField] int _clusterCount = 6;
    [Tooltip("Minimum spacing between cluster centres.")]
    [SerializeField] float _clusterSpacing = 160f;
    [Tooltip("How far stars can sit from their cluster centre.")]
    [SerializeField] float _clusterRadius = 60f;
    [Tooltip("How far a territory extends from its centre where it faces open space. Where two territories meet they share a straight border instead. Must exceed Cluster Radius or stars fall outside their own territory.")]
    [SerializeField] float _territoryReach = 110f;
    [Tooltip("Chance of keeping a star at the cluster's outer edge (the core is always 1). Lower = tighter, more defined clusters.")]
    [Range(0f, 1f)] [SerializeField] float _edgeDensity = 0.15f;
    [Tooltip("Curve of the core-to-edge falloff. >1 keeps the core denser for longer.")]
    [SerializeField] float _falloffPower = 1.6f;

    [Header("Territory Appearance")]
    [SerializeField] float _borderWidth = 2f;
    [Tooltip("Fractal detail levels on each border. 0 = straight Voronoi edges, 3 = 8 segments per edge. Above 4 gets expensive for little visual gain.")]
    [Range(0, 5)] [SerializeField] int _borderDetail = 3;
    [Tooltip("How far a shared border between two territories wanders, as a fraction of its own length. Past ~0.2 borders start to self-intersect and the fill breaks up.")]
    [Range(0f, 0.3f)] [SerializeField] float _borderRoughness = 0.1f;
    [Tooltip("How far an open-space edge wanders. These border nothing, so they take far more distortion than shared borders — this is what keeps Territory Reach from reading as a circle.")]
    [Range(0f, 0.6f)] [SerializeField] float _outerRoughness = 0.3f;
    [Tooltip("Sides used for the open-space outline before roughening. Low values (6-10) give an irregular blob; high values look like a circle.")]
    [Range(5, 32)] [SerializeField] int _outerSegments = 9;
    [Tooltip("Alpha of a territory's fill in the zoomed-out region view.")]
    [Range(0f, 1f)] [SerializeField] float _regionFillAlpha = 0.12f;
    [Tooltip("Alpha of territory fills once drilled in, so they read as context.")]
    [Range(0f, 1f)] [SerializeField] float _regionFadedAlpha = 0.04f;
    [Tooltip("Saturation/value of the generated per-territory hues.")]
    [SerializeField] float _regionSaturation = 0.55f;

    [Header("Placement")]
    [Tooltip("Minimum spacing between stars, in canvas units.")]
    [SerializeField] float _nodeSpacing = 26f;
    [SerializeField] int _seed = 12345;
    [Tooltip("0 = as many as the clusters produce.")]
    [SerializeField] int _maxNodes = 90;
    [Tooltip("Regenerate with a fresh random seed every time the scene starts.")]
    [SerializeField] bool _randomiseSeedOnPlay = false;

    [Header("Links")]
    [Tooltip("Stars closer than nodeSpacing * this are linked. ~1.8 gives a good web.")]
    [SerializeField] float _linkRadiusMultiplier = 1.8f;
    [SerializeField] int _maxLinksPerNode = 4;
    [SerializeField] float _linkWidth = 0.8f;
    [SerializeField] Color _linkColor = new Color(0.35f, 0.85f, 1f, 0.3f);

    [Header("Node Appearance")]
    [SerializeField] float _nodeSize = 7f;
    [Tooltip("Glow halo diameter as a multiple of node size.")]
    [SerializeField] float _glowScale = 3.2f;
    [SerializeField] float _pulseSpeed = 1.6f;

    [Header("Node Type Colors")]
    [SerializeField] Color _startColor    = new Color(0.4f, 1f,    0.6f);
    [SerializeField] Color _standardColor = new Color(0.4f, 0.9f,  1f);
    [SerializeField] Color _eliteColor    = new Color(1f,   0.75f, 0.3f);
    [SerializeField] Color _shopColor     = new Color(0.75f, 0.5f, 1f);
    [SerializeField] Color _bossColor     = new Color(1f,   0.35f, 0.4f);

    [Header("Type Distribution")]
    [Range(0f, 1f)] [SerializeField] float _eliteChance = 0.18f;
    [Range(0f, 1f)] [SerializeField] float _shopChance  = 0.12f;

    [Header("Selection")]
    [SerializeField] Color _selectedLinkColor = new Color(0.6f, 1f, 1f, 0.9f);

    [Header("Back Button")]
    [Tooltip("Size of the back button, in canvas units.")]
    [SerializeField] Vector2 _backButtonSize = new Vector2(52f, 16f);
    [Tooltip("Inset from the viewport's top-left corner.")]
    [SerializeField] Vector2 _backButtonMargin = new Vector2(8f, 8f);
    [SerializeField] float _backButtonLabelSize = 9f;

    [Header("Tooltip")]
    [SerializeField] bool _showTooltips = true;
    [SerializeField] float _tooltipFontSize = 7f;

    [Header("Missions")]
    [Tooltip("Open a mission briefing when a star is clicked.")]
    [SerializeField] bool _showMissionPanel = true;
    [Tooltip("Separate canvas that hosts the briefing. The panel is built inside it and the whole canvas is toggled on and off as stars are opened and closed. Leave empty to overlay the briefing on the map itself instead.")]
    [SerializeField] RectTransform _starInfoCanvas;
    [Tooltip("Optional. When set, opening a briefing lerps the camera to its sixth point, and closing one returns it to the fifth point (where the star map lives).")]
    [SerializeField] CameraLerpBetweenPoints _cameraLerp;
    [Tooltip("Leave empty to load every BoardDefinition from Resources/BoardDefinitions.")]
    [SerializeField] BoardDefinition[] _boards;
    [Tooltip("Leave empty to load every PlayerShipDefinition from Resources/PlayerShipDefinitions.")]
    [SerializeField] PlayerShipDefinition[] _ships;

    /// <summary>A cluster: its territory polygon plus the stars inside it.</summary>
    class Territory
    {
        public int Index;
        public Vector2 Centre;
        public Color Hue;
        public StarMapRegion Region;
        public readonly List<StarMapNode> Stars = new List<StarMapNode>();
        public readonly List<Vector2Int> Edges = new List<Vector2Int>();   // indices into Stars
    }

    readonly List<Territory> _territories = new List<Territory>();

    RectTransform _content;
    RectTransform _regionLayer;
    RectTransform _starLayer;
    StarMapLinkRenderer _linkRenderer;
    StarMapFocuser _focuser;
    StarMapBackdrop _backdrop;
    StarMapBackButton _backButton;
    StarMapTooltip _tooltip;
    StarMapMissionPanel _missionPanel;
    List<StarMapMissionCatalog.Assignment> _assignments;
    Sprite _coreSprite;
    Sprite _glowSprite;

    StarMapDetailLevel _detailLevel = StarMapDetailLevel.Regions;
    Territory _activeTerritory;
    StarMapNode _selectedNode;
    float _regionViewZoom = 0.5f;
    Vector2 _mapCentre;

    /// <summary>Fires when a territory is opened at the region level.</summary>
    public event System.Action<int> RegionOpened;
    /// <summary>Fires when the player commits to a mission from the briefing panel.</summary>
    public event System.Action<MissionLaunchRequest> MissionLaunched;
    /// <summary>Fires when the player clicks a star. Hook your run logic here.</summary>
    public event System.Action<StarMapNode> NodeSelected;

    public StarMapDetailLevel DetailLevel { get { return _detailLevel; } }
    public RectTransform Content { get { return _content; } }

    void Awake()
    {
        if (_viewport == null)
            _viewport = GetComponent<RectTransform>();
    }

    void Start()
    {
        if (_randomiseSeedOnPlay)
            _seed = Random.Range(int.MinValue, int.MaxValue);

        Generate();
    }

    [ContextMenu("Regenerate")]
    public void Generate()
    {
        if (_viewport == null)
        {
            Debug.LogError("[StarMapGenerator] No viewport assigned and no RectTransform found.", this);
            return;
        }

        ClearMap();
        EnsureSprites();
        EnsureCatalog();
        EnsureRig();

        Rect rect = _content.rect;
        Vector2 area = new Vector2(rect.width - _padding * 2f, rect.height - _padding * 2f);

        if (area.x <= _nodeSpacing || area.y <= _nodeSpacing)
        {
            Debug.LogError(string.Format(
                "[StarMapGenerator] Usable area is {0}x{1} (content {2}x{3} minus {4} padding) " +
                "but node spacing is {5}. Increase Content Size, or reduce Padding / Node Spacing.",
                area.x, area.y, rect.width, rect.height, _padding, _nodeSpacing), this);
            return;
        }

        // Territories tile this rect, in the content's centred local space.
        var bounds = new Rect(rect.xMin + _padding, rect.yMin + _padding, area.x, area.y);
        _mapCentre = bounds.center;

        List<Vector2> centres = SampleClusterCentres(bounds);
        if (centres.Count == 0) return;

        // Few, long arc segments: each one then takes a big displacement below,
        // which is what breaks up the circle. A finely sampled arc would just
        // wobble slightly and still read as round.
        List<List<Vector2>> cells = VoronoiPartition.Compute(
            centres, bounds, _territoryReach, _outerSegments);

        // Ragged coastlines instead of straight bisectors. Shared edges stay
        // sealed because the displacement is hashed from the edge itself.
        for (int i = 0; i < cells.Count; i++)
            cells[i] = PolygonUtility.Roughen(cells[i], _borderDetail, _borderRoughness,
                                              bounds, _seed, centres[i], _territoryReach,
                                              _outerRoughness);

        // Frame what actually exists, not the rect it was carved from — the
        // territories are an irregular island in the middle of empty space.
        FrameTerritories(cells);

        BuildTerritories(centres, cells);
        PopulateStars(centres, bounds);

        ShowRegionView(true);
    }

    public void ClearMap()
    {
        for (int i = 0; i < _territories.Count; i++)
        {
            Territory t = _territories[i];
            if (t.Region != null) DestroyObject(t.Region.gameObject);
            for (int s = 0; s < t.Stars.Count; s++)
                if (t.Stars[s] != null) DestroyObject(t.Stars[s].gameObject);
        }

        _territories.Clear();
        _activeTerritory = null;
        _selectedNode = null;

        if (_linkRenderer != null) _linkRenderer.Clear();
    }

    static void DestroyObject(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    // ------------------------------------------------------- level of detail

    /// <summary>Zoom out to the territory overview.</summary>
    public void ShowRegionView() { ShowRegionView(false); }

    void ShowRegionView(bool immediate)
    {
        _detailLevel = StarMapDetailLevel.Regions;
        _activeTerritory = null;
        _selectedNode = null;

        for (int i = 0; i < _territories.Count; i++)
        {
            Territory t = _territories[i];
            SetStarsVisible(t, false);
            if (t.Region == null) continue;
            t.Region.SetInteractive(true);
            t.Region.SetColors(FillColor(t.Hue, _regionFillAlpha), BorderColor(t.Hue));
        }

        _linkRenderer.Clear();

        // Nothing to back out to from the top level.
        _backdrop.SetActive(false);
        _backButton.SetVisible(false);

        // Whatever was hovered is gone or no longer interactive, and turning off
        // raycastTarget under the cursor never fires OnPointerExit.
        if (_tooltip != null) _tooltip.Hide();

        // No star is selected at the top level, so the info screen goes dark.
        if (_missionPanel != null) _missionPanel.Close();

        if (immediate) _focuser.FocusImmediate(_mapCentre, _regionViewZoom);
        else _focuser.FocusOn(_mapCentre, _regionViewZoom);
    }

    /// <summary>Drill into one territory and reveal its stars.</summary>
    public void ShowStarView(int territoryIndex)
    {
        if (territoryIndex < 0 || territoryIndex >= _territories.Count) return;

        _detailLevel = StarMapDetailLevel.Stars;
        _activeTerritory = _territories[territoryIndex];
        _selectedNode = null;

        for (int i = 0; i < _territories.Count; i++)
        {
            Territory t = _territories[i];
            bool active = t == _activeTerritory;
            SetStarsVisible(t, active);

            if (t.Region == null) continue;
            // Territories become non-interactive context once drilled in;
            // backing out is handled by the backdrop, not by clicking a cell.
            t.Region.SetInteractive(false);
            t.Region.SetColors(
                FillColor(t.Hue, active ? _regionFillAlpha : _regionFadedAlpha),
                BorderColor(t.Hue));
        }

        DrawLinks(_activeTerritory);
        _backdrop.SetActive(true);
        _backButton.SetVisible(true);

        // The region being drilled into just stopped accepting pointer events,
        // so its tooltip would otherwise linger.
        if (_tooltip != null) _tooltip.Hide();

        // Entering a territory clears any briefing left from the last one.
        if (_missionPanel != null) _missionPanel.Close();

        _focuser.FocusOn(_activeTerritory.Centre, _starViewZoom);

        var handler = RegionOpened;
        if (handler != null) handler(territoryIndex);
    }

    static void SetStarsVisible(Territory territory, bool visible)
    {
        for (int i = 0; i < territory.Stars.Count; i++)
            if (territory.Stars[i] != null)
                territory.Stars[i].gameObject.SetActive(visible);
    }

    /// <summary>
    /// Sets the zoomed-out framing from the territories' real extent, so the
    /// region view isn't padded out by the empty rect they were carved from.
    /// </summary>
    void FrameTerritories(List<List<Vector2>> cells)
    {
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        bool any = false;

        for (int i = 0; i < cells.Count; i++)
        {
            for (int j = 0; j < cells[i].Count; j++)
            {
                min = Vector2.Min(min, cells[i][j]);
                max = Vector2.Max(max, cells[i][j]);
                any = true;
            }
        }

        if (!any)
        {
            _mapCentre = Vector2.zero;
            _regionViewZoom = 1f;
            return;
        }

        _mapCentre = (min + max) * 0.5f;

        Vector2 extent = Vector2.Max(max - min, Vector2.one);
        _regionViewZoom = Mathf.Min(_viewport.rect.width / extent.x,
                                    _viewport.rect.height / extent.y) * _regionViewFill;
    }

    // ------------------------------------------------------------ clustering

    List<Vector2> SampleClusterCentres(Rect bounds)
    {
        // Inset by the full reach, so a territory's rounded edge never runs into
        // the bounds rect and gets flattened back into a straight map border.
        float inset = Mathf.Max(_clusterRadius, _territoryReach);
        Vector2 centreArea = new Vector2(
            Mathf.Max(1f, bounds.width - inset * 2f),
            Mathf.Max(1f, bounds.height - inset * 2f));

        List<Vector2> centres = PoissonDiskSampler.Sample(
            centreArea, _clusterSpacing, _seed, _clusterCount);

        if (centres.Count == 0)
        {
            Debug.LogWarning("[StarMapGenerator] No cluster centres fit — lower Cluster Spacing or Cluster Radius.", this);
            return centres;
        }

        // A single centre has no neighbour to be clipped against, so its Voronoi
        // cell is the whole rect — the map renders as one plain square. Say so
        // explicitly, because the symptom doesn't obviously point at spacing.
        if (centres.Count < _clusterCount)
        {
            Debug.LogWarning(string.Format(
                "[StarMapGenerator] Only {0} of {1} cluster centres fit. Usable centre area is " +
                "{2:0}x{3:0} (content {4:0}x{5:0} minus {6:0} padding and {7:0} territory reach, " +
                "both doubled) but Cluster Spacing is {8:0}. Aim for at least ~2.5x the spacing " +
                "on each axis. Raise Content Size, or lower Cluster Spacing / Territory Reach / Padding.{9}",
                centres.Count, _clusterCount, centreArea.x, centreArea.y,
                _contentSize.x, _contentSize.y, _padding, inset, _clusterSpacing,
                centres.Count == 1 ? " With one centre the whole map is a single square territory." : ""),
                this);
        }

        Vector2 origin = new Vector2(bounds.xMin + inset, bounds.yMin + inset);
        for (int i = 0; i < centres.Count; i++)
            centres[i] += origin;

        return centres;
    }

    void BuildTerritories(List<Vector2> centres, List<List<Vector2>> cells)
    {
        for (int i = 0; i < centres.Count; i++)
        {
            var territory = new Territory
            {
                Index = i,
                Centre = centres[i],
                // Spread hues evenly so adjacent territories stay distinguishable.
                Hue = Color.HSVToRGB((float)i / Mathf.Max(1, centres.Count), _regionSaturation, 1f),
            };

            var go = new GameObject("Region_" + i, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_regionLayer, false);
            Stretch(rt);

            var region = go.AddComponent<StarMapRegion>();
            region.Initialise(i, centres[i], cells[i],
                              FillColor(territory.Hue, _regionFillAlpha),
                              BorderColor(territory.Hue), _borderWidth);
            region.DisplayName = StarMapNaming.Territory(_seed, i);
            region.Clicked += HandleRegionClicked;
            region.HoverChanged += HandleRegionHover;

            territory.Region = region;
            _territories.Add(territory);
        }
    }

    Color FillColor(Color hue, float alpha)
    {
        return new Color(hue.r, hue.g, hue.b, alpha);
    }

    static Color BorderColor(Color hue)
    {
        return new Color(hue.r, hue.g, hue.b, 0.85f);
    }

    // ---------------------------------------------------------------- stars

    /// <summary>
    /// One global fine Poisson pass, split between territories by nearest centre
    /// with a core-to-edge density falloff. Sampling globally rather than
    /// per-cluster is what keeps spacing valid across territory borders.
    /// </summary>
    void PopulateStars(List<Vector2> centres, Rect bounds)
    {
        List<Vector2> candidates = PoissonDiskSampler.Sample(
            new Vector2(bounds.width, bounds.height), _nodeSpacing, _seed ^ 0x1b873593);

        Vector2 origin = new Vector2(bounds.xMin, bounds.yMin);
        var rng = new System.Random(_seed ^ 0x27d4eb2f);
        float sqrRadius = _clusterRadius * _clusterRadius;

        // Bucket accepted points per territory before creating any GameObjects.
        var buckets = new List<List<Vector2>>(centres.Count);
        var anchors = new int[centres.Count];
        var anchorDistance = new float[centres.Count];
        for (int i = 0; i < centres.Count; i++)
        {
            buckets.Add(new List<Vector2>());
            anchors[i] = -1;
            anchorDistance[i] = float.MaxValue;
        }

        int accepted = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2 point = candidates[i] + origin;

            int nearest = -1;
            float nearestSqr = float.MaxValue;
            for (int c = 0; c < centres.Count; c++)
            {
                float d = (point - centres[c]).sqrMagnitude;
                if (d < nearestSqr) { nearestSqr = d; nearest = c; }
            }

            if (nearest < 0 || nearestSqr > sqrRadius) continue;

            float t = Mathf.Sqrt(nearestSqr) / _clusterRadius;   // 0 core .. 1 edge
            float keepChance = Mathf.Lerp(1f, _edgeDensity, Mathf.Pow(t, _falloffPower));

            // Always keep each territory's most central candidate, so a cluster
            // can never be left empty — by unlucky rolls or by the node cap
            // running out before this territory got its turn.
            bool isAnchor = nearestSqr < anchorDistance[nearest];
            if (isAnchor)
            {
                anchorDistance[nearest] = nearestSqr;
                anchors[nearest] = buckets[nearest].Count;
            }
            else if (_maxNodes > 0 && accepted >= _maxNodes)
            {
                continue;
            }
            else if (rng.NextDouble() > keepChance)
            {
                continue;
            }

            buckets[nearest].Add(point);
            accepted++;
        }

        for (int i = 0; i < _territories.Count; i++)
            BuildTerritoryStars(_territories[i], buckets[i], anchors[i], rng);
    }

    void BuildTerritoryStars(Territory territory, List<Vector2> points, int anchorIndex, System.Random rng)
    {
        if (points.Count == 0)
        {
            Debug.LogWarning(string.Format(
                "[StarMapGenerator] Territory {0} got no stars — raise Cluster Radius or Edge Density.",
                territory.Index), this);
            return;
        }

        // Furthest-apart pair inside this territory becomes its start and boss.
        int startIndex, bossIndex;
        FindExtremes(points, out startIndex, out bossIndex);

        for (int i = 0; i < points.Count; i++)
        {
            StarMapNodeType type;
            if (i == startIndex)     type = StarMapNodeType.Start;
            else if (i == bossIndex) type = StarMapNodeType.Boss;
            else
            {
                double roll = rng.NextDouble();
                if (roll < _eliteChance)                    type = StarMapNodeType.Elite;
                else if (roll < _eliteChance + _shopChance) type = StarMapNodeType.Shop;
                else                                        type = StarMapNodeType.Standard;
            }

            territory.Stars.Add(CreateNode(territory, i, type, points[i]));
        }

        BuildEdges(territory, points);
        SetStarsVisible(territory, false);

        if (territory.Region != null)
            territory.Region.StarCount = territory.Stars.Count;
    }

    StarMapNode CreateNode(Territory territory, int index, StarMapNodeType type, Vector2 localPos)
    {
        Color color = ColorFor(type);
        float size = _nodeSize * SizeMultiplierFor(type);

        var go = new GameObject("Region" + territory.Index + "_Node" + index + "_" + type,
                                typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_starLayer, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = localPos;

        // Glow sits behind and is deliberately non-interactive so it never
        // steals pointer events from the small core.
        var glowGO = new GameObject("Glow", typeof(RectTransform));
        var glowRT = (RectTransform)glowGO.transform;
        glowRT.SetParent(rt, false);
        Stretch(glowRT);
        glowRT.localScale = Vector3.one * _glowScale;
        var glow = glowGO.AddComponent<Image>();
        glow.sprite = _glowSprite;
        glow.raycastTarget = false;

        var coreGO = new GameObject("Core", typeof(RectTransform));
        var coreRT = (RectTransform)coreGO.transform;
        coreRT.SetParent(rt, false);
        Stretch(coreRT);
        var core = coreGO.AddComponent<Image>();
        core.sprite = _coreSprite;
        core.raycastTarget = true;   // this is the click target

        var node = go.AddComponent<StarMapNode>();
        node.Initialise(index, type, localPos, core, glow, color, _pulseSpeed);
        node.DisplayName = StarMapNaming.Star(_seed, territory.Index, index);

        StarMapMissionCatalog.Assignment assignment =
            StarMapMissionCatalog.Pick(_assignments, _seed, territory.Index, index);
        node.Board = assignment.Board;
        node.Mission = assignment.Mission;

        node.Clicked += HandleNodeClicked;
        node.HoverChanged += HandleNodeHover;

        return node;
    }

    Color ColorFor(StarMapNodeType type)
    {
        switch (type)
        {
            case StarMapNodeType.Start: return _startColor;
            case StarMapNodeType.Elite: return _eliteColor;
            case StarMapNodeType.Shop:  return _shopColor;
            case StarMapNodeType.Boss:  return _bossColor;
            default:                    return _standardColor;
        }
    }

    static float SizeMultiplierFor(StarMapNodeType type)
    {
        switch (type)
        {
            case StarMapNodeType.Boss:  return 1.7f;
            case StarMapNodeType.Start: return 1.35f;
            case StarMapNodeType.Elite: return 1.25f;
            default:                    return 1f;
        }
    }

    static void FindExtremes(List<Vector2> points, out int a, out int b)
    {
        a = 0; b = points.Count > 1 ? 1 : 0;
        float best = -1f;
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                float d = (points[i] - points[j]).sqrMagnitude;
                if (d > best) { best = d; a = i; b = j; }
            }
        }
    }

    // ---------------------------------------------------------------- links

    /// <summary>
    /// Links within one territory only. Nearest pairs first, so the per-node cap
    /// keeps the shortest links; then union-find bridges any leftover islands so
    /// the territory's own web is always fully connected.
    /// </summary>
    void BuildEdges(Territory territory, List<Vector2> points)
    {
        float linkRadius = _nodeSpacing * _linkRadiusMultiplier;
        float sqrRadius = linkRadius * linkRadius;

        var linkCount = new int[points.Count];
        var seen = new HashSet<long>();

        var candidates = new List<KeyValuePair<float, Vector2Int>>();
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                float d = (points[i] - points[j]).sqrMagnitude;
                if (d <= sqrRadius)
                    candidates.Add(new KeyValuePair<float, Vector2Int>(d, new Vector2Int(i, j)));
            }
        }
        candidates.Sort((x, y) => x.Key.CompareTo(y.Key));

        for (int c = 0; c < candidates.Count; c++)
        {
            Vector2Int e = candidates[c].Value;
            if (linkCount[e.x] >= _maxLinksPerNode || linkCount[e.y] >= _maxLinksPerNode)
                continue;
            AddEdge(territory, e.x, e.y, linkCount, seen);
        }

        var uf = new UnionFind(points.Count);
        for (int i = 0; i < territory.Edges.Count; i++)
            uf.Union(territory.Edges[i].x, territory.Edges[i].y);

        while (uf.ComponentCount > 1)
        {
            float best = float.MaxValue;
            var bridge = new Vector2Int(-1, -1);

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    if (uf.Find(i) == uf.Find(j)) continue;
                    float d = (points[i] - points[j]).sqrMagnitude;
                    if (d < best) { best = d; bridge = new Vector2Int(i, j); }
                }
            }

            if (bridge.x < 0) break;   // shouldn't happen, but never spin forever
            AddEdge(territory, bridge.x, bridge.y, linkCount, seen);
            uf.Union(bridge.x, bridge.y);
        }
    }

    void AddEdge(Territory territory, int a, int b, int[] linkCount, HashSet<long> seen)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        if (!seen.Add(key)) return;

        territory.Edges.Add(new Vector2Int(a, b));
        linkCount[a]++;
        linkCount[b]++;
        territory.Stars[a].Neighbours.Add(territory.Stars[b]);
        territory.Stars[b].Neighbours.Add(territory.Stars[a]);
    }

    void DrawLinks(Territory territory)
    {
        _linkRenderer.Clear();
        if (territory == null) return;

        for (int i = 0; i < territory.Edges.Count; i++)
        {
            Vector2Int e = territory.Edges[i];
            _linkRenderer.AddLink(territory.Stars[e.x].MapPosition,
                                  territory.Stars[e.y].MapPosition,
                                  _linkColor, _linkWidth);
        }
    }

    // ------------------------------------------------------------ selection

    void HandleRegionClicked(StarMapRegion region)
    {
        if (_detailLevel != StarMapDetailLevel.Regions) return;
        ShowStarView(region.Index);
    }

    void HandleRegionHover(StarMapRegion region, bool entered)
    {
        if (!_showTooltips || _tooltip == null) return;

        if (!entered)
        {
            _tooltip.Hide();
            return;
        }

        string body = string.Format("{0}\n{1} SYSTEM{2}",
            region.DisplayName, region.StarCount, region.StarCount == 1 ? "" : "S");

        _tooltip.Show(body, VoronoiPartition.Centroid(region.Polygon));
    }

    void HandleNodeHover(StarMapNode node, bool entered)
    {
        if (!_showTooltips || _tooltip == null) return;

        if (!entered)
        {
            _tooltip.Hide();
            return;
        }

        _tooltip.Show(string.Format("{0}\n{1}", node.DisplayName,
                                    StarMapNaming.TypeLabel(node.NodeType)),
                      node.MapPosition);
    }

    void HandleNodeClicked(StarMapNode node)
    {
        if (_activeTerritory == null) return;

        if (_selectedNode != null) _selectedNode.Selected = false;
        _selectedNode = node;
        node.Selected = true;

        _focuser.FocusOn(node.MapPosition, _starViewZoom);

        // Light up only the links touching the selected star.
        for (int i = 0; i < _activeTerritory.Edges.Count; i++)
        {
            Vector2Int e = _activeTerritory.Edges[i];
            bool touches = e.x == node.Index || e.y == node.Index;
            _linkRenderer.SetLinkColor(i, touches ? _selectedLinkColor : _linkColor);
        }

        if (_showTooltips && _tooltip != null) _tooltip.Hide();
        if (_showMissionPanel && _missionPanel != null) _missionPanel.Open(node);

        var handler = NodeSelected;
        if (handler != null) handler(node);
    }

    /// <summary>
    /// The star-info canvas just switched on, so bring the camera in to it.
    /// Driven by the canvas toggle rather than by the click, so the camera and
    /// the screen can never disagree about which one is showing.
    /// </summary>
    void HandleMissionPanelOpened()
    {
        if (_cameraLerp != null) _cameraLerp.GoToSixth();
    }

    /// <summary>
    /// The canvas switched off, so pull back to the star map — however it
    /// closed: the X, clicking away, backing out, or opening another territory.
    /// </summary>
    void HandleMissionPanelClosed()
    {
        if (_cameraLerp != null) _cameraLerp.GoToFifth();
    }

    void HandleMissionLaunched(MissionLaunchRequest request)
    {
        var handler = MissionLaunched;
        if (handler != null) handler(request);
    }

    // -------------------------------------------------------------- plumbing

    /// <summary>
    /// Loads the real board / mission assets and flattens them into the pool
    /// stars draw from.
    /// </summary>
    void EnsureCatalog()
    {
        List<BoardDefinition> boards = StarMapMissionCatalog.LoadBoards(_boards);
        _assignments = StarMapMissionCatalog.BuildAssignments(boards);

        if (_assignments.Count == 0)
        {
            Debug.LogWarning(string.Format(
                "[StarMapGenerator] No BoardDefinitions found. Assign them on the component, " +
                "or put them under Resources/{0}. Stars will have no mission attached.",
                StarMapMissionCatalog.BoardResourcePath), this);
            return;
        }

        int playable = 0;
        for (int i = 0; i < _assignments.Count; i++)
            if (_assignments[i].IsPlayable) playable++;

        if (playable == 0)
        {
            Debug.LogWarning(string.Format(
                "[StarMapGenerator] {0} board(s) loaded but none has a mission in its Missions " +
                "array, so no star is launchable. Add a Challenge Mode to a BoardDefinition.",
                _assignments.Count), this);
        }
        else if (playable < _assignments.Count)
        {
            Debug.Log(string.Format(
                "[StarMapGenerator] {0} of {1} board/mission pairings are playable; the rest " +
                "are boards with an empty Missions array and will show as unsurveyed.",
                playable, _assignments.Count), this);
        }
    }

    /// <summary>
    /// Builds the viewport rig: clipping mask, backdrop, the zooming content,
    /// its region and star layers, and the focuser that drives the camera.
    /// </summary>
    void EnsureRig()
    {
        if (_viewport.GetComponent<RectMask2D>() == null)
            _viewport.gameObject.AddComponent<RectMask2D>();

        _backdrop = _viewport.GetComponentInChildren<StarMapBackdrop>(true);
        if (_backdrop == null)
        {
            var go = new GameObject("Backdrop", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_viewport, false);
            Stretch(rt);
            _backdrop = go.AddComponent<StarMapBackdrop>();
        }
        _backdrop.transform.SetAsFirstSibling();
        _backdrop.Configure(ShowRegionView);

        _content = EnsureChild(_viewport, "Content");
        _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0.5f);
        _content.pivot = new Vector2(0.5f, 0.5f);
        _content.sizeDelta = _contentSize;

        // Regions sit under the links, which sit under the stars.
        _regionLayer = EnsureChild(_content, "Regions");
        Stretch(_regionLayer);
        _regionLayer.SetSiblingIndex(0);

        RectTransform linkLayer = EnsureChild(_content, "Links");
        Stretch(linkLayer);
        linkLayer.SetSiblingIndex(1);
        _linkRenderer = linkLayer.GetComponent<StarMapLinkRenderer>();
        if (_linkRenderer == null)
            _linkRenderer = linkLayer.gameObject.AddComponent<StarMapLinkRenderer>();
        _linkRenderer.raycastTarget = false;

        _starLayer = EnsureChild(_content, "Stars");
        Stretch(_starLayer);
        _starLayer.SetSiblingIndex(2);

        // Overlay: lives on the viewport and is drawn last, so it stays fixed
        // and on top while the map zooms underneath it.
        RectTransform backRect = EnsureChild(_viewport, "BackButton");
        backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 1f);
        backRect.sizeDelta = _backButtonSize;
        backRect.anchoredPosition = new Vector2(_backButtonMargin.x, -_backButtonMargin.y);
        backRect.SetAsLastSibling();

        _backButton = backRect.GetComponent<StarMapBackButton>();
        if (_backButton == null)
            _backButton = backRect.gameObject.AddComponent<StarMapBackButton>();
        _backButton.Configure(ShowRegionView, _backButtonLabelSize);

        // Tooltip is drawn last of all, so it floats over the back button too.
        RectTransform tooltipRect = EnsureChild(_viewport, "Tooltip");
        tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0f, 0f);   // Reposition() works from the lower-left
        tooltipRect.SetAsLastSibling();

        _tooltip = tooltipRect.GetComponent<StarMapTooltip>();
        if (_tooltip == null)
            _tooltip = tooltipRect.gameObject.AddComponent<StarMapTooltip>();
        _tooltip.Configure(_viewport, _content, _tooltipFontSize);

        EnsureMissionPanel();

        _focuser = _viewport.GetComponent<StarMapFocuser>();
        if (_focuser == null)
            _focuser = _viewport.gameObject.AddComponent<StarMapFocuser>();
        _focuser.Configure(_viewport, _content);
    }

    /// <summary>
    /// Builds the mission briefing, either inside the dedicated star-info canvas
    /// or, if none is assigned, as a modal overlay on the map itself.
    /// </summary>
    void EnsureMissionPanel()
    {
        bool hosted = _starInfoCanvas != null;
        RectTransform host = hosted ? _starInfoCanvas : _viewport;

        // The canvas is normally left switched off in the scene, and UI built
        // under an inactive object can't measure text or lay out. Switch it on
        // for the build; Configure closes the panel, which switches it back off.
        bool wasActive = host.gameObject.activeSelf;
        if (hosted && !wasActive) host.gameObject.SetActive(true);

        RectTransform panelRect = EnsureChild(host, "MissionPanel");
        panelRect.SetAsLastSibling();

        _missionPanel = panelRect.GetComponent<StarMapMissionPanel>();
        if (_missionPanel == null)
            _missionPanel = panelRect.gameObject.AddComponent<StarMapMissionPanel>();

        _missionPanel.MissionLaunched -= HandleMissionLaunched;
        _missionPanel.MissionLaunched += HandleMissionLaunched;
        _missionPanel.Opened -= HandleMissionPanelOpened;
        _missionPanel.Opened += HandleMissionPanelOpened;
        _missionPanel.Closed -= HandleMissionPanelClosed;
        _missionPanel.Closed += HandleMissionPanelClosed;

        _missionPanel.Configure(host, StarMapMissionCatalog.LoadShips(_ships),
                                hosted ? host.gameObject : null);
    }

    static RectTransform EnsureChild(RectTransform parent, string name)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null) return existing;

        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Builds the node sprites in code so the prototype needs no imported art.
    /// Core is a crisp disc, glow is a soft radial falloff.
    /// </summary>
    void EnsureSprites()
    {
        if (_coreSprite == null) _coreSprite = CreateRadialSprite(64, 0.9f, 1.0f);
        if (_glowSprite == null) _glowSprite = CreateRadialSprite(128, 0f, 2.2f);
    }

    static Sprite CreateRadialSprite(int size, float solidRadius, float falloffPower)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 0 at centre, 1 at the inscribed circle's edge
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float a;
                if (d <= solidRadius)
                    a = 1f;
                else if (d >= 1f)
                    a = 0f;
                else
                {
                    float t = (d - solidRadius) / Mathf.Max(0.0001f, 1f - solidRadius);
                    a = Mathf.Pow(1f - t, falloffPower);
                }

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Minimal union-find used to keep each territory's star graph connected.</summary>
    class UnionFind
    {
        readonly int[] _parent;
        public int ComponentCount { get; private set; }

        public UnionFind(int count)
        {
            _parent = new int[count];
            for (int i = 0; i < count; i++) _parent[i] = i;
            ComponentCount = count;
        }

        public int Find(int x)
        {
            while (_parent[x] != x)
            {
                _parent[x] = _parent[_parent[x]];   // path halving
                x = _parent[x];
            }
            return x;
        }

        public void Union(int a, int b)
        {
            int ra = Find(a), rb = Find(b);
            if (ra == rb) return;
            _parent[ra] = rb;
            ComponentCount--;
        }
    }
}
