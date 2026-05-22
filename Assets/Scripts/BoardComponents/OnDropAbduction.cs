using UnityEngine;

public class OnDropAbduction : MonoBehaviour
{
    [SerializeField] private DropTarget dropTarget;
    [SerializeField] private FrenzyManager frenzyManager;
    [SerializeField] private bool frenzyModeBeenEnabled = false;
    [SerializeField] private Abductor abductor;
    public bool gettingAbducted = false;

    private void Awake()
    {
        frenzyManager = ServiceLocator.Get<FrenzyManager>();
    }

    void Start()
    {
        frenzyManager.OnFrenzyDeactivated += OnFrenzyEnd;
        dropTarget.GetComponent<BoardComponent>().onBallHit += OnBallHit;
        dropTarget.OnFullyDown += StartAbduction;
        frenzyModeBeenEnabled = false;
    }

    private void Update()
    {
        if (gettingAbducted)
        {
            dropTarget.resetTimer += Time.deltaTime;
        }
    }

    void OnBallHit()
    {
        if (!frenzyModeBeenEnabled)
        {
            dropTarget.amountOfHits = 0;
        }
    }

    void OnFrenzyEnd()
    {
        frenzyModeBeenEnabled = true;
    }

    void StartAbduction()
    {
        abductor.StartAbduction();
        gettingAbducted = true;
    }
}
