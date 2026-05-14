using System;
using TMPro;
using UnityEngine;

public class FrenzyManager : MonoBehaviour
{
    [SerializeField] private string onFrenzyStartedText = "FRENZY! x2 MULT!";
    [SerializeField] private float defaultTimeForFrenzy = 15f;
    [SerializeField] private float currentFrenzyTime = 0f;
    [SerializeField] private float frenzyLastsUntil = 0f;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;
    [SerializeField] private TMP_FontAsset popupFontAsset;
    [SerializeField] private float popupScale = 0.9f;

    public event Action OnFrenzyActivated;
    public event Action OnFrenzyDeactivated;
    public bool isFrenzyActive;

    private void Awake()
    {
        ServiceLocator.Register(this);
        scoreManager = ServiceLocator.Get<ScoreManager>();
        floatingTextSpawner = ServiceLocator.Get<FloatingTextSpawner>();
    }

    // Activates frenzy mode, defaults to doubling current mult.
    public void ActivateFrenzy(Vector3 position, Vector3 offset, float time = 0, int mult = -1, string popupText = null)
    {
        if (isFrenzyActive) {
            frenzyLastsUntil += time > 0 ? time : defaultTimeForFrenzy;
            return;
        }
        
        isFrenzyActive = true;
        OnFrenzyActivated.Invoke();
        SteamAchievements.UnlockFirstFrenzy();
        frenzyLastsUntil = time > 0 ? time : defaultTimeForFrenzy;
        scoreManager.AddFrenzyMult(mult != -1 ? mult : scoreManager.Mult);
        floatingTextSpawner.SpawnText(
            position, popupText ?? onFrenzyStartedText, popupFontAsset, popupScale, offset);
        ServiceLocator.Get<AudioManager>()?.PlayFrenzyActivated();
    }

    private void Update()
    {
        if (!isFrenzyActive) return;

        currentFrenzyTime += Time.deltaTime;

        if (currentFrenzyTime >= frenzyLastsUntil)
        {
            currentFrenzyTime = 0f;
            frenzyLastsUntil = 0f;
            DeactivateFrenzy();
        }
    }

    public void DeactivateFrenzy()
    {
        if (!isFrenzyActive) return;

        isFrenzyActive = false;
        OnFrenzyDeactivated.Invoke();
        scoreManager.RemoveFrenzyMult();
    }
}