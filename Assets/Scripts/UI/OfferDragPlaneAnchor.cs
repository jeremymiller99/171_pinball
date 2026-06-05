using UnityEngine;

/// <summary>
/// Marks the world position that shop offers should drag on. Place this on an
/// empty GameObject positioned ABOVE the board (between the camera and the
/// board surface). <see cref="RenderTextureRaycaster"/> looks it up at runtime
/// via <see cref="ServiceLocator"/> so it works across scenes — e.g. this can
/// live in the shop scene (board_Na) while the camera/raycaster lives in
/// GameplayCore, which a serialized inspector reference could not span.
///
/// Only the world position is used; rotation and scale are ignored. The drag
/// plane itself is built camera-facing at this object's depth, so dragged
/// items always stay in front of the board instead of sinking into it.
/// </summary>
public class OfferDragPlaneAnchor : MonoBehaviour
{
    private void Awake()
    {
        ServiceLocator.Register<OfferDragPlaneAnchor>(this);
    }

    private void OnDestroy()
    {
        if (ServiceLocator.Get<OfferDragPlaneAnchor>() == this)
        {
            ServiceLocator.Unregister<OfferDragPlaneAnchor>();
        }
    }
}
