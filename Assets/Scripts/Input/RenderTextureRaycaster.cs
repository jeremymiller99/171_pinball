// Updated by Cursor (claude-4.6-opus) for jjmil on 2026-03-26.
// Change: add per-frame hover raycasting for tooltip support.
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

    private GameObject _lastHoveredObject;
    private bool _tooltipShownByHover;
    private BallSpawner _cachedSpawner;
    private BoardComponent _highlightedComponent;
    private Outline _highlightedOutline;
    private Color _highlightedOutlinePrevColor;

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
        HandleHover(mouseScreenPos);
        HandleClick(mouseScreenPos);
    }

    private void OnDisable()
    {
        ClearHover();
    }

    private void HandleClick(Vector2 mouseScreenPos)
    {
        if (!WasClickedThisFrame())
        {
            return;
        }

        Vector2 viewportPoint = ScreenToViewport(mouseScreenPos);
        Ray ray = targetCamera.ViewportPointToRay(viewportPoint);

        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxRayDistance,
                clickableLayers))
        {
            GameObject hitObject = hit.collider.gameObject;

            Ball ball =
                hitObject.GetComponentInParent<Ball>();

            if (ball != null)
            {
                Debug.Log(
                    $"[RenderTextureRaycaster] Clicked Ball: "
                    + $"'{ball.gameObject.name}' "
                    + $"(hit collider: '{hitObject.name}')");
            }

            BoardComponent boardComp =
                hitObject.GetComponentInParent<BoardComponent>();

            if (boardComp != null)
            {
                Debug.Log(
                    $"[RenderTextureRaycaster] Clicked BoardComponent: "
                    + $"'{boardComp.gameObject.name}' "
                    + $"(type: {boardComp.componentType}, "
                    + $"hit collider: '{hitObject.name}')");
            }

            if (ball == null && boardComp == null)
            {
                Debug.Log(
                    $"[RenderTextureRaycaster] Clicked: "
                    + $"'{hitObject.name}'");
            }

            onObjectClicked?.Invoke(hitObject);
        }
    }

    private void HandleHover(Vector2 mouseScreenPos)
    {
        if (!enableHoverTooltip)
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
            return;
        }

        if (hitObject == _lastHoveredObject)
        {
            return;
        }

        ClearHighlight();
        _lastHoveredObject = hitObject;
        ApplyHighlight(hitObject);
        TooltipManager.Show(title, desc);
        _tooltipShownByHover = true;
    }

    private static void TryResolveTooltipFromObject(
        GameObject obj,
        out string title,
        out string desc)
    {
        title = null;
        desc = null;

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
        if (_cachedSpawner == null)
        {
            _cachedSpawner =
                FindFirstObjectByType<BallSpawner>();
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

            if (b == null)
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
}
