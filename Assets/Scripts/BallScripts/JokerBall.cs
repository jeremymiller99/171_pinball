using UnityEngine;

public class JokerBall : Ball
{
    new void Awake()
    {
        base.Awake();
        pointMultiplier = 1.25f;
        multMultiplier = 1.25f;
    }
}
