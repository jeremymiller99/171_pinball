// Generated with Cursor AI, by Claude, 2026-03-22.
using UnityEngine;

/// <summary>
/// Pins a World Space canvas to a fixed viewport position so it
/// appears at the same screen location regardless of resolution
/// or aspect ratio. Attach to the Board Canvas alongside
/// WorldSpaceCanvasCamBinder.
/// </summary>
[DisallowMultipleComponent]
public class WorldSpaceViewportPinner : MonoBehaviour
{
    [Header("Viewport Position")]
    [Tooltip("Desired screen position in 0-1 range. "
        + "(0,0) = bottom-left, (1,1) = top-right.")]
    [SerializeField] private Vector2 viewportAnchor
        = new Vector2(0.35f, 0.6f);

    [Tooltip("Distance from the rendering camera to place "
        + "the canvas (world units).")]
    [SerializeField] private float distanceFromCamera = 17f;

    [Header("Debug")]
    [SerializeField] private bool pinEveryFrame;

    private Camera renderingCamera;
    private ScoreTallyAnimator scoreTallyAnimator;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private bool pinned;

    private void Start()
    {
        scoreTallyAnimator = GetComponent<ScoreTallyAnimator>();
        FindRenderingCamera();
        Pin();
        SubscribeToDisplayChanged();
    }

    private void OnEnable()
    {
        if (pinned)
        {
            FindRenderingCamera();
            Pin();
        }

        SubscribeToDisplayChanged();
    }

    private void OnDisable()
    {
        UnsubscribeFromDisplayChanged();
    }

    private void LateUpdate()
    {
        if (pinEveryFrame)
        {
            Pin();
            return;
        }

        if (Screen.width != lastScreenWidth
            || Screen.height != lastScreenHeight)
        {
            Pin();
        }
    }

    private void SubscribeToDisplayChanged()
    {
        if (DisplaySettingsManager.Instance != null)
        {
            DisplaySettingsManager.Instance.DisplayChanged
                -= OnDisplayChanged;
            DisplaySettingsManager.Instance.DisplayChanged
                += OnDisplayChanged;
        }
    }

    private void UnsubscribeFromDisplayChanged()
    {
        if (DisplaySettingsManager.Instance != null)
        {
            DisplaySettingsManager.Instance.DisplayChanged
                -= OnDisplayChanged;
        }
    }

    private void OnDisplayChanged()
    {
        FindRenderingCamera();
        Pin();
    }

    private void FindRenderingCamera()
    {
        int canvasLayer = gameObject.layer;
        int layerMask = 1 << canvasLayer;

        Camera[] cameras = FindObjectsByType<Camera>(
            FindObjectsSortMode.None
        );

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam == null || !cam.enabled) continue;
            if (!cam.gameObject.activeInHierarchy) continue;
            if (cam.targetTexture != null) continue;

            if ((cam.cullingMask & layerMask) != 0)
            {
                renderingCamera = cam;
                return;
            }
        }

        renderingCamera = Camera.main;
    }

    private void Pin()
    {
        if (renderingCamera == null)
        {
            FindRenderingCamera();
        }

        if (renderingCamera == null) return;

        Vector3 viewportPoint = new Vector3(
            viewportAnchor.x,
            viewportAnchor.y,
            distanceFromCamera
        );

        Vector3 worldPosition =
            renderingCamera.ViewportToWorldPoint(viewportPoint);

        transform.position = worldPosition;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        pinned = true;

        if (scoreTallyAnimator != null)
        {
            scoreTallyAnimator.ResetCachedPositions();
        }
    }
}
