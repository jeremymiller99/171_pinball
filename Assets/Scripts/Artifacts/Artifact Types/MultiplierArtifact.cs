using UnityEngine;

public class MultilplierArtifact : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private float multMultiplier;
    [SerializeField] private float pointDivider;
    private void Awake()
    {
        scoreManager = ServiceLocator.Get<ScoreManager>();
        scoreManager.ArtifactMultMultiplier *= multMultiplier;
        scoreManager.ArtifactPointMultiplier /= pointDivider;
    }

    private void OnDestroy()
    {
        scoreManager.ArtifactMultMultiplier /= multMultiplier;
        scoreManager.ArtifactPointMultiplier *= pointDivider;
    }
}
