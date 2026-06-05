using System;
using UnityEngine;

public class FrenzyManager : MonoBehaviour
{
    [SerializeField] private float defaultTimeForFrenzy = 15f;
    [SerializeField] private float currentFrenzyTime = 0f;
    [SerializeField] private float frenzyLastsUntil = 0f;
    [SerializeField] private ScoreManager scoreManager;

    public event Action OnFrenzyActivated;
    public event Action OnFrenzyDeactivated;
    public bool isFrenzyActive;

    private void Awake()
    {
        ServiceLocator.Register(this);
        scoreManager = ServiceLocator.Get<ScoreManager>();
    }

    // Activates frenzy mode, defaults to doubling current mult.
    // position is the world point of activation (e.g. the portal or
    // abducted object); the particle is raised on Z so it sits above it.
    public void ActivateFrenzy(Vector3 position, float time = 0, int mult = -1)
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
        // Board VFX is owned by LevelUpVFXTrigger (board scene), so the
        // frenzy particle is configured alongside the level-up effects.
        ServiceLocator.Get<LevelUpVFXTrigger>()?.SpawnFrenzyVFX(position);
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