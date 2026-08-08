using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class PowerSurgeModeDuplicator : Bumper
{
    [SerializeField] private bool isAsleep;
    [SerializeField] private float timeToSleep;
    [SerializeField] private GameRulesManager gameRulesManager;
    [FormerlySerializedAs("frenzyManager")]
    [SerializeField] private PowerSurgeManager powerSurgeManager;
    [FormerlySerializedAs("frenzyTextTransform")]
    [SerializeField] private Transform powerSurgeTextTransform;

    override protected void Awake()
    {
        base.Awake();
        gameRulesManager = ServiceLocator.Get<GameRulesManager>();
        powerSurgeManager = ServiceLocator.Get<PowerSurgeManager>();

        if (powerSurgeTextTransform == null)
        {
            powerSurgeTextTransform = transform;
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
            powerSurgeManager.ActivatePowerSurge(
                PowerSurgeSource.ModeDuplicator, powerSurgeTextTransform.position);
        }
    }

    private IEnumerator StayAsleep()
    {
        yield return new WaitForSeconds(timeToSleep);
        isAsleep = false;
    }
}
