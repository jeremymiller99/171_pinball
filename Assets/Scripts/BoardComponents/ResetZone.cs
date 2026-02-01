using UnityEngine;

public class ResetZone : MonoBehaviour
{
    [SerializeField] private GameRulesManager rulesManager;
    [SerializeField] private bool outsideBounds;

    private void Awake()
    {
        ResolveRulesManager();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            if (rulesManager == null)
                ResolveRulesManager();

            if (rulesManager == null)
            {
                Debug.LogError($"{nameof(ResetZone)}: No {nameof(GameRulesManager)} found. Assign it in the inspector.", this);
                return;
            }

            if (outsideBounds)
            {
                rulesManager.OnBallDrained(other.gameObject, 2f, showHomeRunPopup: true);
            }
            else
            {
                rulesManager.OnBallDrained(other.gameObject);
            }
        }
    }

    private void ResolveRulesManager()
    {
        if (rulesManager != null)
            return;

#if UNITY_2022_2_OR_NEWER
        rulesManager = FindFirstObjectByType<GameRulesManager>();
#else
        rulesManager = FindObjectOfType<GameRulesManager>();
#endif
    }
}
