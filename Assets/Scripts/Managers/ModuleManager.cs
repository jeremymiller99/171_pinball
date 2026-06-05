using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.UI;

public class ModuleManager : MonoBehaviour
{
    [SerializeField] private ModulePool modulePool;
    [SerializeField] private List<GameObject> inPlayModuleList;
    [SerializeField] private List<GameObject> moduleCards;
    [SerializeField] private ModuleSpawnPoint moduleSpawnPoint;
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private Image image;
    [SerializeField] private float timeUntilDeselectModules;
    private float _timeScaleBeforePause;
    private float _fixedDeltaBeforePause;

    public bool SelectingModule;

    private void Awake()
    {
        ServiceLocator.Register(this);
        gameRulesManager = ServiceLocator.Get<GameRulesManager>();
        gameRulesManager.LevelChanged += TryActivateCards;
        image = GetComponent<Image>();
        SetCardsActive(false);
    }

    public void ActivateModuleCards()
    {
        // Pause Gameplay 
        _timeScaleBeforePause = Time.timeScale;
        _fixedDeltaBeforePause = Time.fixedDeltaTime;
        Time.timeScale = 0f;
        SelectingModule = true;
        // Disable floating text so it's not in the way of the module cards
        foreach (var text in FindObjectsByType<FloatingText>(FindObjectsSortMode.None))
        {
            text.gameObject.SetActive(false);
        }

        List<ArtifactDefinition> modules = modulePool.GetThreeRandomModules();
        for (int i = 0; i < modules.Count; i++)
        {
            moduleCards[i].SetActive(true);
            moduleCards[i].GetComponent<ModuleCard>().Populate(modules[i]);
        }
    }

    public void AddModuleToPlay(GameObject modulePrefab)
    {
        // moduleSpawnPoint can't be found in start function, needs to be done here.
        if (!moduleSpawnPoint)
        {
            moduleSpawnPoint = ServiceLocator.Get<ModuleSpawnPoint>();
        }

        GameObject newModule = moduleSpawnPoint.SpawnModule(modulePrefab, inPlayModuleList.Count);
        inPlayModuleList.Add(newModule);
        SetCardsActive(false);

        // Resume gameplay
        Time.timeScale = _timeScaleBeforePause;
        Time.fixedDeltaTime = _fixedDeltaBeforePause;
        StartCoroutine(WaitThenDeselectModules());

        // Enable floating text again
        foreach (var text in FindObjectsByType<FloatingText>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            text.gameObject.SetActive(true);
        }
    }

    public void TryActivateCards()
    {
        if (gameRulesManager.LevelIndex % 5 == 0 && gameRulesManager.LevelIndex != 0)
        {
            ActivateModuleCards();
        }
    }

    private void SetCardsActive(bool enabled)
    {
        image.enabled = enabled;
        foreach (GameObject card in moduleCards)
        {
            card.SetActive(enabled);
        }
    }

    private IEnumerator WaitThenDeselectModules()
    {
        yield return new WaitForSeconds(timeUntilDeselectModules);
        SelectingModule = false;
    }
}
