// Generated with Antigravity by jjmil on 2026-03-29.
using UnityEngine;

/// <summary>
/// Marker component placed on the OfferPanel prefab in the board
/// scene. Exposes an item anchor transform so the
/// <see cref="ShopOfferShelfController"/> (in GameplayCore) can
/// locate the panel cross-scene and spawn offer items on its
/// surface.
/// </summary>
[DisallowMultipleComponent]
public sealed class ShopOfferPanel : MonoBehaviour
{
    [Header("Item Spawn Point")]
    [Tooltip(
        "Child transform where the first shop item " +
        "spawns. Items are laid out along this " +
        "transform's right axis.")]
    [SerializeField] private Transform itemAnchor;

    /// <summary>
    /// The world-space anchor that offer items are placed
    /// relative to. Falls back to this transform when no
    /// explicit child anchor is assigned.
    /// </summary>
    public Transform ItemAnchor =>
        itemAnchor != null ? itemAnchor : transform;

    private void Awake()
    {
        ServiceLocator.Register<ShopOfferPanel>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<ShopOfferPanel>();
    }
}
