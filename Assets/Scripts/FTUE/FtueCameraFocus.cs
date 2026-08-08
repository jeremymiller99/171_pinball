// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-08 (FTUE camera focus moves).
using System.Collections;
using UnityEngine;

/// <summary>
/// Dollies the gameplay camera to a point in the board scene and back, for beats that need the
/// player looking somewhere specific (the launcher, to begin with).
///
/// The rig it drives lives in GameplayCore, not in the board, so it cannot be a serialized
/// reference. It is resolved as <c>Camera.main.transform.parent</c> — the same way
/// <see cref="CameraIntroPan"/> and <see cref="ShopTransitionController"/> already resolve it, so
/// all three agree on what "the rig" is.
///
/// <b>Position only, in local space.</b> Both existing systems animate <c>localPosition</c> and
/// leave rotation alone; matching that keeps the camera composable with them and makes the move
/// read as a dolly rather than a swing.
///
/// <b>The play pose is captured, not authored.</b> An authored "return here" point would silently
/// drift out of step with wherever the opening pan actually finishes, and would need re-authoring
/// every time that intro is retuned.
/// </summary>
[DisallowMultipleComponent]
public sealed class FtueCameraFocus : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Seconds for a focus move when the caller does not specify one.")]
    [SerializeField] private float defaultDuration = 0.9f;

    [Tooltip("Shapes the move over time.")]
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Transform rig;
    private Camera rigCamera;

    private Vector3 playPoseLocal;
    private bool hasPlayPose;
    private Coroutine moveRoutine;

    /// <summary>True while a focus move is running. Beats that must not overlap one gate on this.</summary>
    public bool IsMoving => moveRoutine != null;

    /// <summary>Remembers where the camera sits during normal play, as the pose to return to.</summary>
    public void CapturePlayPose()
    {
        if (!ResolveRig()) return;

        playPoseLocal = rig.localPosition;
        hasPlayPose = true;
    }

    /// <summary>Moves so the camera ends up at <paramref name="point"/>.</summary>
    public void FocusOn(Transform point, float duration = -1f)
    {
        if (point == null)
        {
            Debug.LogWarning($"[{nameof(FtueCameraFocus)}] No focus point supplied; "
                + "leaving the camera where it is.", this);
            return;
        }

        if (!ResolveRig()) return;

        // Captured lazily in case a caller focuses before the opening sequence got there, so the
        // camera always has somewhere to come back to.
        if (!hasPlayPose) CapturePlayPose();

        StartMove(LocalPositionThatPutsCameraAt(point.position), duration);
    }

    /// <summary>Moves back to the captured play pose.</summary>
    public void ReturnToPlayPose(float duration = -1f)
    {
        if (!hasPlayPose || !ResolveRig()) return;

        StartMove(playPoseLocal, duration);
    }

    /// <summary>
    /// Restores the play pose immediately, with no move. Used when something else is about to take
    /// the camera over: <see cref="ShopTransitionController"/> re-reads the rig's current position
    /// as its "home" when the shop opens, so being anywhere else at that moment would teach it the
    /// wrong home and strand the camera there for the rest of the run.
    /// </summary>
    public void SnapToPlayPose()
    {
        CancelMove();

        if (!hasPlayPose || !ResolveRig()) return;

        rig.localPosition = playPoseLocal;
    }

    /// <summary>Stops any move in progress, leaving the camera where it currently is.</summary>
    public void CancelMove()
    {
        if (moveRoutine == null) return;

        StopCoroutine(moveRoutine);
        moveRoutine = null;
    }

    private void OnDisable()
    {
        // Never leave the camera parked at a focus point because the board unloaded mid-move.
        SnapToPlayPose();
    }

    private void StartMove(Vector3 targetLocal, float duration)
    {
        CancelMove();

        float seconds = duration > 0f ? duration : defaultDuration;
        if (seconds <= 0f)
        {
            rig.localPosition = targetLocal;
            return;
        }

        moveRoutine = StartCoroutine(MoveRoutine(targetLocal, seconds));
    }

    private IEnumerator MoveRoutine(Vector3 targetLocal, float seconds)
    {
        Vector3 startLocal = rig.localPosition;
        float elapsed = 0f;

        // Unscaled, so a beat that pauses the game still gets its camera move.
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = ease.Evaluate(Mathf.Clamp01(elapsed / seconds));
            rig.localPosition = Vector3.LerpUnclamped(startLocal, targetLocal, t);
            yield return null;
        }

        rig.localPosition = targetLocal;
        moveRoutine = null;
    }

    /// <summary>
    /// The rig localPosition that lands the camera on <paramref name="worldPoint"/>. The camera is
    /// a child of the rig with its own offset, so moving the rig to the point directly would leave
    /// the camera short of it by that offset; shifting the rig by the camera's own delta does not
    /// care what the offset is.
    /// </summary>
    private Vector3 LocalPositionThatPutsCameraAt(Vector3 worldPoint)
    {
        Vector3 delta = worldPoint - rigCamera.transform.position;
        Vector3 targetWorld = rig.position + delta;

        return rig.parent != null
            ? rig.parent.InverseTransformPoint(targetWorld)
            : targetWorld;
    }

    private bool ResolveRig()
    {
        if (rig != null && rigCamera != null) return true;

        rigCamera = Camera.main;
        if (rigCamera == null)
        {
            Debug.LogWarning($"[{nameof(FtueCameraFocus)}] No main camera; "
                + "camera beats will be skipped.", this);
            return false;
        }

        // Matches CameraIntroPan and ShopTransitionController. Falling back to the camera itself
        // keeps a rig-less setup working rather than silently doing nothing.
        rig = rigCamera.transform.parent != null
            ? rigCamera.transform.parent
            : rigCamera.transform;

        return true;
    }
}
