using UnityEngine;

public class MultilplierModule : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private float multMultiplier;
    [SerializeField] private float pointDivider;
    private void Awake()
    {
        scoreManager = ServiceLocator.Get<ScoreManager>();
        scoreManager.ModuleMultMultiplier *= multMultiplier;
        scoreManager.ModulePointMultiplier /= pointDivider;
    }

    private void OnDestroy()
    {
        scoreManager.ModuleMultMultiplier /= multMultiplier;
        scoreManager.ModulePointMultiplier *= pointDivider;
    }
}
