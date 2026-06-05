using UnityEngine;

/// <summary>
/// Binds a World Space canvas's event camera to the camera that renders the UI
/// to the screen, so its GraphicRaycaster can convert screen clicks into UI
/// hits.
///
/// Unlike <see cref="WorldSpaceCanvasCamBinder"/> (which binds to Camera.main),
/// this deliberately avoids the gameplay board camera: that camera is tagged
/// MainCamera but renders to a RenderTexture, so screen-space pointer positions
/// do not map onto it and UI raycasts fail. Instead we pick a camera that
/// renders to the screen (no target texture) and includes the UI layer in its
/// culling mask.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public class WorldSpaceCanvasScreenCamBinder : MonoBehaviour
{
    private Canvas _canvas;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        Bind();
    }

    private void Bind()
    {
        if (_canvas == null || _canvas.renderMode != RenderMode.WorldSpace) return;

        Camera cam = FindScreenUICamera();
        if (cam != null) _canvas.worldCamera = cam;
    }

    private static Camera FindScreenUICamera()
    {
        int uiMask = 1 << LayerMask.NameToLayer("UI");

        var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (cams == null) return null;

        // Prefer an enabled, screen-rendering camera that draws the UI layer.
        Camera screenFallback = null;
        for (int i = 0; i < cams.Length; i++)
        {
            Camera cam = cams[i];
            if (cam == null || !cam.enabled || !cam.gameObject.activeInHierarchy) continue;
            if (cam.targetTexture != null) continue; // skip render-to-texture (board) cameras

            if ((cam.cullingMask & uiMask) != 0) return cam;
            if (screenFallback == null) screenFallback = cam;
        }

        return screenFallback;
    }
}
