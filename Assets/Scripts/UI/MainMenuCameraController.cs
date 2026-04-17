using UnityEngine;
using UnityEngine.InputSystem;

public class MainMenuCameraController : MonoBehaviour
{
    public enum View { Default, ShipPick }

    [Header("Points Of Interest")]
    [SerializeField] private Transform defaultPoint;
    [SerializeField] private Transform midPoint;
    [SerializeField] private Transform shipPickPoint;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 1.25f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private View startingView = View.Default;

    [Header("Mouse Look")]
    [SerializeField] private float lookRange = 10f;
    [SerializeField] private float lookDamping = 5f;

    private View currentView;
    private View targetView;
    private float transitionT;
    private bool transitioning;

    [Header("UI Hide")]
    [SerializeField] private Canvas[] menuCanvases;

    private Vector2 mouseLookOffset;
    private Vector2 smoothedOffset;
    private Quaternion baseRotation;
    private bool uiHidden;

    private void Awake()
    {
        currentView = startingView;
        targetView = startingView;
        SnapTo(startingView);
    }

    public void GoToDefault() => SetView(View.Default);
    public void GoToShipPick() => SetView(View.ShipPick);

    public void SetView(View view)
    {
        if (view == targetView && !transitioning) return;
        targetView = view;
        transitionT = 0f;
        transitioning = true;
    }

    public void SnapTo(View view)
    {
        currentView = view;
        targetView = view;
        transitioning = false;
        transitionT = 0f;

        Transform t = GetEndpoint(view);
        if (t != null)
        {
            transform.position = t.position;
            baseRotation = t.rotation;
        }
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) SnapTo(defaultPoint);
            if (kb.digit2Key.wasPressedThisFrame) SnapTo(midPoint);
            if (kb.digit3Key.wasPressedThisFrame) SnapTo(shipPickPoint);
            if (kb.digit4Key.wasPressedThisFrame) { SnapTo(View.Default); SetView(View.ShipPick); }
            if (kb.hKey.wasPressedThisFrame) ToggleUI();
        }

        // Mouse look offset from screen center (normalized -1 to 1)
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 screenPos = mouse.position.ReadValue();
            mouseLookOffset.x = (screenPos.x / Screen.width - 0.5f) * 2f;
            mouseLookOffset.y = (screenPos.y / Screen.height - 0.5f) * 2f;
        }
        smoothedOffset = Vector2.Lerp(smoothedOffset, mouseLookOffset, Time.deltaTime * lookDamping);

        if (transitioning)
        {
            if (defaultPoint == null || midPoint == null || shipPickPoint == null) return;

            transitionT += Time.deltaTime / Mathf.Max(0.0001f, transitionDuration);
            float clamped = Mathf.Clamp01(transitionT);
            float eased = ease.Evaluate(clamped);

            Transform from = GetEndpoint(currentView);
            Transform to = GetEndpoint(targetView);
            if (from == null || to == null) return;

            Vector3 pos = QuadraticBezier(from.position, midPoint.position, to.position, eased);
            Quaternion rot = Quaternion.Slerp(
                Quaternion.Slerp(from.rotation, midPoint.rotation, eased),
                Quaternion.Slerp(midPoint.rotation, to.rotation, eased),
                eased);

            transform.position = pos;
            baseRotation = rot;

            if (clamped >= 1f)
            {
                transitioning = false;
                currentView = targetView;
            }
        }

        // Apply damped mouse look on top of the base rotation
        Quaternion yaw = Quaternion.AngleAxis(smoothedOffset.x * lookRange, Vector3.up);
        Quaternion pitch = Quaternion.AngleAxis(-smoothedOffset.y * lookRange, Vector3.right);
        transform.rotation = baseRotation * yaw * pitch;
    }

    private void SnapTo(Transform point)
    {
        if (point == null) return;
        transitioning = false;
        transform.position = point.position;
        baseRotation = point.rotation;
    }

    private Transform GetEndpoint(View view)
    {
        switch (view)
        {
            case View.Default: return defaultPoint;
            case View.ShipPick: return shipPickPoint;
            default: return defaultPoint;
        }
    }

    private void ToggleUI()
    {
        uiHidden = !uiHidden;
        if (menuCanvases != null)
        {
            for (int i = 0; i < menuCanvases.Length; i++)
            {
                if (menuCanvases[i] != null)
                    menuCanvases[i].enabled = !uiHidden;
            }
        }
    }

    private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return (u * u) * a + (2f * u * t) * b + (t * t) * c;
    }
}
