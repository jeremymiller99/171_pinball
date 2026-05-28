using UnityEngine;

public class ActiveIncomeModule : MonoBehaviour
{

    public float activeIncomeMultiplier;
    public float passiveIncomeDivider;
    private void Awake()
    {
        ServiceLocator.Get<ScoreManager>().ModuleCoinMultiplier *= activeIncomeMultiplier;
    }

    private void OnDestroy()
    {
        ServiceLocator.Get<ScoreManager>().ModuleCoinMultiplier /= activeIncomeMultiplier;
    }
}
