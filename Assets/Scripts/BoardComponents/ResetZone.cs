// Updated by Claude Code (claude-opus-5) for jjmil on 2026-08-05 (mark drains ball-save eligible).
using UnityEngine;

public class ResetZone : MonoBehaviour
{
    [SerializeField] private DrainHandler drainHandler;
    [SerializeField] private bool outsideBounds;

    private void Awake()
    {
        ResolveDrainHandler();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            ServiceLocator.Get<AudioManager>()?.PlayBallLost(other.transform.position);

            if (drainHandler == null)
                ResolveDrainHandler();

            if (drainHandler == null)
            {
                Debug.LogError(
                    $"{nameof(ResetZone)}: No {nameof(DrainHandler)} found. " +
                    $"Assign it in the inspector.", this);
                return;
            }

            if (outsideBounds)
            {
                drainHandler.OnBallDrained(
                    other.gameObject,
                    showHomeRunPopup: true,
                    eligibleForBallSave: true);
            }
            else
            {
                drainHandler.OnBallDrained(
                    other.gameObject,
                    showHomeRunPopup: false,
                    eligibleForBallSave: true);
            }
        }
    }

    private void ResolveDrainHandler()
    {
        if (drainHandler != null)
            return;

        drainHandler = ServiceLocator.Get<DrainHandler>();
    }
}
