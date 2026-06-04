using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a one-shot "intro" pan at the start of gameplay: snaps the camera rig
/// to the left of its normal play pose, then slides it into place (the rig's
/// authored local position = the shop's "home").
///
/// Drives the camera RIG's localPosition only, so it layers cleanly with
/// CameraShake / CameraAliveMotion (which write the camera child) and with
/// ShopTransitionController (which also pans the rig, but only after this
/// one-shot intro has finished and the rig is back home).
///
/// Position-only: the rig keeps its home rotation throughout. Trigger it via
/// <see cref="Play"/> — RunFlowController calls this in sync with the fade-in.
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraIntroPan : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Parent transform of the Main Camera (the same rig ShopTransitionController pans). Auto-resolved to Camera.main's parent if left empty.")]
    [SerializeField] private Transform cameraRig;

    [Header("Movement")]
    [Tooltip("How far to the left of the play pose the camera starts the intro, in rig-local units. Positive = further left.")]
    [SerializeField] private float startLeftOffset = 20f;

    [Tooltip("Seconds to travel from the intro point to the play pose.")]
    [SerializeField] private float duration = 1.5f;

    [Tooltip("Shapes the pan over time (ease in/out by default).")]
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("If true, ignores Time.timeScale (keeps moving while paused / during fade).")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Auto play")]
    [Tooltip("Play automatically on Start. Leave OFF when RunFlowController drives it so it can sync with the fade-in.")]
    [SerializeField] private bool playOnStart = false;

    // The authored play pose, captured before we ever move the rig.
    private Vector3 _homeLocalPos;
    private bool _hasHome;
    private Coroutine _routine;

    private void Awake()
    {
        ResolveRig();
        CaptureHome();
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    /// <summary>
    /// Snaps the rig to the left of the play pose and pans it back in.
    /// Safe to call once at gameplay start (e.g. alongside SceneFader.FadeIn()).
    /// </summary>
    public void Play()
    {
        ResolveRig();
        if (cameraRig == null)
        {
            Debug.LogWarning("[CameraIntroPan] No camera rig; skipping intro pan.");
            return;
        }

        // Make sure we have the true play pose (in case Awake ran before the rig existed).
        if (!_hasHome)
            CaptureHome();

        Vector3 introLocal = _homeLocalPos + new Vector3(-startLeftOffset, 0f, 0f);

        // Snap to the intro framing first, then animate home.
        cameraRig.localPosition = introLocal;

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(PanRoutine(introLocal, _homeLocalPos));
    }

    private IEnumerator PanRoutine(Vector3 from, Vector3 to)
    {
        float t = 0f;
        float dur = Mathf.Max(0.001f, duration);

        while (t < dur)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float n = ease != null ? ease.Evaluate(Mathf.Clamp01(t / dur)) : Mathf.Clamp01(t / dur);
            cameraRig.localPosition = Vector3.LerpUnclamped(from, to, n);
            yield return null;
        }

        cameraRig.localPosition = to;
        _routine = null;
    }

    private void ResolveRig()
    {
        if (cameraRig != null)
            return;

        Camera cam = Camera.main;
        if (cam != null && cam.transform.parent != null)
            cameraRig = cam.transform.parent;
    }

    private void CaptureHome()
    {
        if (cameraRig == null)
            return;

        _homeLocalPos = cameraRig.localPosition;
        _hasHome = true;
    }
}
