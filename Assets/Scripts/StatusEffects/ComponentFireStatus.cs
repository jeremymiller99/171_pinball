using UnityEngine;

/// <summary>
/// Fire status for board components. Added at runtime the first time a
/// component is Fueled; while On Fire the component re-activates as if hit
/// every tick. Contact spread is handled entirely by BallFireStatus since
/// components never touch each other.
/// </summary>
public sealed class ComponentFireStatus : FireStatus
{
    private BoardComponent[] _components;

    protected override void Awake()
    {
        base.Awake();
        _components = GetComponents<BoardComponent>();
    }

    protected override void ActivateTick()
    {
        foreach (BoardComponent component in _components)
        {
            if (component != null)
            {
                component.ActivateAsIfHit();
            }
        }
    }
}
