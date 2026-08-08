using NUnit.Framework;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class Zapper : Drone
{
    [SerializeField] private float whenToZap;
    [SerializeField] private float zapTimer;
    [SerializeField] private float timeBetweenZaps;
    [SerializeField] private BoardComponent[] boardComponents;

    void Awake()
    {
        boardComponents = FindObjectsByType<BoardComponent>(FindObjectsSortMode.None);
    }
    protected override void Update()
    {
        base.Update();
        zapTimer += Time.deltaTime;
        if (zapTimer >= whenToZap)
        {
            zapTimer = 0f;
            StartCoroutine(Zap());
        }
    }

    private IEnumerator Zap()
    {
        HitRandomBoardComponent();
        yield return new WaitForSeconds(timeBetweenZaps);
        HitRandomBoardComponent();
        yield return new WaitForSeconds(timeBetweenZaps);
        HitRandomBoardComponent();
    }

    private void HitRandomBoardComponent()
    {
        while (true)
        {
            BoardComponent randomBoardComponent = boardComponents[Random.Range(0, boardComponents.Length)];
            if (randomBoardComponent.amountToScore != 0)
            {
                randomBoardComponent.AddScore();
                break;
            }
        }
    }
}