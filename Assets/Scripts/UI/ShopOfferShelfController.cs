// Generated with Antigravity by jjmil on 2026-03-29.
// Updated with Cursor (Composer) on 2026-04-08: pass merchant visit + player
// shop multiplier into ShopOfferGenerator.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates and displays 3D shop offers on the
/// <see cref="ShopOfferPanel"/> placed in the board scene.
/// Lives in GameplayCore; locates the panel cross-scene via
/// <see cref="ServiceLocator"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UnifiedShopController))]
public sealed class ShopOfferShelfController : MonoBehaviour
{
    [Header("Catalogs")]
    [SerializeField] private List<BallDefinition>
        allBallDefinitions = new List<BallDefinition>();
    [SerializeField] private List<BoardComponentDefinition>
        allComponentDefinitions =
            new List<BoardComponentDefinition>();
    [Tooltip("Buyable component groups. Only groups whose category matches a " +
        "BoardSection present on the loaded board are offered.")]
    [SerializeField] private List<ComponentGroupDefinition>
        allGroupDefinitions =
            new List<ComponentGroupDefinition>();

    [Tooltip("When the loaded board contains BoardSections (a modular/prototype " +
        "board), offer ONLY component groups -- no balls or individual " +
        "components. Boards without sections are unaffected.")]
    [SerializeField] private bool groupsOnlyOnSectionBoards = true;

    [Header("3D Offer Display")]
    [Tooltip(
        "Spacing between 3D offer items along the " +
        "panel's right axis.")]
    [SerializeField] private float offerSpacing = 1.5f;

    [Tooltip("Scale applied to each 3D display prefab.")]
    [SerializeField] private float offerDisplayScale = 0.5f;

    [Tooltip(
        "Vertical offset above the panel anchor where " +
        "items are placed.")]
    [SerializeField] private float offerYOffset = 0f;

    public readonly List<ShopOffer3DEntry> OfferEntries =
        new List<ShopOffer3DEntry>();

    private ShopOfferPanel _panel;
    private ShopOfferGenerator _generator;

    private readonly List<ShopOffer> _currentOffers =
        new List<ShopOffer>();

    private UnifiedShopController _shop;

    private void Awake()
    {
        _shop = GetComponent<UnifiedShopController>();
    }

    public void Initialize()
    {
        _panel = ServiceLocator.Get<ShopOfferPanel>();

        if (_panel == null)
        {
            Debug.LogWarning(
                "[ShopOfferShelf] No ShopOfferPanel " +
                "found in any loaded scene.");
        }

        _generator = new ShopOfferGenerator(
            allBallDefinitions, allComponentDefinitions);
    }

    /// <summary>
    /// Returns the subset of <see cref="allGroupDefinitions"/> whose category
    /// matches one of the supplied <see cref="BoardSection"/>s. An empty/absent
    /// section set yields an empty list, so boards with no sections never offer
    /// groups.
    /// </summary>
    private List<ComponentGroupDefinition> GetGroupsForSections(BoardSection[] sections)
    {
        var result = new List<ComponentGroupDefinition>();
        if (allGroupDefinitions.Count == 0 || sections == null || sections.Length == 0)
            return result;

        var present = new HashSet<BoardSectionCategory>();
        foreach (BoardSection s in sections)
            if (s != null) present.Add(s.Category);

        foreach (ComponentGroupDefinition g in allGroupDefinitions)
            if (g != null && g.IsValid() && present.Contains(g.Category))
                result.Add(g);

        return result;
    }

    public void Cleanup()
    {
        ClearOfferDisplays();
        _panel = null;
    }

    public ShopOffer GetOffer(int offerIndex)
    {
        if (offerIndex < 0
            || offerIndex >= _currentOffers.Count)
        {
            return null;
        }

        return _currentOffers[offerIndex];
    }

    /// <summary>
    /// Resolves a mystery-ball offer to a concrete unlocked ball of the given
    /// rarity. Returns null if no eligible ball exists in the catalog.
    /// </summary>
    public BallDefinition ResolveMysteryBall(BallRarity rarity)
    {
        return MysteryBallResolver.Resolve(rarity, allBallDefinitions);
    }

    public void ConsumeOffer(int offerIndex)
    {
        if (offerIndex < 0
            || offerIndex >= _currentOffers.Count)
        {
            return;
        }

        _currentOffers[offerIndex] = null;

        for (int i = OfferEntries.Count - 1; i >= 0; i--)
        {
            ShopOffer3DEntry entry = OfferEntries[i];
            if (entry == null)
            {
                OfferEntries.RemoveAt(i);
                continue;
            }

            if (entry.OfferIndex == offerIndex)
            {
                OfferEntries.RemoveAt(i);
                Destroy(entry.gameObject);
                break;
            }
        }
    }

#if UNITY_EDITOR
    // Prototype/testing convenience: if no groups were wired into the inspector
    // list, auto-discover every ComponentGroupDefinition asset so the shop still
    // offers groups in the editor. Builds should populate the list explicitly.
    private void EnsureGroupCatalogInEditor()
    {
        if (allGroupDefinitions.Count > 0) return;

        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ComponentGroupDefinition"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<ComponentGroupDefinition>(path);
            if (def != null && !allGroupDefinitions.Contains(def))
                allGroupDefinitions.Add(def);
        }

