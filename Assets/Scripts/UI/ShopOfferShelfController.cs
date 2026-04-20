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

    public void RebuildOffers()
    {
        ClearOfferDisplays();

        if (_generator == null)
        {
            return;
        }

        ShopShipController ship =
            Object.FindFirstObjectByType<ShopShipController>();

        float playerMult = 1f;

        if (GameSession.Instance != null
            && GameSession.Instance.ActiveShip != null)
        {
            playerMult = Mathf.Max(
                0f,
                GameSession.Instance.ActiveShip.ShopPriceMultiplier);
        }

        float visitorMult = ship != null
            ? Mathf.Max(0f, ship.CurrentVisitorPriceMultiplier)
            : 1f;

        float combined = playerMult * visitorMult;

        ElementType catalogElement = ship != null
            ? ship.CurrentCatalogElement
            : ElementType.None;

        int minO = ship != null ? ship.MinOffers : 3;
        int maxO = ship != null ? ship.MaxOffers : 6;

        _currentOffers.Clear();
        _currentOffers.AddRange(
            _generator.GenerateOffers(
                catalogElement,
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
