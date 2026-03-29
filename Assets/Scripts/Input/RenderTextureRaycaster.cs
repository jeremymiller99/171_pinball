// Updated by Cursor (claude-4.6-opus) for jjmil on 2026-03-27.
// Change: route clicks to UnifiedShopController for shop offers and board
// component placement; add ShopOffer3DEntry tooltip support.
// Updated 2026-03-27: defer offer activation to mouse-up (click vs drag-to-drop).
// Updated 2026-03-27: highlight valid targets during drag, placement-mode hover.
// Updated 2026-03-27: mesh follows mouse during drag (depth-plane projection).
// Updated 2026-03-27: route hand ball clicks in Browsing state for ball swap.
// Updated 2026-03-27: hand ball drag-to-swap (click+threshold → visual drag, drop on another ball).
using UnityEngine;
using UnityEngine.Events;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Raycasts against 3D colliders using viewport-space conversion so clicks
/// work correctly when the camera renders to a RenderTexture.
/// Also performs per-frame hover raycasts to show tooltips via
/// <see cref="TooltipManager"/> for <see cref="Ball"/> and
/// <see cref="BoardComponent"/> objects.
/// </summary>
public class RenderTextureRaycaster : MonoBehaviour
{

    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask clickableLayers = ~0;
    [SerializeField] private float maxRayDistance = 1000f;
    [SerializeField] private UnityEvent<GameObject> onObjectClicked;

    [Header("Hover Tooltip")]
    [SerializeField] private bool enableHoverTooltip = true;
    [Tooltip(
        "Radius used for ray-sphere hover checks on "
        + "hand balls whose colliders are disabled.")]
    [SerializeField] private float handBallHoverRadius = 0.25f;

    [Header("Shop offer drag")]
    [SerializeField] private float offerDragThresholdPixels = 12f;

    private GameObject _lastHoveredObject;
    private bool _tooltipShownByHover;
    private BallSpawner _cachedSpawner;
    private BoardComponent _highlightedComponent;
    private Outline _highlightedOutline;
    private Color _highlightedOutlinePrevColor;
    private UnifiedShopController _cachedShopController;

    private ShopOffer3DEntry _offerDragEntry;
    private Vector2 _offerDragStartScreenPos;
    private bool _offerDragThresholdExceeded;

    private Vector3 _offerDragOriginalPos;
    private Vector3 _offerDragWorldOffset;
    private Plane _offerDragPlane;
    private Collider[] _offerDragDisabledColliders;

    // Hand ball drag-to-swap state
    private GameObject _handBallDragObject;
    private int _handBallDragSlot = -1;
    private Vector2 _handBallDragStartScreenPos;
    private Vector3 _handBallDragOriginalPos;
    private bool _handBallDragThresholdExceeded;
    private Plane _handBallDragPlane;
    private Vector3 _handBallDragWorldOffset;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (targetCamera == null)
        {
            return;
        }

