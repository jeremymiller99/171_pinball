using UnityEngine;

public class SuspenseBall : Ball
{
    [SerializeField] private float baseGrowth = 1.1f;
    private float timeSinceReset;

    void Update()
    {
        if (ControlsBindingsService.WasPressedThisFrame(ControlAction.LeftFlipper) ||
            ControlsBindingsService.WasPressedThisFrame(ControlAction.RightFlipper))
        {
            timeSinceReset = 0f;
        }
        timeSinceReset += Time.deltaTime;
    }

    new void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);
    }

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        float scale = Mathf.Pow(baseGrowth, timeSinceReset);
        base.AddScore(amount * scale, typeOfScore, pos);
    }
}
