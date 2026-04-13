using UnityEngine;

public class UnlockEverything : MonoBehaviour
{
    [SerializeField] private ProgressionService progService;

    private void Awake()
    {
        progService = FindAnyObjectByType<ProgressionService>();
    }
    public void OnClick()
    {
        progService.everythingUnlocked = true;
    }
}
