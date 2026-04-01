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
            ServiceLocator.Get<AudioManager>()?.PlayBallLost();

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
                    other.gameObject, 2f, showHomeRunPopup: true);
            }
            else
            {
                drainHandler.OnBallDrained(other.gameObject);
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
