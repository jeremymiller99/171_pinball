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
    [Tooltip(
        "Leave EMPTY. The full catalog is loaded from " +
        "Resources/BallDefinitions at runtime. Anything " +
        "listed here silently shadows that catalog " +
        "(test / curation escape hatch only).")]
    [SerializeField] private List<BallDefinition>
        allBallDefinitions = new List<BallDefinition>();

    [Tooltip(
        "Leave EMPTY. The full catalog is loaded from " +
        "Resources/BoardComponentDefinitions at runtime. " +
        "Anything listed here silently shadows that catalog.")]
    [SerializeField] private List<BoardComponentDefinition>
        allComponentDefinitions =
            new List<BoardComponentDefinition>();

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

    // Full catalogs resolved once via RunItemCatalog (Resources, or the
    // inspector overrides above). Unlock + ship/mission filtering happens per
    // draw, not here.
    private List<BallDefinition> _ballCatalog;
    private List<BoardComponentDefinition> _componentCatalog;

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

        if (_ballCatalog == null)
        {
            _ballCatalog =
                RunItemCatalog.LoadBalls(allBallDefinitions);
        }

        if (_componentCatalog == null)
        {
            _componentCatalog =
                RunItemCatalog.LoadComponents(
                    allComponentDefinitions);
        }

        _generator = new ShopOfferGenerator(
            _ballCatalog, _componentCatalog);
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
        if (_ballCatalog == null)
        {
            _ballCatalog =
                RunItemCatalog.LoadBalls(allBallDefinitions);
        }

        return MysteryBallResolver.Resolve(rarity, _ballCatalog);
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

    /// <summary>
    /// Module-driven multiplier on all shop offer prices (1 = normal). Loyalty Program
    /// sets it to 0.8 for a 20% discount. Read when offers are generated.
    /// </summary>
    public static float ModulePriceMultiplier = 1f;

    public void RebuildOffers()
    {
        ClearOfferDisplays();

        if (_generator == null)
        {
            return;
        }

        ShopShipController ship =
            Object.FindFirstObjectByType<ShopShipController>();

        // Player-ship / visitor-merchant multipliers stay bypassed (1x); the only
        // price modifier applied is the module discount (e.g. Loyalty Program).
        float combined = Mathf.Max(0f, ModulePriceMultiplier);

        int minO = ship != null ? ship.MinOffers : 3;
        int maxO = ship != null ? ship.MaxOffers : 6;

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
