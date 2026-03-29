using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnifiedShopController))]
public sealed class ShopOfferShelfController : MonoBehaviour
{
    [Header("Catalogs")]
    [SerializeField] private List<BallDefinition> allBallDefinitions = new List<BallDefinition>();
    [SerializeField] private List<BoardComponentDefinition> allComponentDefinitions = new List<BoardComponentDefinition>();

    [Header("3D Offer Display")]
    [Tooltip("Offset from the BoardRoot position where the offer shelf starts.")]
    [SerializeField] private Vector3 offerShelfOffset = new Vector3(3f, 0f, 0f);
    [Tooltip("Rotation (euler) applied to the offer shelf relative to BoardRoot.")]
    [SerializeField] private Vector3 offerShelfRotation = Vector3.zero;
    [Tooltip("Spacing between 3D offer items on the shelf.")]
    [SerializeField] private float offerSpacing = 1.5f;
    [Tooltip("Scale applied to each 3D display prefab.")]
    [SerializeField] private float offerDisplayScale = 0.5f;

    private Transform _offerShelfAnchor;
    private ShopOfferGenerator _generator;
    private readonly List<ShopOffer> _currentOffers = new List<ShopOffer>();
    private readonly List<ShopOffer3DEntry> _offerEntries = new List<ShopOffer3DEntry>();

    private UnifiedShopController _shop;

    private void Awake()
    {
        _shop = GetComponent<UnifiedShopController>();
    }

    public void Initialize()
    {
        CreateOfferShelfAnchor();
        _generator = new ShopOfferGenerator(allBallDefinitions, allComponentDefinitions);
        RebuildOffers();
    }

    public void Cleanup()
    {
        ClearOfferDisplays();
        DestroyOfferShelfAnchor();
    }

    public ShopOffer GetOffer(int offerIndex)
    {
        if (offerIndex < 0 || offerIndex >= _currentOffers.Count) return null;
        return _currentOffers[offerIndex];
    }

    public void ConsumeOffer(int offerIndex)
    {
        if (offerIndex < 0 || offerIndex >= _currentOffers.Count) return;

        _currentOffers[offerIndex] = null;

        for (int i = _offerEntries.Count - 1; i >= 0; i--)
        {
            ShopOffer3DEntry entry = _offerEntries[i];
            if (entry == null)
            {
                _offerEntries.RemoveAt(i);
                continue;
            }

            if (entry.OfferIndex == offerIndex)
            {
                _offerEntries.RemoveAt(i);
                Destroy(entry.gameObject);
                break;
            }
        }
    }

    public void RebuildOffers()
    {
        ClearOfferDisplays();

        if (_generator == null) return;

        _currentOffers.Clear();
        _currentOffers.AddRange(_generator.GenerateOffers());

        for (int i = 0; i < _currentOffers.Count; i++)
        {
            SpawnOfferDisplay(_currentOffers[i], i);
        }
    }

    private void SpawnOfferDisplay(ShopOffer offer, int index)
    {
        if (offer == null || !offer.IsValid || _offerShelfAnchor == null) return;

        Vector3 pos = _offerShelfAnchor.position + _offerShelfAnchor.right * (index * offerSpacing);
        string label = $"ShopOffer_{index}_{offer.DisplayName}";

        GameObject displayGo;
        if (offer.Prefab != null)
        {
            displayGo = Instantiate(offer.Prefab, pos, _offerShelfAnchor.rotation);
            displayGo.transform.localScale = Vector3.one * offerDisplayScale;
        }
        else
        {
            bool isBall = offer.Type == ShopOffer.OfferType.Ball;
            displayGo = ShopFallbackMesh.CreateFallbackCube(label, isBall, pos, _offerShelfAnchor.rotation, offerDisplayScale);
        }

        displayGo.name = label;
        DisableGameplayBehaviours(displayGo);

        ShopOffer3DEntry entry = displayGo.GetComponent<ShopOffer3DEntry>();
        if (entry == null) entry = displayGo.AddComponent<ShopOffer3DEntry>();
        
        entry.Init(_shop, index, offer);
        _offerEntries.Add(entry);
    }

    private void ClearOfferDisplays()
    {
        for (int i = _offerEntries.Count - 1; i >= 0; i--)
        {
            ShopOffer3DEntry entry = _offerEntries[i];
            if (entry != null && entry.gameObject != null)
            {
                Destroy(entry.gameObject);
            }
        }
        _offerEntries.Clear();
    }

    private void CreateOfferShelfAnchor()
    {
        DestroyOfferShelfAnchor();

        BoardRoot boardRoot = ServiceLocator.Get<BoardLoader>()?.CurrentBoardRoot ?? Object.FindFirstObjectByType<BoardRoot>();
        if (boardRoot == null) return;

        var go = new GameObject("_ShopOfferShelfAnchor");
        go.transform.SetParent(boardRoot.transform, worldPositionStays: false);
        go.transform.localPosition = offerShelfOffset;
        go.transform.localRotation = Quaternion.Euler(offerShelfRotation);
        go.transform.localScale = Vector3.one;
        _offerShelfAnchor = go.transform;
    }

    private void DestroyOfferShelfAnchor()
    {
        if (_offerShelfAnchor != null)
        {
            Destroy(_offerShelfAnchor.gameObject);
            _offerShelfAnchor = null;
        }
    }

    private static void DisableGameplayBehaviours(GameObject go)
    {
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
        foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (BoardComponent bc in go.GetComponentsInChildren<BoardComponent>(true)) bc.enabled = false;
        Ball ball = go.GetComponentInChildren<Ball>(true);
        if (ball != null) ball.enabled = false;
    }
}
