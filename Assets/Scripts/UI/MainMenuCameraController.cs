using UnityEngine;

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

    private View currentView;
    private View targetView;
    private float transitionT;
    private bool transitioning;

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
            transform.SetPositionAndRotation(t.position, t.rotation);
        }
    }

    private void Update()
    {
        if (!transitioning) return;
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

        transform.SetPositionAndRotation(pos, rot);

        if (clamped >= 1f)
        {
            transitioning = false;
            currentView = targetView;
        }
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

    private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return (u * u) * a + (2f * u * t) * b + (t * t) * c;
    }
}
