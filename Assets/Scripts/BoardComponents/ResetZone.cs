using UnityEngine;

public class ResetZone : MonoBehaviour
{
    [SerializeField] private GameRulesManager rulesManager;

    private void Awake()
    {
        if (rulesManager == null)
        {
#if UNITY_2022_2_OR_NEWER
            rulesManager = FindFirstObjectByType<GameRulesManager>();
#else
            rulesManager = FindObjectOfType<GameRulesManager>();
#endif
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            if (rulesManager == null)
            {
                Debug.LogError($"{nameof(ResetZone)}: No {nameof(GameRulesManager)} found. Assign it in the inspector.", this);
                return;
            }
            FMODUnity.RuntimeManager.PlayOneShot("event:/ball_lost");
            rulesManager.OnBallDrained(other.gameObject);
        }
    }
}
