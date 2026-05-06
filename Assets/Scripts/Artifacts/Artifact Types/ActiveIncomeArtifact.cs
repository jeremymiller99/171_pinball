using UnityEngine;

public class ActiveIncomeArtifact : MonoBehaviour
{

    public float activeIncomeMultiplier;
    public float passiveIncomeDivider;
    private void Awake()
    {
        ServiceLocator.Get<ScoreManager>().ArtifactCoinMultiplier *= activeIncomeMultiplier;
    }

    private void OnDestroy()
    {
        ServiceLocator.Get<ScoreManager>().ArtifactCoinMultiplier /= activeIncomeMultiplier;
    }
}
