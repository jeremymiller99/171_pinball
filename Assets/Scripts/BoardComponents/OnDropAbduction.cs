using UnityEngine;

public class OnDropAbduction : MonoBehaviour
{
    [SerializeField] private Dropper dropTarget;
    [SerializeField] private Abductor abductor;
    public bool gettingAbducted = false;

    private bool _subscribed;

    /// <summary>The dropper whose full-down triggers this abduction.</summary>
    public Dropper DropTarget => dropTarget;

    void Start()
    {
        Subscribe();
    }

    /// <summary>
    /// Wires this satellite up from code, used when a replaced abduction target
    /// needs its OnDropAbduction re-created. Safe to call before or after Start —
    /// it (re)subscribes to the given dropper.
    /// </summary>
    public void Configure(Dropper dropper, Abductor owningAbductor)
    {
        Unsubscribe();
        dropTarget = dropper;
        abductor = owningAbductor;
        Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed || dropTarget == null) return;
        dropTarget.OnFullyDown += StartAbduction;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || dropTarget == null) return;
        dropTarget.OnFullyDown -= StartAbduction;
        _subscribed = false;
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (gettingAbducted)
        {
            dropTarget.resetTimer += Time.deltaTime;
        }
    }

    void StartAbduction()
    {
        abductor.StartAbduction();
        gettingAbducted = true;
    }
}