        Vector2 mouseScreenPos = GetMouseScreenPos();
        HandleDragProgress(mouseScreenPos);
        HandleHandBallDragProgress(mouseScreenPos);
        HandleOfferDragEnd(mouseScreenPos);
        HandleHandBallDragEnd(mouseScreenPos);
        HandleHover(mouseScreenPos);
        HandleClick(mouseScreenPos);
    }

    private void OnDisable()
    {
        ClearOfferDragState();
        ClearHandBallDragState();
        ClearHover();
    }

    private void HandleClick(Vector2 mouseScreenPos)
    {
        if (WasClickedThisFrame())
        {
            Vector2 viewportPoint = ScreenToViewport(mouseScreenPos);
            Ray ray = targetCamera.ViewportPointToRay(viewportPoint);

            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    maxRayDistance,
                    clickableLayers))
            {
                GameObject offerHitGo = hit.collider.gameObject;
                ShopOffer3DEntry offerEntry =
                    offerHitGo.GetComponentInParent<ShopOffer3DEntry>();

                if (offerEntry != null)
                {
                    _offerDragEntry = offerEntry;
                    _offerDragStartScreenPos = mouseScreenPos;
                    offerEntry.SetDragVisual(true);
                    return;
                }
            }

            EnsureShopController();
            if (_cachedShopController != null
                && _cachedShopController.CurrentState
                    == UnifiedShopController.ShopState.Browsing)
            {
                GameObject handBall = FindClosestHandBallOnRay(ray);
                if (handBall != null)
                {
                    BallHandSlotMarker marker =
                        handBall.GetComponentInParent<BallHandSlotMarker>();

                    if (marker != null && marker.SlotIndex >= 0)
                    {
                        _handBallDragObject = handBall;
                        _handBallDragSlot = marker.SlotIndex;
                        _handBallDragStartScreenPos = mouseScreenPos;
                        _handBallDragOriginalPos =
                            handBall.transform.position;
                        _handBallDragThresholdExceeded = false;
                        return;
                    }
                }
            }
        }

        if (!WasClickedThisFrame())
        {
            return;
        }

        Vector2 viewportDown = ScreenToViewport(mouseScreenPos);
        Ray rayDown = targetCamera.ViewportPointToRay(viewportDown);

        if (!Physics.Raycast(
                rayDown,
                out RaycastHit hitDown,
                maxRayDistance,
                clickableLayers))
        {
            return;
        }

        GameObject hitObject = hitDown.collider.gameObject;
        EnsureShopController();

        if (_cachedShopController != null
            && _cachedShopController.CurrentState
                == UnifiedShopController.ShopState.PlacingComponent)
        {
            BoardComponent boardComp =
                hitObject.GetComponentInParent<BoardComponent>();

            if (boardComp != null)
            {
                _cachedShopController
                    .OnBoardComponentClicked(boardComp);
                return;
            }
        }

        if (_cachedShopController != null
            && (_cachedShopController.CurrentState
                    == UnifiedShopController.ShopState.PlacingBall
                || _cachedShopController.CurrentState
                    == UnifiedShopController.ShopState.Browsing))
        {
            if (TryRouteHandBallClick(hitObject))
            {
                return;
            }

            GameObject handBall = FindClosestHandBallOnRay(rayDown);
            if (handBall != null && TryRouteHandBallClick(handBall))
            {
                return;
            }
        }

        onObjectClicked?.Invoke(hitObject);
    }

    /// <summary>
    /// Runs each frame while a shelf-offer drag is active. Once the mouse
    /// exceeds the pixel threshold the drag preview begins: all valid
    /// targets are highlighted, and the specific target under the cursor
    /// receives the stronger "drop-here" outline.
    /// </summary>
    private void HandleDragProgress(Vector2 mouseScreenPos)
    {
        if (_offerDragEntry == null)
        {
            return;
        }

        if (!_offerDragThresholdExceeded)
        {
            float threshold = offerDragThresholdPixels;
            Vector2 delta = mouseScreenPos - _offerDragStartScreenPos;

            if (delta.sqrMagnitude < threshold * threshold)
            {
                return;
            }

            _offerDragThresholdExceeded = true;
            BeginOfferMeshDrag(mouseScreenPos);

            EnsureShopController();

            if (_cachedShopController != null)
            {
                _cachedShopController.OnOfferDragStarted(
                    _offerDragEntry.Offer);
            }
        }

        MoveOfferMeshToMouse(mouseScreenPos);

        EnsureShopController();

        if (_cachedShopController == null)
        {
            return;
        }

        Vector2 viewportPoint = ScreenToViewport(mouseScreenPos);
        Ray ray = targetCamera.ViewportPointToRay(viewportPoint);

        GameObject hitObject = null;

        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxRayDistance,
                clickableLayers))
        {
            hitObject = hit.collider.gameObject;
        }

        if (hitObject == null
            || hitObject.GetComponentInParent<BallHandSlotMarker>() == null)
        {
            GameObject handBall = FindClosestHandBallOnRay(ray);
            if (handBall != null)
            {
                hitObject = handBall;
            }
        }

        _cachedShopController.OnOfferDragHover(
            _offerDragEntry.Offer, hitObject, ray);
    }

    private void EnsureShopController()
    {
        if (_cachedShopController == null)
        {
            _cachedShopController =
                ServiceLocator.Get<UnifiedShopController>();
        }
    }

    /// <summary>
    /// Checks if the hit object belongs to a hand ball with a
    /// <see cref="BallHandSlotMarker"/> and routes the click to
    /// <see cref="UnifiedShopController"/>.
    /// Returns true if handled.
    /// </summary>
    private bool TryRouteHandBallClick(GameObject hitObject)
    {
        if (_cachedShopController == null)
        {
            return false;
        }

        BallHandSlotMarker marker =
            hitObject.GetComponentInParent<BallHandSlotMarker>();

        if (marker != null && marker.SlotIndex >= 0)
        {
            _cachedShopController.OnBallSlotClicked(marker.SlotIndex);
            return true;
        }

        return false;
    }

    private void HandleHover(Vector2 mouseScreenPos)
    {
        if (!enableHoverTooltip)
        {
            ClearHover();
            return;
        }

        if (_offerDragEntry != null || _handBallDragObject != null)
        {
            ClearHover();
            return;
        }

        Vector2 viewportPoint =
            ScreenToViewport(mouseScreenPos);

        if (viewportPoint.x < 0f || viewportPoint.x > 1f
            || viewportPoint.y < 0f || viewportPoint.y > 1f)
        {
            ClearHover();
            return;
        }

        Ray ray =
            targetCamera.ViewportPointToRay(viewportPoint);

        GameObject hitObject = null;

        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxRayDistance,
                clickableLayers))
        {
            hitObject = hit.collider.gameObject;
        }

        string title = null;
        string desc = null;

        if (hitObject != null)
        {
            TryResolveTooltipFromObject(
                hitObject, out title, out desc);
        }

        if (title == null)
        {
            GameObject handBall =
                FindClosestHandBallOnRay(ray);

            if (handBall != null)
            {
                hitObject = handBall;
                TryResolveTooltipFromObject(
                    handBall, out title, out desc);
            }
        }

        if (title == null)
        {
            ClearHover();
            RouteToPlacementHover(null, ray);
            return;
        }

        if (hitObject != _lastHoveredObject)
        {
            ClearHighlight();
            _lastHoveredObject = hitObject;
            ApplyHighlight(hitObject);
            TooltipManager.Show(title, desc);
            _tooltipShownByHover = true;
        }

        RouteToPlacementHover(hitObject, ray);
    }

    /// <summary>
    /// During PlacingComponent / PlacingBall, forwards hover info to the
    /// shop controller so it can apply the green "drop-here" highlight on
    /// the specific target under the cursor.
    /// </summary>
    private void RouteToPlacementHover(GameObject hitObject, Ray ray)
    {
        EnsureShopController();
        if (_cachedShopController == null)
        {
            return;
        }

        var state = _cachedShopController.CurrentState;
        if (state == UnifiedShopController.ShopState.PlacingComponent
            || state == UnifiedShopController.ShopState.PlacingBall)
        {
            _cachedShopController.OnPlacementHover(hitObject, ray);
        }
    }

    private static void TryResolveTooltipFromObject(
        GameObject obj,
        out string title,
        out string desc)
    {
        title = null;
        desc = null;

        ShopOffer3DEntry offerEntry =
            obj.GetComponentInParent<ShopOffer3DEntry>();

        if (offerEntry != null && offerEntry.Offer != null)
        {
            ShopOffer offer = offerEntry.Offer;
            title = offer.DisplayName;
            desc = $"${offer.Price}\n{offer.Description}";
            return;
        }

        BallDefinitionLink ballLink =
            obj.GetComponentInParent<BallDefinitionLink>();

        if (ballLink != null
            && ballLink.TryGetDefinition(
                out BallDefinition ballDef))
        {
            title = ballDef.GetSafeDisplayName();
            desc = ballDef.Description;
            return;
        }

        BoardComponentDefinitionLink compLink =
            obj.GetComponentInParent<BoardComponentDefinitionLink>();

        if (compLink != null
            && compLink.TryGetDefinition(
                out BoardComponentDefinition compDef))
        {
            title = compDef.GetSafeDisplayName();
            desc = compDef.Description;
        }
    }

    private GameObject FindClosestHandBallOnRay(Ray ray)
    {
        return FindClosestHandBallOnRay(ray, null);
    }

    private GameObject FindClosestHandBallOnRay(
        Ray ray, GameObject exclude)
    {
        if (_cachedSpawner == null)
        {
            _cachedSpawner =
                ServiceLocator.Get<BallSpawner>();
        }

        if (_cachedSpawner == null)
        {
            return null;
        }

        var handBalls = _cachedSpawner.HandBalls;

        if (handBalls == null || handBalls.Count == 0)
        {
            return null;
        }

        float radiusSq =
            handBallHoverRadius * handBallHoverRadius;
        float bestDistSq = float.MaxValue;
        GameObject bestBall = null;

        for (int i = 0; i < handBalls.Count; i++)
        {
            GameObject b = handBalls[i];

            if (b == null || b == exclude)
            {
                continue;
            }

            Vector3 toCenter =
                b.transform.position - ray.origin;
            float dot =
                Vector3.Dot(toCenter, ray.direction);

            if (dot < 0f)
            {
                continue;
            }

            Vector3 closest =
                ray.origin + ray.direction * dot;
            float distSq =
                (b.transform.position - closest)
                    .sqrMagnitude;

            if (distSq <= radiusSq
                && distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestBall = b;
            }
        }

        return bestBall;
    }

    private void HandleOfferDragEnd(Vector2 mouseScreenPos)
    {
        if (!WasMouseReleasedThisFrame() || _offerDragEntry == null)
        {
            return;
        }

        ShopOffer3DEntry entry = _offerDragEntry;
        RestoreOfferMesh(entry);
        _offerDragEntry = null;
        entry.SetDragVisual(false);

        bool wasDrag = _offerDragThresholdExceeded;
        _offerDragThresholdExceeded = false;

        if (wasDrag)
        {
            EnsureShopController();
            if (_cachedShopController != null)
            {
                _cachedShopController.OnOfferDragEnded();
            }
        }

        float threshold = offerDragThresholdPixels;
        Vector2 delta = mouseScreenPos - _offerDragStartScreenPos;

        if (delta.sqrMagnitude < threshold * threshold)
        {
            entry.OnClick();
            return;
        }

        EnsureShopController();

        if (_cachedShopController == null)
        {
            return;
        }

        Vector2 viewportPoint = ScreenToViewport(mouseScreenPos);
        Ray ray = targetCamera.ViewportPointToRay(viewportPoint);

        GameObject hitObject = null;

        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxRayDistance,
                clickableLayers))
        {
            hitObject = hit.collider.gameObject;
        }

        _cachedShopController.TryDropOfferAfterDrag(
            entry.OfferIndex,
            hitObject,
            ray);
    }

    #region Hand Ball Drag

    private void HandleHandBallDragProgress(Vector2 mouseScreenPos)
    {
        if (_handBallDragObject == null)
        {
            return;
        }

        if (!_handBallDragThresholdExceeded)
        {
            float threshold = offerDragThresholdPixels;
            Vector2 delta =
                mouseScreenPos - _handBallDragStartScreenPos;

            if (delta.sqrMagnitude < threshold * threshold)
            {
                return;
            }

            _handBallDragThresholdExceeded = true;
            BeginHandBallMeshDrag(mouseScreenPos);

            EnsureShopController();
            if (_cachedShopController != null)
            {
                _cachedShopController.OnHandBallDragStarted(
                    _handBallDragSlot);
            }
        }

        MoveHandBallMeshToMouse(mouseScreenPos);

        EnsureShopController();
        if (_cachedShopController == null)
        {
            return;
        }

        Vector2 viewportPoint = ScreenToViewport(mouseScreenPos);
        Ray ray = targetCamera.ViewportPointToRay(viewportPoint);

        int hoveredSlot = -1;
        GameObject target =
            FindClosestHandBallOnRay(ray, _handBallDragObject);

        if (target != null)
        {
            BallHandSlotMarker marker =
                target.GetComponentInParent<BallHandSlotMarker>();

            if (marker != null)
            {
                hoveredSlot = marker.SlotIndex;
            }
        }

        _cachedShopController.OnHandBallDragHover(hoveredSlot);
    }

    private void HandleHandBallDragEnd(Vector2 mouseScreenPos)
    {
        if (!WasMouseReleasedThisFrame()
            || _handBallDragObject == null)
        {
            return;
        }

        GameObject draggedBall = _handBallDragObject;
        int draggedSlot = _handBallDragSlot;
        bool wasDrag = _handBallDragThresholdExceeded;

        draggedBall.transform.position = _handBallDragOriginalPos;
        _handBallDragObject = null;
        _handBallDragSlot = -1;
        _handBallDragThresholdExceeded = false;

        EnsureShopController();

        if (wasDrag)
        {
            if (_cachedShopController != null)
            {
                _cachedShopController.OnHandBallDragEnded();
            }

            Vector2 viewportPoint =
                ScreenToViewport(mouseScreenPos);
            Ray ray =
                targetCamera.ViewportPointToRay(viewportPoint);

            GameObject target =
                FindClosestHandBallOnRay(ray, draggedBall);

            if (target != null)
            {
                BallHandSlotMarker marker =
                    target
                        .GetComponentInParent<BallHandSlotMarker>();

                if (marker != null && marker.SlotIndex >= 0
                    && _cachedShopController != null)
                {
                    _cachedShopController.OnHandBallDragSwap(
                        draggedSlot, marker.SlotIndex);
                    return;
                }
            }

            return;
        }

        if (_cachedShopController != null)
        {
            _cachedShopController.OnBallSlotClicked(draggedSlot);
        }
    }

    private void BeginHandBallMeshDrag(Vector2 startScreenPos)
    {
        if (_handBallDragObject == null)
        {
            return;
        }

        _handBallDragPlane = new Plane(
            -targetCamera.transform.forward,
            _handBallDragOriginalPos);

        Vector2 startViewport =
            ScreenToViewport(startScreenPos);
        Ray startRay =
            targetCamera.ViewportPointToRay(startViewport);

        if (_handBallDragPlane.Raycast(
                startRay, out float startEnter))
        {
            Vector3 grabPoint = startRay.GetPoint(startEnter);
            _handBallDragWorldOffset =
                _handBallDragOriginalPos - grabPoint;
        }
        else
        {
            _handBallDragWorldOffset = Vector3.zero;
        }
    }

    private void MoveHandBallMeshToMouse(Vector2 mouseScreenPos)
    {
        if (_handBallDragObject == null)
        {
            return;
        }

        Vector2 viewportPoint = ScreenToViewport(mouseScreenPos);
        Ray ray = targetCamera.ViewportPointToRay(viewportPoint);

        if (_handBallDragPlane.Raycast(ray, out float enter))
        {
            _handBallDragObject.transform.position =
                ray.GetPoint(enter) + _handBallDragWorldOffset;
        }
    }

    private void ClearHandBallDragState()
    {
        if (_handBallDragObject != null)
        {
            _handBallDragObject.transform.position =
                _handBallDragOriginalPos;
            _handBallDragObject = null;
        }

        if (_handBallDragThresholdExceeded)
        {
            _handBallDragThresholdExceeded = false;
            EnsureShopController();
            if (_cachedShopController != null)
            {
                _cachedShopController.OnHandBallDragEnded();
            }
        }

        _handBallDragSlot = -1;
    }

    #endregion

    private void ClearOfferDragState()
    {
        if (_offerDragEntry != null)
        {
            RestoreOfferMesh(_offerDragEntry);
            _offerDragEntry.SetDragVisual(false);
            _offerDragEntry = null;
        }

        if (_offerDragThresholdExceeded)
        {
            _offerDragThresholdExceeded = false;
            EnsureShopController();
            if (_cachedShopController != null)
            {
                _cachedShopController.OnOfferDragEnded();
            }
        }
    }

    /// <summary>
    /// Saves the offer's world position, builds a camera-facing depth plane
    /// for projection, computes the grab offset, and disables colliders so
    /// the dragged mesh doesn't block drop-target raycasts.
    /// </summary>
    private void BeginOfferMeshDrag(Vector2 startScreenPos)
    {
        if (_offerDragEntry == null)
        {
            return;
        }

        _offerDragOriginalPos = _offerDragEntry.transform.position;

        _offerDragPlane = new Plane(
            -targetCamera.transform.forward,
            _offerDragOriginalPos);

        Vector2 startViewport = ScreenToViewport(startScreenPos);
        Ray startRay = targetCamera.ViewportPointToRay(startViewport);

        if (_offerDragPlane.Raycast(startRay, out float startEnter))
        {
            Vector3 grabPoint = startRay.GetPoint(startEnter);
            _offerDragWorldOffset = _offerDragOriginalPos - grabPoint;
        }
        else
        {
            _offerDragWorldOffset = Vector3.zero;
        }

        _offerDragDisabledColliders =
            _offerDragEntry.GetComponentsInChildren<Collider>();

        for (int i = 0; i < _offerDragDisabledColliders.Length; i++)
        {
            if (_offerDragDisabledColliders[i] != null)
            {
                _offerDragDisabledColliders[i].enabled = false;
            }
        }
    }

    /// <summary>
    /// Projects the current mouse position onto the drag depth plane and
    /// moves the offer mesh to that world position (plus the grab offset).
    /// </summary>
    private void MoveOfferMeshToMouse(Vector2 mouseScreenPos)
    {
        if (_offerDragEntry == null)
        {
            return;
        }

        Vector2 viewportPoint = ScreenToViewport(mouseScreenPos);
        Ray ray = targetCamera.ViewportPointToRay(viewportPoint);

        if (_offerDragPlane.Raycast(ray, out float enter))
        {
            _offerDragEntry.transform.position =
                ray.GetPoint(enter) + _offerDragWorldOffset;
        }
    }

    /// <summary>
    /// Returns the offer mesh to its original shelf position and
    /// re-enables colliders that were disabled during drag.
    /// </summary>
    private void RestoreOfferMesh(ShopOffer3DEntry entry)
    {
        if (entry != null && _offerDragThresholdExceeded)
        {
            entry.transform.position = _offerDragOriginalPos;
        }

        if (_offerDragDisabledColliders != null)
        {
            for (int i = 0; i < _offerDragDisabledColliders.Length; i++)
            {
                if (_offerDragDisabledColliders[i] != null)
                {
                    _offerDragDisabledColliders[i].enabled = true;
                }
            }

            _offerDragDisabledColliders = null;
        }
    }

    private void ClearHover()
    {
        ClearHighlight();

        if (_tooltipShownByHover)
        {
            TooltipManager.Hide();
            _tooltipShownByHover = false;
        }

        _lastHoveredObject = null;
    }

    private void ApplyHighlight(GameObject obj)
    {
        if (obj == null)
        {
            return;
        }

        BoardComponent bc =
            obj.GetComponentInParent<BoardComponent>();

        if (bc != null)
        {
            _highlightedComponent = bc;
            bc.HighlightHover();
            return;
        }

        Outline outline =
            obj.GetComponentInParent<Outline>();

        if (outline != null)
        {
            _highlightedOutline = outline;
            _highlightedOutlinePrevColor =
                outline.OutlineColor;
            outline.OutlineColor = Color.white;
        }
    }

    private void ClearHighlight()
    {
        if (_highlightedComponent != null)
        {
            _highlightedComponent.UnhighlightHover();
            _highlightedComponent = null;
        }

        if (_highlightedOutline != null)
        {
            _highlightedOutline.OutlineColor =
                _highlightedOutlinePrevColor;
            _highlightedOutline = null;
        }
    }

    private static Vector2 ScreenToViewport(
        Vector2 screenPos)
    {
        float w = Mathf.Max(1f, Screen.width);
        float h = Mathf.Max(1f, Screen.height);

        return new Vector2(
            screenPos.x / w,
            screenPos.y / h);
    }

    private static Vector2 GetMouseScreenPos()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;

        if (mouse != null)
        {
            return mouse.position.ReadValue();
        }

        return Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    private static bool WasClickedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;

        if (mouse != null
            && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        return false;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private static bool WasMouseReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;

        if (mouse != null
            && mouse.leftButton.wasReleasedThisFrame)
        {
            return true;
        }

        return false;
#else
        return Input.GetMouseButtonUp(0);
#endif
    }
}
