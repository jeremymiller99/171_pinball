using System.Collections;
using UnityEngine;

public class FrenzyModeDuplicator : Bumper
{
    [SerializeField] private bool isAsleep;
    [SerializeField] private float timeToSleep;
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private FrenzyManager frenzyManager;
    [SerializeField] private Transform frenzyTextTransform;
    [SerializeField] private Vector2 frenzyTextOffset;

    override protected void Awake()
    {
        base.Awake();
        gameRulesManager = ServiceLocator.Get<GameRulesManager>();
        frenzyManager = ServiceLocator.Get<FrenzyManager>();

        if (frenzyTextTransform == null)
        {
            frenzyTextTransform = transform;
        }
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);
        if (isAsleep) return;

        BallDefinitionLink newBallDefinitionLink = collision.collider.GetComponent<BallDefinitionLink>();
        if (newBallDefinitionLink)
        {
            GameObject ballDefPrefab = newBallDefinitionLink.Definition.Prefab;
            GameObject newBall = Instantiate(ballDefPrefab, collision.transform.position, Quaternion.identity);
            gameRulesManager?.ActiveBalls?.Add(newBall);
            isAsleep = true;
            StartCoroutine(StayAsleep());
            frenzyManager.ActivateFrenzy(frenzyTextTransform.position, frenzyTextOffset);
        }
    }

    private IEnumerator StayAsleep()
    {
        yield return new WaitForSeconds(timeToSleep);
        isAsleep = false;
    }
}
