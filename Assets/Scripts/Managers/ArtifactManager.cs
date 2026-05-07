using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Net;
using UnityEngine.UI;

public class ArtifactManager : MonoBehaviour
{
    [SerializeField] private ArtifactPool artifactPool;
    [SerializeField] private List<GameObject> inPlayArtifactList;
    [SerializeField] private List<GameObject> artifactCards;
    [SerializeField] private ArtifactSpawnPoint artifactSpawnPoint;
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private Image image;
    private float _timeScaleBeforePause;
    private float _fixedDeltaBeforePause;

    private void Awake()
    {
        ServiceLocator.Register(this);
        gameRulesManager = ServiceLocator.Get<GameRulesManager>();
        gameRulesManager.LevelChanged += TryActivateCards;
        image = GetComponent<Image>();
        SetCardsActive(false);
    }

    public void ActivateArtifactCards()
    {
        // Pause Gameplay 
        _timeScaleBeforePause = Time.timeScale;
        _fixedDeltaBeforePause = Time.fixedDeltaTime;
        Time.timeScale = 0f;
        // Disable floating text so it's not in the way of the artifact cards
        foreach (var text in FindObjectsByType<FloatingText>(FindObjectsSortMode.None))
        {
            text.gameObject.SetActive(false);
        }

        // Get 3 random artifacts and populate the cards
        List<ArtifactDefinition> artifacts = artifactPool.GetThreeRandomArtifacts();
        for (int i = 0; i < artifacts.Count; i++)
        {
            artifactCards[i].SetActive(true);
            artifactCards[i].GetComponent<ArtifactCard>().Populate(artifacts[i]);
        }
    }

    public void AddArtifactToPlay(GameObject artifactPrefab)
    {
        // artifactSpawnPoint can't be found in start function, needs to be done here.
        if (!artifactSpawnPoint)
        {
            artifactSpawnPoint = ServiceLocator.Get<ArtifactSpawnPoint>();
        }

        GameObject newArtifact = artifactSpawnPoint.SpawnArtifact(artifactPrefab, inPlayArtifactList.Count);
        inPlayArtifactList.Add(newArtifact);
        SetCardsActive(false);

        // Resume gameplay
        Time.timeScale = _timeScaleBeforePause;
        Time.fixedDeltaTime = _fixedDeltaBeforePause;

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
            ActivateArtifactCards();
        }
    }

    private void SetCardsActive(bool enabled)
    {
        image.enabled = enabled;
        foreach (GameObject card in artifactCards)
        {
            card.SetActive(enabled);
        }
    }
}