        if (allGroupDefinitions.Count > 0)
            Debug.Log($"[ShopOfferShelf] Editor fallback: auto-loaded {allGroupDefinitions.Count} " +
                      "ComponentGroupDefinition asset(s) because the inspector list was empty.");
    }
#endif

    public void RebuildOffers()
    {
        ClearOfferDisplays();

#if UNITY_EDITOR
        EnsureGroupCatalogInEditor();
#endif

        if (_generator == null)
        {
            return;
        }

        ShopShipController ship =
            Object.FindFirstObjectByType<ShopShipController>();

        // Shop price multipliers are standardized to 1x for now. The player-ship
        // (PlayerShipDefinition.ShopPriceMultiplier) and visitor-merchant
        // (ShopShipController) multiplier features remain in place but are
        // intentionally bypassed here.
        float combined = 1f;

        int minO = ship != null ? ship.MinOffers : 3;
        int maxO = ship != null ? ship.MaxOffers : 6;

        // Rebuild the generator each visit so the group pool reflects the
        // sections present on the currently-loaded board. On a section/modular
        // board, offer ONLY component groups (no balls or individual
        // components); other boards keep the normal balls + components pool.
        BoardSection[] sections =
            Object.FindObjectsByType<BoardSection>(FindObjectsSortMode.None);
        List<ComponentGroupDefinition> boardGroups = GetGroupsForSections(sections);
        bool sectionBoard = sections.Length > 0;

        _generator = (groupsOnlyOnSectionBoards && sectionBoard)
            ? new ShopOfferGenerator(
                new List<BallDefinition>(),
                new List<BoardComponentDefinition>(),
                boardGroups)
            : new ShopOfferGenerator(
                allBallDefinitions,
                allComponentDefinitions,
                boardGroups);

        _currentOffers.Clear();
        _currentOffers.AddRange(
            _generator.GenerateOffers(
                minO,
                maxO,
                combined));

        int count = _currentOffers.Count;

        for (int i = 0; i < count; i++)
        {
            SpawnOfferDisplay(
                _currentOffers[i], i, count);
        }
    }

    private void SpawnOfferDisplay(
        ShopOffer offer, int index, int totalCount)
    {
        if (offer == null || !offer.IsValid)
        {
            return;
        }

        if (_panel == null)
        {
            return;
        }

        Transform anchor = _panel.ItemAnchor;

        float halfSpan =
            (totalCount - 1) * offerSpacing * 0.5f;

        float centeredOffset =
            (index * offerSpacing) - halfSpan;

        Vector3 pos = anchor.position
            + anchor.right * centeredOffset
            + anchor.up * offerYOffset;

        Quaternion rot = anchor.rotation;

        string label =
            $"ShopOffer_{index}_{offer.DisplayName}";

        GameObject displayGo;

        if (offer.Prefab != null)
        {
            displayGo = Instantiate(
                offer.Prefab, pos, rot);
            displayGo.transform.localScale =
                Vector3.one * offerDisplayScale;
        }
        else
        {
            bool isBall =
                offer.Type == ShopOffer.OfferType.Ball;
            displayGo =
                ShopFallbackMesh.CreateFallbackCube(
                    label, isBall, pos, rot,
                    offerDisplayScale);
        }

        displayGo.name = label;
        DisableGameplayBehaviours(displayGo);

        ShopOffer3DEntry entry =
            displayGo.GetComponent<ShopOffer3DEntry>();

        if (entry == null)
        {
            entry = displayGo
                .AddComponent<ShopOffer3DEntry>();
        }

        entry.Init(_shop, index, offer);
        OfferEntries.Add(entry);

        PinballAnalytics.LogShopItemShown(offer);
    }

    public void ClearOfferDisplays()
    {
        for (int i = OfferEntries.Count - 1; i >= 0; i--)
        {
            ShopOffer3DEntry entry = OfferEntries[i];
            if (entry != null
                && entry.gameObject != null)
            {
                Destroy(entry.gameObject);
            }
        }

        OfferEntries.Clear();
    }

    private static void DisableGameplayBehaviours(
        GameObject go)
    {
        foreach (Rigidbody rb in
            go.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
        }

        foreach (Collider col in
            go.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }

        foreach (BoardComponent bc in
            go.GetComponentsInChildren<BoardComponent>(
                true))
        {
            bc.enabled = false;
        }

        foreach (PinballFlipper bc in
            go.GetComponentsInChildren<PinballFlipper>(
                true))
        {
            bc.enabled = false;
        }

        Ball ball =
            go.GetComponentInChildren<Ball>(true);
        if (ball != null)
        {
            ball.enabled = false;
        }
    }
}
